using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Drives a smoke transform's uniform local scale from the linked <see cref="FireSource"/> intensity.
    /// Full fire → <see cref="scaleAtFullIntensity"/>; extinguished → <see cref="scaleAtZeroIntensity"/> (0 = smoke fully dissipated).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Woi/Office Fire/Smoke Scale Fire Intensity Driver")]
    public sealed class SmokeScaleFireIntensityDriver : MonoBehaviour
    {
        private const float IntensityEpsilon = 0.0001f;

        [SerializeField]
        private Transform smokeTransform;

        [SerializeField]
        private FireSource fireSource;

        [SerializeField]
        private bool autoFindFireSource = true;

        [SerializeField]
        private float scaleAtFullIntensity = 1f;

        [SerializeField]
        private float scaleAtZeroIntensity = 0f;

        [Tooltip("Seconds to ease toward the intensity-driven target scale. Higher = slower.")]
        [SerializeField, Min(0.01f)]
        private float scaleSmoothTime = 2f;

        private float _currentScale;
        private float _targetScale;
        private float _scaleVelocity;
        private float _lastIntensity = -1f;

        private void Awake()
        {
            if (smokeTransform == null)
            {
                smokeTransform = transform;
            }

            if (fireSource == null && autoFindFireSource)
            {
                fireSource = GetComponentInParent<FireSource>();
                if (fireSource == null)
                {
                    fireSource = FindFirstObjectByType<FireSource>();
                }
            }

            InitializeFullScale();
        }

        private void OnEnable()
        {
            InitializeFullScale();

            if (fireSource == null)
            {
                return;
            }

            _lastIntensity = -1f;
            fireSource.OnIntensityChanged += HandleIntensityChanged;

            float intensity = fireSource.CurrentNormalizedIntensity;
            if (intensity > IntensityEpsilon)
            {
                _lastIntensity = intensity;
                if (intensity < 1f - IntensityEpsilon)
                {
                    SetTargetFromIntensity(intensity);
                }
            }
        }

        private void OnDisable()
        {
            if (fireSource != null)
            {
                fireSource.OnIntensityChanged -= HandleIntensityChanged;
            }
        }

        private void Update()
        {
            if (smokeTransform == null)
            {
                return;
            }

            if (Mathf.Approximately(_currentScale, _targetScale))
            {
                return;
            }

            _currentScale = Mathf.SmoothDamp(_currentScale, _targetScale, ref _scaleVelocity, scaleSmoothTime);
            ApplyCurrentScale();
        }

        private void HandleIntensityChanged(float normalizedIntensity)
        {
            if (_lastIntensity < 0f)
            {
                _lastIntensity = normalizedIntensity;
                if (normalizedIntensity <= IntensityEpsilon)
                {
                    return;
                }
            }

            if (normalizedIntensity < _lastIntensity - IntensityEpsilon)
            {
                SetTargetFromIntensity(normalizedIntensity);
            }
            else if (normalizedIntensity > _lastIntensity + IntensityEpsilon)
            {
                _targetScale = scaleAtFullIntensity;
            }

            _lastIntensity = normalizedIntensity;
        }

        private void InitializeFullScale()
        {
            _currentScale = scaleAtFullIntensity;
            _targetScale = scaleAtFullIntensity;
            _scaleVelocity = 0f;
            ApplyCurrentScale();
        }

        private void SetTargetFromIntensity(float normalizedIntensity)
        {
            float clamped = Mathf.Clamp01(normalizedIntensity);
            _targetScale = Mathf.Lerp(scaleAtZeroIntensity, scaleAtFullIntensity, clamped);
        }

        private void ApplyCurrentScale()
        {
            if (smokeTransform == null)
            {
                return;
            }

            smokeTransform.localScale = Vector3.one * _currentScale;
        }
    }
}
