using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using Woi.UI.Result;
using Woi.Player;
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
        [SerializeField] private Transform cameraOverride;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private float distanceInFrontOfCamera = 1.35f;
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
        private bool pendingLayoutRefresh;
        private bool detachedForFollow;
        private Transform parentBeforeDetach;
        private float distanceInFrontOfCameraRuntime;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            distanceInFrontOfCameraRuntime = distanceInFrontOfCamera;
            ResolveWorldPanelSettings();
        }

        public void SetFollowActive(bool active)
        {
            bool wasFollowing = followActive;
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
            distanceInFrontOfCameraRuntime = Mathf.Max(0.25f, distanceMeters);
        }

        /// <summary>Applies <see cref="worldDocumentScale"/> from Inspector to transform — does not overwrite the serialized field.</summary>
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
            if (!WasteCollectionPlatform.IsVR)
            {
                enabled = false;
                return;
            }

            StartCoroutine(ConfigureWhenReady());
        }

        private void OnDisable()
        {
            RestoreParentAfterFollow();
        }

        private IEnumerator ConfigureWhenReady()
        {
            int safety = 120;
            while (safety-- > 0 && enabled && (uiDocument == null || uiDocument.rootVisualElement == null))
                yield return null;

            if (!enabled || uiDocument == null)
                yield break;

            ConfigureWorldDocument();
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

            if (!followActive || !configuredForVr)
                return;

            SnapInFrontOfEye();
        }

        private void SnapInFrontOfEye()
        {
            if (!TryResolveFollowEye(out Transform eye))
                return;

            Transform t = transform;
            Vector3 pos = eye.position + eye.forward * distanceInFrontOfCameraRuntime;
            t.SetPositionAndRotation(pos, ComputeBillboardRotation(eye, pos));
        }

        private Quaternion ComputeBillboardRotation(Transform eye, Vector3 panelWorldPosition)
        {
            Vector3 toEye = eye.position - panelWorldPosition;
            if (toEye.sqrMagnitude < 1e-6f)
                return eye.rotation;

            Quaternion look = Quaternion.LookRotation(-toEye.normalized, eye.up);
            if (Mathf.Abs(billboardYawOffsetDegrees) > 1e-3f)
                look *= Quaternion.Euler(0f, billboardYawOffsetDegrees, 0f);
            return look;
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

            uiDocument.panelSettings = runtimePanelSettings;
            ApplyVrWorldSpaceLayout();
            ApplyWorldSpaceRootLayout();

            if (GetComponent<ExitPanelNearFarUiBootstrap>() == null)
                gameObject.AddComponent<ExitPanelNearFarUiBootstrap>();

            configuredForVr = true;
        }

        /// <summary>
        /// UI Toolkit pixel layout (1920×1080) must be shrunk for world space — never leave scale at 1 in the scene.
        /// </summary>
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
            distanceInFrontOfCameraRuntime = distanceInFrontOfCamera;
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
            VisualElement root = uiDocument.rootVisualElement;
            if (root == null)
                return;

            root.MarkDirtyRepaint();
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

        private bool TryResolveFollowEye(out Transform eye)
        {
            eye = null;

            if (ServiceLocator.TryGet(out IXRPlayerService xrPlayer)
                && xrPlayer?.PlayerCamera != null
                && xrPlayer.PlayerCamera.isActiveAndEnabled)
            {
                eye = xrPlayer.PlayerCamera.transform;
                return true;
            }

            Camera sceneMain = ResolveActiveMainCameraInScene();
            if (sceneMain != null)
            {
                eye = sceneMain.transform;
                return true;
            }

            if (cameraOverride != null && cameraOverride.gameObject.activeInHierarchy)
            {
                Camera overrideCam = cameraOverride.GetComponent<Camera>();
                if (overrideCam != null && overrideCam.isActiveAndEnabled)
                {
                    eye = overrideCam.transform;
                    return true;
                }

                eye = cameraOverride;
                return true;
            }

            return false;
        }

        private static Camera ResolveActiveMainCameraInScene()
        {
            Camera main = Camera.main;
            if (main != null && main.isActiveAndEnabled)
                return main;

            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            Camera best = null;
            float bestDepth = float.MinValue;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (cam == null || !cam.isActiveAndEnabled || !cam.gameObject.scene.IsValid())
                    continue;

                if (cam.CompareTag("MainCamera"))
                    return cam;

                if (cam.depth > bestDepth)
                {
                    bestDepth = cam.depth;
                    best = cam;
                }
            }

            return best;
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
