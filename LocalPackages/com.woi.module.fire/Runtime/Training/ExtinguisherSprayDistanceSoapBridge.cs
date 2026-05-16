using FireExtinguisher.Core;
using Obvious.Soap;
using UnityEngine;
using Woi.Equipment;

namespace Woi.Game.Training
{
    /// <summary>
    /// Sıkma sırasında <see cref="ExtinguishResult.HitZone"/> <b>değiştiğinde</b> (ör. Base → Upper → Custom),
    /// oyuncudan ölçüm noktasına mesafeyi yeniden hesaplar ve çok yakın / ideal / çok uzak SOAP’larından birini Raise eder.
    /// Aynı zone üzerinde ardışık tick’lerde tekrarlamaz; spray bırakılınca son zone sıfırlanır.
    /// Valf / şalter ön koşulu kapalıyken mesafe SOAP'ları yalnızca <see cref="CompatibilityResult.Effective"/> iken bastırılır.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Training/Extinguisher Spray Distance Soap Bridge")]
    public sealed class ExtinguisherSprayDistanceSoapBridge : MonoBehaviour
    {
        [Header("Source (one of)")]
        [SerializeField]
        private PlayerExtinguisherEquipment _equipment;

        [SerializeField]
        private ExtinguisherController _fixedController;

        [Header("Distance target")]
        [Tooltip("Yalnızca bu yangına ait isabetler değerlendirilir.")]
        [SerializeField]
        private FireSource _fireSource;

        [Tooltip("Boşsa Equipment transformu kullanılır.")]
        [SerializeField]
        private Transform _playerPosition;

        [Tooltip("Açıkken mesafe isabet noktasına; kapalıysa vurulan zone’un transform pozisyonuna.")]
        [SerializeField]
        private bool _useHitPointForDistance = true;

        [Header("Ideal band (m) — telemetry panel ile aynı fikir")]
        [SerializeField, Min(0.01f)]
        private float _idealDistanceMinMeters = 2f;

        [SerializeField, Min(0.01f)]
        private float _idealDistanceMaxMeters = 4f;

        [Header("Session (optional)")]
        [SerializeField]
        private ExtinguisherSessionRecorder _sessionRecorder;

        [Header("SOAP (No Param) — zone değişiminde tek bant")]
        [SerializeField]
        private ScriptableEventNoParam _onSprayingTooFarFromFire;

        [SerializeField]
        private ScriptableEventNoParam _onSprayingTooCloseToFire;

        [Tooltip("İdeal bantta zone değiştiğinde; boşsa Raise edilmez.")]
        [SerializeField]
        private ScriptableEventNoParam _onSprayingIdealDistanceForZone;

        private ExtinguisherController _bound;

        /// <summary>Son değerlendirmede bu yangın için hangi zone’a isabet edildi (null = henüz yok / sıfırlandı).</summary>
        private FireTargetZone _lastHitZone;

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
        }

        private void OnValidate()
        {
            if (_idealDistanceMaxMeters < _idealDistanceMinMeters)
                _idealDistanceMaxMeters = _idealDistanceMinMeters + 0.01f;
        }

        private void HandleEquipmentChanged(ExtinguisherPickupItem item)
        {
            UnbindFromController();
            _lastHitZone = null;
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
            _bound.OnSprayStarted += HandleSprayStarted;
            _bound.OnSprayStopped += HandleSprayStopped;
        }

        private void UnbindFromController()
        {
            if (_bound == null)
                return;

            _bound.OnSprayEvaluated -= HandleSprayEvaluated;
            _bound.OnSprayStarted -= HandleSprayStarted;
            _bound.OnSprayStopped -= HandleSprayStopped;
            _bound = null;
        }

        private void HandleSprayStarted()
        {
            _lastHitZone = null;
        }

        private void HandleSprayStopped()
        {
            _lastHitZone = null;
        }

        private void HandleSprayEvaluated(ExtinguishResult result)
        {
            if (_bound == null || !_bound.IsDischarging)
                return;

            if (!AllowMeasurement())
                return;

            if (_fireSource == null || !_fireSource.isActiveAndEnabled)
                return;

            if (!result.DidHitZone || result.Source != _fireSource || result.HitZone == null)
                return;

            if (result.Compatibility == CompatibilityResult.Effective)
            {
                var prerequisiteGate = _fireSource.GetComponent<FireExtinguishPrerequisiteGate>();
                if (prerequisiteGate != null && prerequisiteGate.ShouldSuppressTrainingSprayFeedback)
                    return;
            }

            FireTargetZone zone = result.HitZone;
            if (zone == _lastHitZone)
                return;

            _lastHitZone = zone;

            Transform player = ResolvePlayerTransform();
            if (player == null)
                return;

            Vector3 measurePoint = _useHitPointForDistance ? result.HitPoint : zone.transform.position;
            float d = Vector3.Distance(player.position, measurePoint);

            if (d < _idealDistanceMinMeters)
                _onSprayingTooCloseToFire?.Raise();
            else if (d > _idealDistanceMaxMeters)
                _onSprayingTooFarFromFire?.Raise();
            else
                _onSprayingIdealDistanceForZone?.Raise();
        }

        private bool AllowMeasurement()
        {
            return _sessionRecorder == null || _sessionRecorder.IsSessionActive;
        }

        private Transform ResolvePlayerTransform()
        {
            if (_playerPosition != null)
                return _playerPosition;

            if (_equipment != null)
                return _equipment.transform;

            return _bound != null ? _bound.transform : null;
        }
    }
}
