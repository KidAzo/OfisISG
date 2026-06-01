using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Woi.UI.Result;
using WOI.Modules.SDK;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// VR: positions the shared WasteCollection UIDocument in world space in front of the HMD.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(250)]
    public sealed class WasteWorldUiPresenter : MonoBehaviour
    {
        private const string WorldPanelSettingsPath =
            "Assets/Project/OfficeFire/UI/InteractHoverWorldPanelSettings.asset";

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private PanelSettings worldPanelSettingsSource;
        [SerializeField] private Transform xrRigRoot;
        [SerializeField] private Transform cameraOverride;
        [FormerlySerializedAs("distanceInFrontOfCamera")]
        [SerializeField] private Vector3 localOffsetFromEye = new(0f, 0f, 1.35f);
        [SerializeField] private float billboardYawOffsetDegrees;
        [SerializeField] private bool detachFromParentWhileFollowing = true;
        [Tooltip("Pixel UI → world metres. Try 0.004–0.008 if UI looks invisible in headset.")]
        [SerializeField] private float worldDocumentScale = 0.005f;

        [Header("World panel size (pixels, before scale)")]
        [SerializeField] private bool useFixedWorldPanelSize = true;
        [SerializeField] private Vector2 fixedWorldPanelPixels = new(960f, 820f);

        private PanelSettings runtimePanelSettings;
        private bool configuredForVr;
        private bool followActive;
        private bool pendingFollowRequest;
        private bool pendingLayoutRefresh;
        private bool detachedForFollow;
        private Transform parentBeforeDetach;
        private Vector3 localOffsetFromEyeRuntime;
        private bool warnedNoHeadCamera;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            ResolveXrRigRoot();
            localOffsetFromEyeRuntime = localOffsetFromEye;
            ResolveWorldPanelSettings();
        }

        public void SetFollowActive(bool active)
        {
            if (active && !configuredForVr && uiDocument != null)
                ConfigureWorldDocument();

            bool wasFollowing = followActive;
            if (active && !configuredForVr)
            {
                pendingFollowRequest = true;
                return;
            }

            pendingFollowRequest = false;
            followActive = active && configuredForVr;

            if (followActive && !wasFollowing)
            {
                pendingLayoutRefresh = true;
                if (detachFromParentWhileFollowing)
                    DetachForWorldFollow();
                SnapInFrontOfEye();
            }
            else if (!followActive && wasFollowing)
            {
                RestoreParentAfterFollow();
            }
        }

        public void SetUiDistance(float distanceMeters)
        {
            float distance = Mathf.Max(0.25f, distanceMeters);
            localOffsetFromEyeRuntime = new Vector3(0f, 0f, distance);
        }

        public void ApplyLayoutFromInspector()
        {
            if (uiDocument == null)
                return;

            ApplyVrWorldSpaceLayout();

            if (uiDocument.rootVisualElement != null)
            {
                ApplyWorldSpaceRootLayout();
                RefreshPanelAfterLayout();
            }
        }

        private void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRenderSnap;
            StartCoroutine(BootstrapVrWhenReady());
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRenderSnap;
            RestoreParentAfterFollow();
        }

        private IEnumerator BootstrapVrWhenReady()
        {
            while (enabled && !WasteCollectionPlatform.ShouldUseVrPresentation())
            {
                if (FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsPC)
                    yield break;

                yield return null;
            }

            if (!enabled || !WasteCollectionPlatform.ShouldUseVrPresentation())
                yield break;

            yield return ConfigureWhenReady();
        }

        private IEnumerator ConfigureWhenReady()
        {
            int safety = 120;
            while (safety-- > 0 && enabled && (uiDocument == null || uiDocument.rootVisualElement == null))
                yield return null;

            if (!enabled || uiDocument == null)
                yield break;

            ConfigureWorldDocument();

            if (pendingFollowRequest)
                SetFollowActive(true);

            yield return null;
            yield return null;
            ApplyLayoutFromInspector();
        }

        private void LateUpdate()
        {
            if (pendingLayoutRefresh && configuredForVr)
            {
                pendingLayoutRefresh = false;
                ApplyLayoutFromInspector();
            }

            if (followActive && configuredForVr)
                SnapInFrontOfEye();
        }

        private void OnBeforeRenderSnap()
        {
            if (!followActive || !configuredForVr)
                return;

            SnapInFrontOfEye();
        }

        private void SnapInFrontOfEye()
        {
            if (!TryResolveHeadCamera(out Camera headCamera))
            {
                if (!warnedNoHeadCamera)
                {
                    warnedNoHeadCamera = true;
                    Debug.LogWarning(
                        "[WasteWorldUiPresenter] Head camera not found — UI stays at (0,0,0). " +
                        "Set Xr Rig Root on WasteVrLocomotionGate to your active XR Origin.",
                        this);
                }

                return;
            }

            warnedNoHeadCamera = false;
            Transform eye = headCamera.transform;
            Vector3 pos = eye.TransformPoint(localOffsetFromEyeRuntime);
            Quaternion rot = ComputeBillboardRotation(eye, pos);
            transform.SetPositionAndRotation(pos, rot);
            RefreshWorldSpacePanelAfterMove();
        }

        private Quaternion ComputeBillboardRotation(Transform eye, Vector3 panelWorldPosition)
        {
            Vector3 toCamera = eye.position - panelWorldPosition;
            if (toCamera.sqrMagnitude < 1e-6f)
                return eye.rotation;

            Quaternion face = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            if (Mathf.Abs(billboardYawOffsetDegrees) > 1e-3f)
                face *= Quaternion.Euler(0f, billboardYawOffsetDegrees, 0f);
            return face;
        }

        private void DetachForWorldFollow()
        {
            if (detachedForFollow || transform.parent == null)
                return;

            parentBeforeDetach = transform.parent;
            transform.SetParent(null, true);
            detachedForFollow = true;
        }

        private void RestoreParentAfterFollow()
        {
            if (!detachedForFollow)
                return;

            transform.SetParent(parentBeforeDetach, true);
            parentBeforeDetach = null;
            detachedForFollow = false;
        }

        public void ConfigureWorldDocument()
        {
            if (configuredForVr || uiDocument == null)
                return;

            ResolveWorldPanelSettings();
            if (worldPanelSettingsSource == null)
            {
                Debug.LogError("[WasteWorldUiPresenter] World PanelSettings not assigned.", this);
                return;
            }

            runtimePanelSettings = Instantiate(worldPanelSettingsSource);
            runtimePanelSettings.name = worldPanelSettingsSource.name + " (Waste VR Runtime)";
            runtimePanelSettings.renderMode = PanelRenderMode.WorldSpace;
            TrySetColliderUpdateAlways(runtimePanelSettings);

            uiDocument.panelSettings = runtimePanelSettings;
            ApplyVrWorldSpaceLayout();
            ApplyWorldSpaceRootLayout();

            if (GetComponent<ExitPanelNearFarUiBootstrap>() == null)
                gameObject.AddComponent<ExitPanelNearFarUiBootstrap>();

            configuredForVr = true;
        }

        private void ApplyVrWorldSpaceLayout()
        {
            if (useFixedWorldPanelSize)
            {
                uiDocument.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
                uiDocument.worldSpaceSize = new Vector2(
                    Mathf.Max(320f, fixedWorldPanelPixels.x),
                    Mathf.Max(240f, fixedWorldPanelPixels.y));
            }
            else
            {
                uiDocument.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Dynamic;
            }

            uiDocument.pivot = Pivot.Center;
            uiDocument.pivotReferenceSize = PivotReferenceSize.BoundingBox;

            float scale = Mathf.Clamp(worldDocumentScale, 0.0005f, 0.05f);
            transform.localScale = Vector3.one * scale;
            localOffsetFromEyeRuntime = localOffsetFromEye;
        }

        private void ApplyWorldSpaceRootLayout()
        {
            VisualElement root = uiDocument.rootVisualElement;
            if (root == null)
                return;

            root.EnableInClassList("ui-root--vr-world", true);
            root.style.flexGrow = 0;
            root.style.width = fixedWorldPanelPixels.x;
            root.style.height = StyleKeyword.Auto;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
        }

        private void RefreshPanelAfterLayout()
        {
            RefreshWorldSpacePanelAfterMove();
        }

        /// <summary>
        /// World-space UIDocument does not always redraw when only the transform moves (Inspector click forces OnValidate).
        /// </summary>
        private void RefreshWorldSpacePanelAfterMove()
        {
            if (uiDocument == null)
                return;

            VisualElement root = uiDocument.rootVisualElement;
            if (root == null)
                return;

            root.MarkDirtyRepaint();

            // Next UI Toolkit tick — same effect as selecting the object in the Hierarchy.
            root.schedule.Execute(_ => root.MarkDirtyRepaint());
        }

        private void EnsureVrUiSessionController()
        {
            if (GetComponent<WasteVrUiSessionController>() != null)
                return;

            gameObject.AddComponent<WasteVrUiSessionController>();
        }

        private bool IsAnyModalUiVisible()
        {
            WasteSelectionMenu menu = GetComponent<WasteSelectionMenu>();
            if (menu != null && menu.IsVisible)
                return true;

            WasteResultScreenController result = GetComponent<WasteResultScreenController>();
            if (result != null && result.IsVisible)
                return true;

            return false;
        }

        private void ResolveXrRigRoot()
        {
            if (xrRigRoot != null)
                return;

            WasteVrLocomotionGate gate = GetComponent<WasteVrLocomotionGate>();
            if (gate != null && gate.XrRigRoot != null)
                xrRigRoot = gate.XrRigRoot;
        }

        private bool TryResolveHeadCamera(out Camera headCamera)
        {
            headCamera = null;
            ResolveXrRigRoot();

            if (WasteVrHeadCameraResolver.TryGetHeadCamera(xrRigRoot, out headCamera))
                return true;

            if (cameraOverride != null)
            {
                Camera overrideCam = cameraOverride.GetComponent<Camera>();
                if (overrideCam != null && overrideCam.isActiveAndEnabled)
                {
                    headCamera = overrideCam;
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (uiDocument == null)
                return;

            if (!Application.isPlaying)
            {
                ResolveWorldPanelSettings();
                if (worldPanelSettingsSource != null)
                    uiDocument.panelSettings = worldPanelSettingsSource;
            }

            ApplyLayoutFromInspector();
        }

        [ContextMenu("Apply VR World Space Layout (Editor Preview)")]
        public void ApplyEditorScenePreview()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            ResolveWorldPanelSettings();
            if (worldPanelSettingsSource != null)
                uiDocument.panelSettings = worldPanelSettingsSource;

            ApplyLayoutFromInspector();

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(uiDocument);
        }
#endif

        private static void TrySetColliderUpdateAlways(PanelSettings settings)
        {
            if (settings == null)
                return;

            System.Type type = settings.GetType();
            foreach (string propertyName in new[] { "colliderUpdateMode", "m_ColliderUpdateMode" })
            {
                System.Reflection.PropertyInfo property = type.GetProperty(
                    propertyName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);
                if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
                    continue;

                try
                {
                    string[] prefer = { "Always", "Dynamic", "Continuous" };
                    for (int i = 0; i < prefer.Length; i++)
                    {
                        try
                        {
                            object value = System.Enum.Parse(property.PropertyType, prefer[i], true);
                            property.SetValue(settings, value);
                            return;
                        }
                        catch (System.ArgumentException)
                        {
                            // try next enum name
                        }
                    }
                }
                catch
                {
                    // ignore unsupported Unity version
                }
            }
        }

        private void ResolveWorldPanelSettings()
        {
            if (worldPanelSettingsSource != null)
                return;

#if UNITY_EDITOR
            worldPanelSettingsSource =
                UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(WorldPanelSettingsPath);
#endif
        }
    }
}
