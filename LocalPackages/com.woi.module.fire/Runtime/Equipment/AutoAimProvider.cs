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

        private IAimProvider ActiveProvider
        {
            get
            {
                if (_appMode != null && _appMode.CurrentValue == AppMode.XR)
                {
                    return _vrAimProvider;
                }
                
                return _pcAimProvider;
            }
        }

        public Vector3 SprayOrigin => ActiveProvider != null ? ActiveProvider.SprayOrigin : Vector3.zero;
        public Vector3 SprayDirection => ActiveProvider != null ? ActiveProvider.SprayDirection : Vector3.forward;
        public Vector3 AimPoint => ActiveProvider != null ? ActiveProvider.AimPoint : Vector3.zero;
        public Vector3 EvaluationOrigin => ActiveProvider != null ? ActiveProvider.EvaluationOrigin : Vector3.zero;
        public Vector3 EvaluationDirection => ActiveProvider != null ? ActiveProvider.EvaluationDirection : Vector3.forward;
        public bool IsAimValid => ActiveProvider != null && ActiveProvider.IsAimValid;

        private void Awake()
        {
            if (_appMode == null)
                Debug.LogWarning("[AutoAimProvider] AppMode porting variable is not assigned! Defaulting to PC.", this);
                
            if (_pcAimProvider == null)
                _pcAimProvider = GetComponent<PCAimProvider>();
                
            if (_vrAimProvider == null)
                _vrAimProvider = GetComponent<VRAimProvider>();
        }
    }
}
