using FireExtinguisher.Core;
using UnityEngine;
using Woi.Equipment;
using Woi.Training;

namespace Woi.Game.Training.Feedback
{
    /// <summary>
    /// Yangına etkili sıkış turlarında kamera (veya atanmış transform) için hafif screen shake.
    /// Oyuncu–yangın mesafesi <see cref="criticalDistance"/> altına indikçe sarsıntı güçlenir (HUD critical bandı ile uyumlu düşünülebilir).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("WOI/Training/Feedback/Extinguish Proximity Screen Shake")]
    public sealed class ExtinguishProximityScreenShake : MonoBehaviour
    {
        [Header("Routing")]
        [Tooltip("Oyuncu ekipmanı; sıkılan söndürücünün ExtinguisherController'ına abone olur.")]
        [SerializeField] private PlayerExtinguisherEquipment _equipment;

        [Tooltip("Oyuncu konumu (mesafe için). Boşsa ekipmanın ana transformu.")]
        [SerializeField] private Transform _playerRoot;

        [Header("Shake target")]
        [Tooltip("Genelde ana kamera veya kameranın çocuğu; localPosition ile sarsılır, başlangıç pozisyonu Awake'te saklanır.")]
        [SerializeField] private Transform _shakeTarget;

        [Header("Distance")]
        [Tooltip("Metre. Oyuncu bu mesafeden yakınsa sarsıntı çarpanı artar (0 = çarpan kapalı).")]
        [SerializeField, Min(0.01f)] private float criticalDistance = 1.2f;

        [Tooltip("Tam critical içindeyken (mesafe ~0) taban sarsıntıya uygulanan maksimum ek çarpan.")]
        [SerializeField, Min(1f)] private float maxCloseDistanceMultiplier = 2.2f;

        [Header("Strength")]
        [Tooltip("Her etkili söndürme tick'inde biriken genlik (birimler normalize; sahne ile ayarlayın).")]
        [SerializeField, Min(0f)] private float impulsePerEffectiveTick = 0.08f;

        [Tooltip("Tick başına biriken üst sınır (clamp).")]
        [SerializeField, Min(0f)] private float maxAccumulatedAmplitude = 1.25f;

        [Tooltip("Saniye başına üstel sönüm (büyük = daha hızlı durur).")]
        [SerializeField, Min(0f)] private float amplitudeDecayPerSecond = 5f;

        [Tooltip("Perlin gürültüsü ile ofset ölçeği (metre cinsinden local).")]
        [SerializeField, Min(0f)] private float positionalScale = 0.035f;

        private ExtinguisherController _controller;
        private Vector3 _initialLocalPosition;
        private float _amplitude;
        private float _noiseTime;

        private void OnEnable()
        {
            if (_shakeTarget != null)
                _initialLocalPosition = _shakeTarget.localPosition;

            if (_equipment != null)
                _equipment.OnExtinguisherChanged += HandleEquipmentChanged;

            BindController(_equipment != null ? _equipment.CurrentItem : null);
        }

        private void OnDisable()
        {
            if (_equipment != null)
                _equipment.OnExtinguisherChanged -= HandleEquipmentChanged;

            UnbindController();
            ClearShakePose();
        }

        private void LateUpdate()
        {
            if (_shakeTarget == null)
                return;

            float dt = Time.deltaTime;
            if (dt > 0f && amplitudeDecayPerSecond > 0f)
                _amplitude *= Mathf.Exp(-amplitudeDecayPerSecond * dt);

            _noiseTime += dt * 18f;

            float a = _amplitude;
            if (a < 0.0001f)
            {
                _shakeTarget.localPosition = _initialLocalPosition;
                return;
            }

            float nx = Mathf.PerlinNoise(_noiseTime, 1.7f) - 0.5f;
            float ny = Mathf.PerlinNoise(2.3f, _noiseTime) - 0.5f;
            float nz = Mathf.PerlinNoise(_noiseTime * 0.7f, _noiseTime * 0.9f) - 0.5f;
            Vector3 offset = new Vector3(nx, ny, nz) * 2f * (a * positionalScale);
            _shakeTarget.localPosition = _initialLocalPosition + offset;
        }

        private void HandleEquipmentChanged(ExtinguisherPickupItem item) =>
            BindController(item);

        private void BindController(ExtinguisherPickupItem item)
        {
            UnbindController();

            if (item == null)
                return;

            _controller = item.Controller;
            if (_controller == null)
                return;

            _controller.OnSprayEvaluated += HandleSprayEvaluated;
        }

        private void UnbindController()
        {
            if (_controller != null)
            {
                _controller.OnSprayEvaluated -= HandleSprayEvaluated;
                _controller = null;
            }
        }

        private void HandleSprayEvaluated(ExtinguishResult result)
        {
            if (!result.DidHitZone)
                return;

            if (result.Compatibility != CompatibilityResult.Effective)
                return;

            if (result.ExtinguishAmountCalculated <= 0f || result.Source == null)
                return;

            Transform player = _playerRoot != null ? _playerRoot : (_equipment != null ? _equipment.transform : null);
            if (player == null)
                return;

            float distance = Vector3.Distance(player.position, result.Source.transform.position);
            if (ForcedCriticalProximityRegistry.IsForcedFor(result.Source))
                distance = Mathf.Min(distance, criticalDistance * 0.5f);

            float closeMul = DistanceToShakeMultiplier(distance);

            float add = impulsePerEffectiveTick * closeMul;
            _amplitude = Mathf.Min(_amplitude + add, maxAccumulatedAmplitude);
        }

        private float DistanceToShakeMultiplier(float distanceMeters)
        {
            if (criticalDistance <= 0.01f || maxCloseDistanceMultiplier <= 1f)
                return 1f;

            if (distanceMeters >= criticalDistance)
                return 1f;

            float t = 1f - Mathf.Clamp01(distanceMeters / criticalDistance);
            return Mathf.Lerp(1f, maxCloseDistanceMultiplier, t);
        }

        private void ClearShakePose()
        {
            _amplitude = 0f;
            if (_shakeTarget != null)
                _shakeTarget.localPosition = _initialLocalPosition;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (maxCloseDistanceMultiplier < 1f)
                maxCloseDistanceMultiplier = 1f;
        }
#endif
    }
}
