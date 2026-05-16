using FireExtinguisher.Core;
using Obvious.Soap;
using UnityEngine;
using Woi.Equipment;

namespace Woi.Game.Training
{
    /// <summary>
    /// Doğru tüple yangına etkili sıkma başladıktan <see cref="_delayBeforeMonitorSeconds"/> sn sonra
    /// <see cref="_monitorDurationSeconds"/> boyunca yatay süpürme (mevcut <see cref="SpraySweepTracker"/> kuralları) arar;
    /// pencere içinde <see cref="SpraySweepTracker.IsSweepRulePassed"/> olursa doğru SOAP, süre biterse ve olmazsa yapmadı SOAP.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Training/Extinguisher Sweep Window Soap Bridge")]
    public sealed class ExtinguisherSweepWindowSoapBridge : MonoBehaviour
    {
        [Header("Source (one of)")]
        [SerializeField]
        private PlayerExtinguisherEquipment _equipment;

        [SerializeField]
        private ExtinguisherController _fixedController;

        [Header("Session (optional)")]
        [Tooltip("Doluysa yalnızca ExtinguisherSessionRecorder oturumu aktifken ölçüm yapılır.")]
        [SerializeField]
        private ExtinguisherSessionRecorder _sessionRecorder;

        [Header("Sweep rules")]
        [Tooltip("_sessionRecorder yoksa bu ayarlar kullanılır; varsa kayıttaki SweepMonitorSettings kopyalanır.")]
        [SerializeField]
        private SpraySweepSettings _sweepSettingsFallback = new SpraySweepSettings();

        [Header("Window")]
        [SerializeField, Min(0f)]
        private float _delayBeforeMonitorSeconds = 2f;

        [SerializeField, Min(0.05f)]
        private float _monitorDurationSeconds = 5f;

        [Header("SOAP (No Param)")]
        [SerializeField]
        private ScriptableEventNoParam _onSweepCorrectInWindow;

        [SerializeField]
        private ScriptableEventNoParam _onSweepNotPerformedInWindow;

        private enum Phase
        {
            Idle,
            WaitingDelay,
            Monitoring,
            DoneAwaitSprayStop
        }

        private ExtinguisherController _bound;
        private Phase _phase;
        private float _anchorTime;
        private float _monitorStartTime;
        private float _monitorEndTime;
        private readonly SpraySweepTracker _windowTracker = new SpraySweepTracker();

        private void OnEnable()
        {
            if (_equipment != null)
            {
                _equipment.OnExtinguisherChanged += HandleEquipmentChanged;
                BindToController(_equipment.CurrentItem != null ? _equipment.CurrentItem.Controller : null);
            }
            else
                BindToController(_fixedController);
        }

        private void OnDisable()
        {
            if (_equipment != null)
                _equipment.OnExtinguisherChanged -= HandleEquipmentChanged;

            UnbindFromController();
            _phase = Phase.Idle;
        }

        private void Update()
        {
            if (_bound == null)
                return;

            if (!AllowMeasurement())
            {
                if (_phase != Phase.Idle)
                    ResetToIdle();
                return;
            }

            if (_phase == Phase.WaitingDelay)
            {
                if (!_bound.IsDischarging)
                {
                    ResetToIdle();
                    return;
                }

                if (Time.time >= _anchorTime + _delayBeforeMonitorSeconds)
                    BeginMonitoring();
            }
            else if (_phase == Phase.Monitoring)
            {
                if (Time.time >= _monitorEndTime)
                    CompleteMonitoring(success: _windowTracker.IsSweepRulePassed);
            }
        }

        private void HandleEquipmentChanged(ExtinguisherPickupItem item)
        {
            UnbindFromController();
            ResetToIdle();
            BindToController(item != null ? item.Controller : null);
        }

        private void BindToController(ExtinguisherController next)
        {
            if (next == _bound)
                return;

            UnbindFromController();
            _bound = next;

            if (_bound == null)
                return;

            _bound.OnSprayEvaluated += HandleSprayEvaluated;
            _bound.OnSprayStopped += HandleSprayStopped;
        }

        private void UnbindFromController()
        {
            if (_bound == null)
                return;

            _bound.OnSprayEvaluated -= HandleSprayEvaluated;
            _bound.OnSprayStopped -= HandleSprayStopped;
            _bound = null;
        }

        private void HandleSprayStopped()
        {
            if (_phase == Phase.DoneAwaitSprayStop)
                ResetToIdle();
            else if (_phase == Phase.WaitingDelay)
                ResetToIdle();
        }

        private void HandleSprayEvaluated(ExtinguishResult result)
        {
            if (_bound == null || !AllowMeasurement())
                return;

            if (_phase == Phase.Idle)
            {
                if (TryAnchorExtinguishStart(result))
                {
                    _anchorTime = Time.time;
                    _phase = Phase.WaitingDelay;
                    _windowTracker.Reset(ResolveSweepSettings());
                }

                return;
            }

            if (_phase != Phase.Monitoring)
                return;

            if (Time.time < _monitorStartTime || Time.time >= _monitorEndTime)
                return;

            if (result.DidHitZone)
                _windowTracker.RecordHit(Time.time, result);

            if (_windowTracker.IsSweepRulePassed)
                CompleteMonitoring(success: true);
        }

        private static bool TryAnchorExtinguishStart(in ExtinguishResult result)
        {
            return result.DidHitZone
                   && result.Compatibility == CompatibilityResult.Effective
                   && result.ExtinguishAmountCalculated > 0f;
        }

        private void BeginMonitoring()
        {
            _monitorStartTime = _anchorTime + _delayBeforeMonitorSeconds;
            _monitorEndTime = _monitorStartTime + _monitorDurationSeconds;
            _windowTracker.Reset(ResolveSweepSettings());
            _phase = Phase.Monitoring;
        }

        private void CompleteMonitoring(bool success)
        {
            if (_phase != Phase.Monitoring)
                return;

            if (success)
                _onSweepCorrectInWindow?.Raise();
            else
                _onSweepNotPerformedInWindow?.Raise();

            _phase = Phase.DoneAwaitSprayStop;
        }

        private void ResetToIdle()
        {
            _phase = Phase.Idle;
            _windowTracker.Reset(ResolveSweepSettings());
        }

        private bool AllowMeasurement()
        {
            return _sessionRecorder == null || _sessionRecorder.IsSessionActive;
        }

        private SpraySweepSettings ResolveSweepSettings()
        {
            return _sessionRecorder != null ? _sessionRecorder.SweepMonitorSettings : _sweepSettingsFallback;
        }
    }
}
