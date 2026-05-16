using FireExtinguisher.Core;
using Obvious.Soap;
using UnityEngine;
using Woi.Equipment;

namespace Woi.Game.Training
{
    /// <summary>
    /// Oyuncu yangına telemetry benzeri ideal mesafede iken yangının ortalama yoğunluğu
    /// eşik altına (varsayılan ≤ 0.7 → alevin ~%30’u sönmüş, ~%70 kaldı) indiyinde bir kez SOAP Raise eder.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Training/Fire Distance And Suppression Milestone Soap Bridge")]
    public sealed class FireDistanceAndSuppressionMilestoneSoapBridge : MonoBehaviour
    {
        [Header("Targets")]
        [Tooltip("İzlenecek yangın.")]
        [SerializeField]
        private FireSource _fireSource;

        [Tooltip("Oyuncu mesafe ölçümü için referans (ör. gövde veya kamera). Boşsa Player Extinguisher Equipment transformu denenir.")]
        [SerializeField]
        private Transform _playerPosition;

        [Tooltip("Player Position boşsa bu ekipmanın transformu kullanılır.")]
        [SerializeField]
        private PlayerExtinguisherEquipment _playerEquipment;

        [Header("Distance (same idea as Fire Telemetry Panel)")]
        [SerializeField, Min(0.01f)]
        private float _idealDistanceMinMeters = 2f;

        [SerializeField, Min(0.01f)]
        private float _idealDistanceMaxMeters = 4f;

        [Header("Suppression milestone")]
        [Tooltip("Ortalama normalize yoğunluk (0–1) bu değerin altına inince ve mesafe uygunken event. 0.7 = ~%70 yangın kaldı (~%30 söndü).")]
        [SerializeField, Range(0.05f, 0.99f)]
        private float _remainingIntensityMaxToTrigger = 0.7f;

        [Tooltip("Yoğunluk tekrar bu kadar üstüne çıkınca milestone yeniden silahlanır (yeniden eğitim turu).")]
        [SerializeField, Range(0.01f, 0.2f)]
        private float _rearmHysteresis = 0.05f;

        [Header("Session (optional)")]
        [SerializeField]
        private ExtinguisherSessionRecorder _sessionRecorder;

        [Header("SOAP")]
        [SerializeField]
        private ScriptableEventNoParam _onPlayerAtIdealDistanceAndFireReducedEnough;

        private bool _armed = true;

        private void OnEnable()
        {
            _armed = true;
        }

        private void OnValidate()
        {
            if (_idealDistanceMaxMeters < _idealDistanceMinMeters)
                _idealDistanceMaxMeters = _idealDistanceMinMeters + 0.01f;
        }

        private void Update()
        {
            if (_fireSource == null || !_fireSource.isActiveAndEnabled)
                return;

            if (!AllowMeasurement())
                return;

            float intensity = _fireSource.CurrentNormalizedIntensity;
            float rearmLevel = Mathf.Clamp01(_remainingIntensityMaxToTrigger + _rearmHysteresis);

            if (!_armed)
            {
                if (intensity > rearmLevel)
                    _armed = true;
                return;
            }

            Transform player = ResolvePlayerTransform();
            if (player == null)
                return;

            float distance = Vector3.Distance(player.position, _fireSource.transform.position);
            bool distanceOk = distance >= _idealDistanceMinMeters && distance <= _idealDistanceMaxMeters;
            bool suppressionOk = intensity <= _remainingIntensityMaxToTrigger;

            if (distanceOk && suppressionOk)
            {
                _onPlayerAtIdealDistanceAndFireReducedEnough?.Raise();
                _armed = false;
            }
        }

        private bool AllowMeasurement()
        {
            return _sessionRecorder == null || _sessionRecorder.IsSessionActive;
        }

        private Transform ResolvePlayerTransform()
        {
            if (_playerPosition != null)
                return _playerPosition;

            if (_playerEquipment != null)
                return _playerEquipment.transform;

            return null;
        }
    }
}
