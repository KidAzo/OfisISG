using Obvious.Soap;
using UnityEngine;
using UnityEngine.Events;

namespace Woi.Training
{
    /// <summary>
    /// Trigger hacmine giriş/çıkışta atanmış <see cref="Material"/> üzerinde alfa animasyonlar;
    /// tüm yazma <see cref="ApplyAlpha"/> içinde yapılır.
    /// </summary>
    /// <remarks>
    /// BurnScreen: <c>_BurnAlpha</c> float — <see cref="AlphaPropertyMode.Float01"/> (varsayılan).
    /// Projede paylaşılan bir .mat atıyorsan değer tüm kullanımlara yansır; yalnız bu efekt için materyal kopyası kullan.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Woi/Training/Proximity Material Alpha Driver")]
    public sealed class ProximityMaterialAlphaDriver : MonoBehaviour
    {
        public enum AlphaPropertyMode
        {
            [Tooltip("Renk property’sinin .a değeri (ör. _BaseColor).")]
            ColorAlpha = 0,

            [Tooltip("Tek float (BurnScreen: _BurnAlpha).")]
            Float01 = 1,
        }

        [Header("Who counts as player")]
        [Tooltip("Açıksa yalnızca bu layer mask ile eşleşen colliderlar sayılır.")]
        [SerializeField]
        bool useLayerMaskOnly;

        [SerializeField]
        LayerMask playerLayers;

        [SerializeField]
        string playerTag = "Player";

        [Header("Material")]
        [SerializeField]
        Material material;

        [SerializeField]
        AlphaPropertyMode propertyMode = AlphaPropertyMode.Float01;

        [SerializeField]
        string colorPropertyName = "_BaseColor";

        [SerializeField]
        string floatPropertyName = "_BurnAlpha";

        [Header("Alpha values")]
        [SerializeField, Range(0f, 1f)]
        float alphaWhenOutside;

        [SerializeField, Range(0f, 1f)]
        float alphaWhenInside = 1f;

        [Tooltip("Saniye başına yaklaşım hızı (büyük = daha hızlı).")]
        [SerializeField, Min(0f)]
        float blendSpeed = 5f;

        [Header("Events")]
        public UnityEvent onProximityEntered;

        public UnityEvent onProximityExited;

        [SerializeField]
        ScriptableEventNoParam onProximityEnteredSoap;

        [SerializeField]
        ScriptableEventNoParam onProximityExitedSoap;

        int _overlapCount;
        bool _inside;
        float _displayAlpha;
        int _colorPropId = -1;
        int _floatPropId = -1;
        Color _baseRgb = Color.white;

#if UNITY_EDITOR
        void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null)
                c.isTrigger = true;
        }
#endif

        void Awake()
        {
            if (material == null)
            {
                Debug.LogWarning($"[{nameof(ProximityMaterialAlphaDriver)}] Assign a Material on '{name}'.", this);
                enabled = false;
                return;
            }

            if (propertyMode == AlphaPropertyMode.ColorAlpha)
            {
                _colorPropId = Shader.PropertyToID(colorPropertyName);
                if (!material.HasProperty(_colorPropId))
                {
                    Debug.LogWarning(
                        $"[{nameof(ProximityMaterialAlphaDriver)}] Material has no color property '{colorPropertyName}'.",
                        this);
                    enabled = false;
                    return;
                }

                Color c = material.GetColor(_colorPropId);
                _baseRgb = new Color(c.r, c.g, c.b, 1f);
            }
            else
            {
                _floatPropId = Shader.PropertyToID(floatPropertyName);
                if (!material.HasProperty(_floatPropId))
                {
                    Debug.LogWarning(
                        $"[{nameof(ProximityMaterialAlphaDriver)}] Material has no float property '{floatPropertyName}'.",
                        this);
                    enabled = false;
                    return;
                }
            }

            _displayAlpha = alphaWhenOutside;
            ApplyAlpha(alphaWhenOutside);
        }

        void OnDisable()
        {
            if (_inside)
            {
                onProximityExited?.Invoke();
                onProximityExitedSoap?.Raise();
            }

            _overlapCount = 0;
            _inside = false;
            if (material != null)
                ApplyAlpha(alphaWhenOutside);
        }

        public void ApplyAlpha(float alpha01)
        {
            if (material == null)
            {
                Renderer r = GetComponent<Renderer>();
                if (r != null) 
                {
                    material = r.material; // Instance material alır
                }
                else
                {
                    UnityEngine.UI.Graphic graphic = GetComponent<UnityEngine.UI.Graphic>();
                    if (graphic != null && graphic.material != null)
                    {
                        material = new Material(graphic.material);
                        graphic.material = material;
                    }
                }
            }

            if (material == null)
            {
                Debug.LogWarning($"[{nameof(ProximityMaterialAlphaDriver)}] Material BULUNAMADI! Lütfen Inspector'da Material kutusunu doldurun veya bu objeye bir Renderer/Image bileşeni ekleyin.", this);
                return;
            }

            float a = Mathf.Clamp01(alpha01);

            if (propertyMode == AlphaPropertyMode.ColorAlpha)
            {
                if (_colorPropId < 0) _colorPropId = Shader.PropertyToID(colorPropertyName);
                
                if (material.HasProperty(_colorPropId))
                {
                    Color c = material.GetColor(_colorPropId);
                    c.a = a;
                    material.SetColor(_colorPropId, c);
                    Debug.Log($"[{nameof(ProximityMaterialAlphaDriver)}] Applying ColorAlpha: {a} on {material.name}");
                }
                else
                {
                    Debug.LogWarning($"[{nameof(ProximityMaterialAlphaDriver)}] {material.name} materyalinde '{colorPropertyName}' adında bir özellik yok! Shader'ı kontrol edin.", this);
                }
            }
            else
            {
                if (_floatPropId < 0) _floatPropId = Shader.PropertyToID(floatPropertyName);
                
                if (material.HasProperty(_floatPropId))
                {
                    material.SetFloat(_floatPropId, a);
                    Debug.Log($"[{nameof(ProximityMaterialAlphaDriver)}] Applying Float01: {a} to {floatPropertyName} on {material.name}");
                }
                else
                {
                    Debug.LogWarning($"[{nameof(ProximityMaterialAlphaDriver)}] {material.name} materyalinde '{floatPropertyName}' adında bir float özelliği yok! Shader'ı kontrol edin.", this);
                }
            }
        }
    }
}
