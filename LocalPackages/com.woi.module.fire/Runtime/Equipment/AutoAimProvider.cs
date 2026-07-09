using FireExtinguisher.Core;
using FireExtinguisher.PC;
using FireExtinguisher.VR;
using UnityEngine;

namespace FireExtinguisher.Porting
{
    /// <summary>
    /// PC ve VR Aim Provider'ları arasında ScriptableEnumPortingVariable değerine göre
    /// otomatik geçiş yapan bir sarıcı (wrapper) sınıftır.
    /// ExtinguisherController'a "Aim Provider Source" olarak bu scripti verebilirsiniz.
    /// </summary>
    [AddComponentMenu("Fire Extinguisher/Porting/Auto Aim Provider")]
    public sealed class AutoAimProvider : MonoBehaviour, IAimProvider
    {
        [Header("Mode Variable")]
        [Tooltip("Hangi modda olduğumuzu belirten Soap değişkeni (XR veya PC).")]
        [SerializeField] private ScriptableEnumPortingVariable _appMode;

        [Header("Providers")]
        [SerializeField] private PCAimProvider _pcAimProvider;
        [SerializeField] private VRAimProvider _vrAimProvider;

        bool UseVrProvider =>
            _appMode != null && _appMode.CurrentValue == AppMode.XR
            || (_appMode == null && FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR)
            || (_appMode == null && !FirePlatformRuntime.IsSourceInitialized && IsXrDeviceActiveFallback());

        IAimProvider ActiveProvider => UseVrProvider ? _vrAimProvider : _pcAimProvider;

        public Vector3 SprayOrigin => ActiveProvider != null ? ActiveProvider.SprayOrigin : Vector3.zero;
        public Vector3 SprayDirection => ActiveProvider != null ? ActiveProvider.SprayDirection : Vector3.forward;
        public Vector3 AimPoint => ActiveProvider != null ? ActiveProvider.AimPoint : Vector3.zero;
        public Vector3 EvaluationOrigin => ActiveProvider != null ? ActiveProvider.EvaluationOrigin : Vector3.zero;
        public Vector3 EvaluationDirection => ActiveProvider != null ? ActiveProvider.EvaluationDirection : Vector3.forward;
        public bool IsAimValid => ActiveProvider != null && ActiveProvider.IsAimValid;

        private void Awake()
        {
            if (_appMode == null)
                Debug.LogWarning("[AutoAimProvider] AppMode porting variable is not assigned! Using FirePlatformRuntime / XR device fallback.", this);

            if (_pcAimProvider == null)
                _pcAimProvider = GetComponent<PCAimProvider>();

            if (_vrAimProvider == null)
                _vrAimProvider = GetComponent<VRAimProvider>();

            ApplyProviderEnabledState();
        }

        private void OnEnable()
        {
            ApplyProviderEnabledState();
        }

        void ApplyProviderEnabledState()
        {
            bool useVr = UseVrProvider;

            if (_pcAimProvider != null)
                _pcAimProvider.enabled = !useVr;

            if (_vrAimProvider != null)
                _vrAimProvider.enabled = useVr;
        }

        static bool IsXrDeviceActiveFallback()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return UnityEngine.XR.XRSettings.isDeviceActive;
#else
            return false;
#endif
        }
    }
}
