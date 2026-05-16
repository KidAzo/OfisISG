using System.Collections.Generic;
using FireExtinguisher.Core;
using Obvious.Soap;
using UnityEngine;
using Woi.Equipment;

namespace Woi.Training
{
    /// <summary>
    /// Bölge isabetinde tüp–yangın uyumuna göre SOAP yükseltir.
    /// <b>Doğru tüp</b> (<see cref="CompatibilityResult.Effective"/>): her <see cref="FireSource"/> için en fazla bir kez.
    /// <b>Yanlış tüp</b>: aynı yangında koşul sürerken yapılandırılan saniye aralığıyla tekrarlanabilir.
    /// Valf / şalter ön koşulu kapalıyken (<see cref="FireExtinguishPrerequisiteGate.ShouldSuppressTrainingSprayFeedback"/>)
    /// yalnızca <b>doğru tüp</b> SOAP'ı bastırılır; yanlış tüp mesajı oyuncunun tüp değiştirmesi için çalışmaya devam eder.
    /// Durum <see cref="OnDisable"/> ile temizlenir.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Training/Extinguisher Spray Tube Soap Bridge")]
    public sealed class ExtinguisherSprayTubeSoapBridge : MonoBehaviour
    {
        [Header("Source (one of)")]
        [Tooltip("Oyuncu ekipmanı — değişimde otomatik yeniden bağlanır.")]
        [SerializeField]
        private PlayerExtinguisherEquipment _equipment;

        [Tooltip("_equipment boşsa kullanılır: sabit bir ExtinguisherController.")]
        [SerializeField]
        private ExtinguisherController _fixedController;

        [Header("SOAP (No Param)")]
        [Tooltip("Her yangın için en fazla bir kez: tüp–yangın uyumu Effective ise Raise.")]
        [SerializeField]
        private ScriptableEventNoParam _onSprayCorrectTubeForFire;

        [Tooltip("Yanlış uyumda (Forbidden / Neutral): aynı yangında bu süre dolmadan tekrar Raise edilmez.")]
        [SerializeField]
        private ScriptableEventNoParam _onSprayWrongTubeForFire;

        [Tooltip("Aynı yangında yanlış tüp SOAP’ı için minimum saniye aralığı.")]
        [SerializeField]
        [Min(0f)]
        private float _wrongTubeSoapIntervalSeconds = 5f;

        private ExtinguisherController _bound;
        private readonly HashSet<FireSource> _correctTubeRaisedForFire = new HashSet<FireSource>();
        private readonly Dictionary<FireSource, float> _lastWrongTubeSoapTimeForFire = new Dictionary<FireSource, float>();

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
            _correctTubeRaisedForFire.Clear();
            _lastWrongTubeSoapTimeForFire.Clear();
        }

        private void HandleEquipmentChanged(ExtinguisherPickupItem item)
        {
            UnbindFromController();
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
        }

        private void UnbindFromController()
        {
            if (_bound == null)
                return;

            _bound.OnSprayEvaluated -= HandleSprayEvaluated;
            _bound = null;
        }

        private void HandleSprayEvaluated(ExtinguishResult result)
        {
            if (!result.DidHitZone || result.Source == null)
                return;

            FireSource fire = result.Source;

            if (result.Compatibility == CompatibilityResult.Effective)
            {
                var prerequisiteGate = fire.GetComponent<FireExtinguishPrerequisiteGate>();
                if (prerequisiteGate != null && prerequisiteGate.ShouldSuppressTrainingSprayFeedback)
                    return;

                if (_correctTubeRaisedForFire.Contains(fire))
                    return;

                _correctTubeRaisedForFire.Add(fire);
                _onSprayCorrectTubeForFire?.Raise();
                return;
            }

            float now = Time.time;
            if (_lastWrongTubeSoapTimeForFire.TryGetValue(fire, out float lastWrong) &&
                now - lastWrong < _wrongTubeSoapIntervalSeconds)
                return;

            _lastWrongTubeSoapTimeForFire[fire] = now;
            TrainingVrTransientSoapFireContext.LastWrongTubeFire = fire;
            _onSprayWrongTubeForFire?.Raise();
        }
    }
}
