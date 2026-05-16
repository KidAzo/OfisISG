using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Woi.Fire.LoginScreenVr
{
    /// <summary>
    /// Renders a VR login <see cref="UIDocument"/> into a <see cref="RenderTexture"/>, displays it on a 3D quad,
    /// and maps XR controller rays to panel coordinates (Unity 6 <see cref="PanelSettings.SetScreenToPanelSpaceFunction"/> pattern).
    /// Requires <see cref="XRUIToolkitManager"/> in the scene (auto-created if missing) and
    /// <see cref="PanelInputConfiguration"/> with "No input redirection" per XR Interaction Toolkit docs.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    public sealed class VRLoginDocumentMount : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] UIDocument uiDocument;
        [Tooltip("Cloned at runtime; use the same Panel Settings family as PC (World Space / RT compatible).")]
        [SerializeField] PanelSettings panelSettingsTemplate;

        [Header("RenderTexture & quad")]
        [SerializeField] int renderTextureWidth = 1536;
        [SerializeField] int renderTextureHeight = 864;
        [SerializeField] MeshRenderer panelQuadRenderer;
        [SerializeField] MeshCollider panelQuadCollider;
        [SerializeField] float maxRaycastDistance = 25f;

        [Header("XR")]
        [SerializeField] XRRayInteractor uiRayInteractor;
        [SerializeField] bool autoCreateXrUiToolkitManager = true;

        RenderTexture _renderTexture;
        PanelSettings _runtimePanelSettings;
        Material _runtimeQuadMaterial;
        bool _hasValidRayHit;
        Vector2 _lastPanelTexels;
        static bool _warnedMissingInteractor;
        static bool _warnedMissingTemplate;

        void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (panelSettingsTemplate == null)
            {
                if (!_warnedMissingTemplate)
                {
                    _warnedMissingTemplate = true;
                    Debug.LogWarning(
                        "[VRLoginDocumentMount] Assign a Panel Settings template (duplicate your PC PanelSettings asset if needed).",
                        this);
                }

                return;
            }

            if (panelQuadRenderer == null || panelQuadCollider == null)
            {
                Debug.LogError("[VRLoginDocumentMount] Assign panel Quad MeshRenderer and MeshCollider.", this);
                return;
            }

            var desc = new RenderTextureDescriptor(renderTextureWidth, renderTextureHeight, RenderTextureFormat.ARGB32, 24)
            {
                msaaSamples = 1
            };
            _renderTexture = new RenderTexture(desc)
            {
                name = "VR_Login_UI_RT",
                hideFlags = HideFlags.DontSave
            };
            _renderTexture.Create();

            _runtimePanelSettings = Instantiate(panelSettingsTemplate);
            _runtimePanelSettings.name = "VR_Login_PanelSettings (runtime)";
            _runtimePanelSettings.targetTexture = _renderTexture;
            _runtimePanelSettings.clearColor = true;
            _runtimePanelSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);

            uiDocument.panelSettings = _runtimePanelSettings;

            _runtimeQuadMaterial = CreateUnlitMaterialForTexture(_renderTexture);
            ConfigureQuadMaterialForUiRenderTexture(_runtimeQuadMaterial);
            panelQuadRenderer.sharedMaterial = _runtimeQuadMaterial;

            _runtimePanelSettings.SetScreenToPanelSpaceFunction(ScreenToPanel);

            if (autoCreateXrUiToolkitManager)
                EnsureXrUiToolkitManager();

            if (uiRayInteractor != null)
                uiRayInteractor.enableUIInteraction = true;
        }

        void OnDestroy()
        {
            if (_runtimePanelSettings != null)
            {
                _runtimePanelSettings.SetScreenToPanelSpaceFunction(null);
                Destroy(_runtimePanelSettings);
            }

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }

            if (_runtimeQuadMaterial != null)
                Destroy(_runtimeQuadMaterial);
        }

        void LateUpdate()
        {
            UpdateRayHitState();
        }

        void UpdateRayHitState()
        {
            _hasValidRayHit = false;

            if (panelQuadCollider == null || _renderTexture == null)
                return;

            if (uiRayInteractor == null)
            {
                if (!_warnedMissingInteractor)
                {
                    _warnedMissingInteractor = true;
                    Debug.LogWarning(
                        "[VRLoginDocumentMount] No XRRayInteractor assigned — assign the controller ray used for UI (enable UI Interaction on the interactor).",
                        this);
                }

                return;
            }

            if (!TryGetInteractorWorldRay(uiRayInteractor, out Ray worldRay))
                return;

            if (!Physics.Raycast(worldRay, out RaycastHit hit, maxRaycastDistance, ~0, QueryTriggerInteraction.Ignore))
                return;

            if (hit.collider != panelQuadCollider)
                return;

            Vector2 uv = hit.textureCoord;
            uv.y = 1f - uv.y;
            _lastPanelTexels = new Vector2(uv.x * _renderTexture.width, uv.y * _renderTexture.height);
            _hasValidRayHit = true;
        }

        Vector2 ScreenToPanel(Vector2 _)
        {
            if (!_hasValidRayHit)
                return new Vector2(float.NaN, float.NaN);
            return _lastPanelTexels;
        }

        static bool TryGetInteractorWorldRay(XRRayInteractor interactor, out Ray ray)
        {
            Transform origin = interactor.attachTransform != null ? interactor.attachTransform : interactor.transform;
            ray = new Ray(origin.position, origin.forward);
            return true;
        }

        static Material CreateUnlitMaterialForTexture(RenderTexture rt)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Texture");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader) { name = "VR_Login_RT_Unlit (runtime)" };
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", rt);
            else
                mat.mainTexture = rt;
            return mat;
        }

        /// <summary>
        /// RT is cleared to transparent; quad must blend alpha so empty pixels are not an opaque black plate.
        /// </summary>
        static void ConfigureQuadMaterialForUiRenderTexture(Material mat)
        {
            if (mat == null)
                return;

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_Blend"))
                    mat.SetFloat("_Blend", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            }

            if (mat.HasProperty("_SrcBlend"))
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))
                mat.SetInt("_ZWrite", 0);

            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        static void EnsureXrUiToolkitManager()
        {
            if (Object.FindObjectsByType<XRUIToolkitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0)
                return;

            var go = new GameObject("XRUIToolkitManager (auto)");
            go.AddComponent<XRUIToolkitManager>();
            Debug.Log(
                "[VRLoginDocumentMount] Created XRUIToolkitManager. Add PanelInputConfiguration (No input redirection) on your Event System if UI input does not reach the panel.");
        }

        /// <summary>Reserved for external wiring; RT is owned by this component.</summary>
        public void BindToRenderTexturePanel(RenderTexture _)
        {
        }

        public RenderTexture PanelRenderTexture => _renderTexture;
    }
}
