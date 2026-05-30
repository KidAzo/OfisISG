using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Drives a smoke transform's uniform local scale from the linked <see cref="FireSource"/> intensity.
    /// Full fire → <see cref="scaleAtFullIntensity"/>; extinguished → <see cref="scaleAtZeroIntensity"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Smoke Scale Fire Intensity Driver")]
    public sealed class SmokeScaleFireIntensityDriver : MonoBehaviour
    {
        [SerializeField]
        private Transform smokeTransform;

        [SerializeField]
        private FireSource fireSource;

        [SerializeField]
        private bool autoFindFireSource = true;

        [SerializeField]
        private float scaleAtFullIntensity = 1f;

        [SerializeField]
        private float scaleAtZeroIntensity = 0.5f;

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
        }

        private void OnEnable()
        {
            if (fireSource != null)
            {
                fireSource.OnIntensityChanged += HandleIntensityChanged;
                ApplyScale(fireSource.CurrentNormalizedIntensity);
            }
        }

        private void OnDisable()
        {
            if (fireSource != null)
            {
                fireSource.OnIntensityChanged -= HandleIntensityChanged;
            }
        }

        private void HandleIntensityChanged(float normalizedIntensity)
        {
            ApplyScale(normalizedIntensity);
        }

        private void ApplyScale(float normalizedIntensity)
        {
            if (smokeTransform == null)
            {
                return;
            }

            float clamped = Mathf.Clamp01(normalizedIntensity);
            float scale = Mathf.Lerp(scaleAtZeroIntensity, scaleAtFullIntensity, clamped);
            smokeTransform.localScale = Vector3.one * scale;
        }
    }
}
