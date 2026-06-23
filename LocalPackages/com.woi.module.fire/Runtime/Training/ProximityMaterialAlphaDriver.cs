using Obvious.Soap;
using UnityEngine;
using UnityEngine.Events;

namespace Woi.Training
{
    /// <summary>
    /// Material üzerinde alfa yazar. BurnScreen quad görünürlüğü için <see cref="BurnScreenQuadToggle"/> kullan.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Woi/Training/Proximity Material Alpha Driver")]
    public sealed class ProximityMaterialAlphaDriver : MonoBehaviour
    {
        public enum AlphaPropertyMode
        {
            ColorAlpha = 0,
            Float01 = 1,
        }

        [Header("Material")]
        [SerializeField]
        Material material;

        [SerializeField]
        AlphaPropertyMode propertyMode = AlphaPropertyMode.Float01;

        [SerializeField]
        string colorPropertyName = "_BaseColor";

        [SerializeField]
        string floatPropertyName = "_Alpha";

        [Header("Alpha values")]
        [SerializeField, Range(0f, 1f)]
        float alphaWhenOutside;

        [SerializeField, Range(0f, 1f)]
        float alphaWhenInside = 1f;

        [Header("Events")]
        public UnityEvent onProximityEntered;

        public UnityEvent onProximityExited;

        [SerializeField]
        ScriptableEventNoParam onProximityEnteredSoap;

        [SerializeField]
        ScriptableEventNoParam onProximityExitedSoap;

        int _colorPropId = -1;
        int _floatPropId = -1;

        void Awake()
        {
            if (material == null)
            {
                Debug.LogWarning($"[{nameof(ProximityMaterialAlphaDriver)}] Assign a Material on '{name}'.", this);
                enabled = false;
                return;
            }

            material = new Material(material);
            ApplyAlpha(alphaWhenOutside);
        }

        public void ApplyAlpha(float alpha01)
        {
            if (material == null)
                return;

            if (propertyMode == AlphaPropertyMode.ColorAlpha)
            {
                if (_colorPropId < 0)
                    _colorPropId = Shader.PropertyToID(colorPropertyName);

                if (material.HasProperty(_colorPropId))
                {
                    Color c = material.GetColor(_colorPropId);
                    c.a = Mathf.Clamp01(alpha01);
                    material.SetColor(_colorPropId, c);
                }

                return;
            }

            if (_floatPropId < 0)
                _floatPropId = Shader.PropertyToID(floatPropertyName);

            if (material.HasProperty(_floatPropId))
                material.SetFloat(_floatPropId, Mathf.Clamp01(alpha01));
        }
    }
}
