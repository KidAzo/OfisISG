using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using Woi.UI.Result;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Positions <see cref="OfficeFireLoginScreenController"/>'s UIDocument in world space for XR
    /// (UI Toolkit ray / NearFar interaction). PC keeps screen-space PanelSettings.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(100)]
    public sealed class OfficeFireLoginWorldUiPresenter : MonoBehaviour
    {
        private const string WorldPanelSettingsPath =
            "Assets/Project/OfficeFire/UI/InteractHoverWorldPanelSettings.asset";

        private const string ScreenPanelSettingsPath =
            "Assets/UI Toolkit/PanelSettings.asset";

        [SerializeField]
        private UIDocument uiDocument;

        [SerializeField]
        private PanelSettings worldPanelSettingsSource;

        [SerializeField]
        private PanelSettings screenPanelSettingsSource;

        [SerializeField]
        private Transform xrRigRoot;

        [SerializeField]
        private Vector3 localOffsetFromEye = new(0f, -0.08f, 1.35f);

        [SerializeField]
        private Vector2 fixedWorldPanelPixels = new(1180f, 860f);

        [SerializeField]
        private bool followHeadEachFrame = true;

        [SerializeField]
        private float billboardYawOffsetDegrees = 180f;

        private static readonly int[] ColdStartSnapFrameDelays = { 0, 1, 2, 5, 15, 30, 60, 90, 120 };

        private PanelSettings _runtimePanelSettings;
        private bool _configuredForVr;
        private bool _geometryCallbackRegistered;
        private Coroutine _coldStartVrRoutine;
        private static MethodInfo _uidocumentLateUpdate;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            ResolvePanelSettingsAssets();
            ResolveXrRigRoot();
        }

        private void OnEnable()
        {
            ApplyForCurrentMode();
            BeginColdStartVrSetup();
        }

        private void OnDisable()
        {
            CancelColdStartVrSetup();
            UnregisterGeometryCallback();
        }

        private void LateUpdate()
        {
            if (!_configuredForVr || !ShouldFollowHeadInVr())
                return;

            SnapInFrontOfEye();
        }

        private bool ShouldFollowHeadInVr()
        {
            // Login VR always tracks the active headset — scene may still serialize followHeadEachFrame off.
            return followHeadEachFrame || FirePlatformRuntime.IsVR;
        }

        /// <summary>Call after UXML binds so VR layout/collider refresh runs.</summary>
        public void NotifyContentReady()
        {
            if (FirePlatformRuntime.IsVR && !_configuredForVr)
                ApplyForCurrentMode();

            if (!_configuredForVr || uiDocument == null)
                return;

            ApplyVrRootLayout();
            RegisterGeometryCallback();
            RepositionInFrontOfPlayer();
        }

        public void ApplyForCurrentMode()
        {
            if (FirePlatformRuntime.IsVR)
                ConfigureForVr();
            else
                ConfigureForPc();
        }

        private void ConfigureForVr()
        {
            if (_configuredForVr || uiDocument == null)
                return;

            ResolvePanelSettingsAssets();
            if (worldPanelSettingsSource == null)
            {
                Debug.LogError(
                    "[OfficeFireLoginWorldUiPresenter] World PanelSettings missing — assign InteractHoverWorldPanelSettings.",
                    this);
                return;
            }

            _runtimePanelSettings = Instantiate(worldPanelSettingsSource);
            _runtimePanelSettings.name = worldPanelSettingsSource.name + " (Login VR Runtime)";
            _runtimePanelSettings.renderMode = PanelRenderMode.WorldSpace;
            TrySetColliderUpdateAlways(_runtimePanelSettings);

            uiDocument.panelSettings = _runtimePanelSettings;
            uiDocument.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            uiDocument.worldSpaceSize = new Vector2(
                Mathf.Max(640f, fixedWorldPanelPixels.x),
                Mathf.Max(480f, fixedWorldPanelPixels.y));
            uiDocument.pivot = Pivot.Center;
            uiDocument.pivotReferenceSize = PivotReferenceSize.BoundingBox;

            transform.localRotation = Quaternion.identity;

            EnsurePanelEventHandler();
            if (GetComponent<ExitPanelNearFarUiBootstrap>() == null)
                gameObject.AddComponent<ExitPanelNearFarUiBootstrap>();

            _configuredForVr = true;
            ApplyVrRootLayout();
            RegisterGeometryCallback();
            RepositionInFrontOfPlayer();
        }

        private void ConfigureForPc()
        {
            if (_configuredForVr)
            {
                UnregisterGeometryCallback();
                _configuredForVr = false;
            }

            if (uiDocument == null)
                return;

            ResolvePanelSettingsAssets();
            if (screenPanelSettingsSource != null)
                uiDocument.panelSettings = screenPanelSettingsSource;

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            VisualElement root = uiDocument.rootVisualElement;
            root?.EnableInClassList("login-root--vr-world", false);
        }

        private void ApplyVrRootLayout()
        {
            VisualElement root = uiDocument != null ? uiDocument.rootVisualElement : null;
            if (root == null)
                return;

            root.EnableInClassList("login-root--vr-world", true);
            root.style.flexGrow = 0;
            root.style.width = fixedWorldPanelPixels.x;
            root.style.height = fixedWorldPanelPixels.y;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
        }

        /// <summary>Places the panel once in front of the XR camera (used when followHeadEachFrame is off).</summary>
        public void RepositionInFrontOfPlayer()
        {
            if (!_configuredForVr)
                return;

            SnapInFrontOfEye();
        }

        private void BeginColdStartVrSetup()
        {
            if (!isActiveAndEnabled)
                return;

            CancelColdStartVrSetup();
            _coldStartVrRoutine = StartCoroutine(ColdStartVrSetupRoutine());
        }

        private void CancelColdStartVrSetup()
        {
            if (_coldStartVrRoutine == null)
                return;

            StopCoroutine(_coldStartVrRoutine);
            _coldStartVrRoutine = null;
        }

        private IEnumerator ColdStartVrSetupRoutine()
        {
            int previousDelay = 0;
            for (int i = 0; i < ColdStartSnapFrameDelays.Length; i++)
            {
                int extraFrames = ColdStartSnapFrameDelays[i] - previousDelay;
                for (int f = 0; f < extraFrames; f++)
                    yield return null;

                previousDelay = ColdStartSnapFrameDelays[i];

                if (!isActiveAndEnabled)
                    yield break;

                if (FirePlatformRuntime.IsVR)
                {
                    ResolveXrRigRoot();
                    if (!_configuredForVr)
                        ConfigureForVr();
                }

                if (!_configuredForVr)
                    continue;

                RepositionInFrontOfPlayer();
            }

            if (_configuredForVr)
                RepositionInFrontOfPlayer();

            _coldStartVrRoutine = null;
        }

        private void SnapInFrontOfEye()
        {
            if (!TryResolveHeadTransform(out Transform head))
                return;

            if (transform.parent != null)
                transform.SetParent(null, true);

            Vector3 worldPos = head.TransformPoint(localOffsetFromEye);
            Quaternion worldRot = ComputeBillboardRotation(head, worldPos);
            transform.SetPositionAndRotation(worldPos, worldRot);

            SyncUidocumentWorldTransform();
        }

        private Quaternion ComputeBillboardRotation(Transform eye, Vector3 panelWorldPosition)
        {
            Vector3 toCamera = eye.position - panelWorldPosition;
            toCamera.y = 0f;

            if (toCamera.sqrMagnitude < 1e-6f)
                toCamera = new Vector3(-eye.forward.x, 0f, -eye.forward.z);

            if (toCamera.sqrMagnitude < 1e-6f)
                return Quaternion.identity;

            Quaternion face = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            if (Mathf.Abs(billboardYawOffsetDegrees) > 1e-3f)
                face *= Quaternion.Euler(0f, billboardYawOffsetDegrees, 0f);

            return face;
        }

        private bool TryResolveHeadTransform(out Transform head)
        {
            head = ResolveHeadTransform();
            return head != null;
        }

        private Transform ResolveHeadTransform()
        {
            if (xrRigRoot == null || !IsUsableRigRoot(xrRigRoot))
                ResolveXrRigRoot(forceRefresh: true);

            if (xrRigRoot != null)
            {
                Camera rigCamera = xrRigRoot.GetComponentInChildren<Camera>(true);
                if (rigCamera != null && rigCamera.isActiveAndEnabled)
                    return rigCamera.transform;
            }

            Camera main = Camera.main;
            if (main != null && main.isActiveAndEnabled)
                return main.transform;

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                if (!candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded)
                    continue;

                if (candidate.CompareTag("MainCamera"))
                    return candidate.transform;
            }

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate != null && candidate.isActiveAndEnabled
                    && candidate.gameObject.scene.IsValid()
                    && candidate.gameObject.scene.isLoaded)
                {
                    return candidate.transform;
                }
            }

            return null;
        }

        private static bool IsUsableRigRoot(Transform rigRoot)
        {
            if (rigRoot == null || !rigRoot.gameObject.activeInHierarchy)
                return false;

            if (!rigRoot.gameObject.scene.IsValid() || !rigRoot.gameObject.scene.isLoaded)
                return false;

            Camera camera = rigRoot.GetComponentInChildren<Camera>(true);
            return camera != null && camera.isActiveAndEnabled;
        }

        private void ResolveXrRigRoot()
        {
            ResolveXrRigRoot(forceRefresh: false);
        }

        private void ResolveXrRigRoot(bool forceRefresh)
        {
            if (!forceRefresh && IsUsableRigRoot(xrRigRoot))
                return;

            xrRigRoot = null;

            System.Type originType = System.Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (originType == null)
                return;

            Object[] found = Resources.FindObjectsOfTypeAll(originType);
            Transform bestRoot = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] is not Component origin || origin == null)
                    continue;

                Transform candidate = origin.transform;
                if (!IsUsableRigRoot(candidate))
                    continue;

                int score = 0;
                if (candidate.gameObject.activeInHierarchy)
                    score += 40;

                Camera camera = candidate.GetComponentInChildren<Camera>(true);
                if (camera != null && camera.isActiveAndEnabled)
                    score += 40;

                if (camera != null && camera.CompareTag("MainCamera"))
                    score += 25;

                string rigName = candidate.name;
                if (rigName.IndexOf("XR Origin", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || rigName.IndexOf("XR Rig", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 15;
                }

                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestRoot = candidate;
            }

            xrRigRoot = bestRoot;
        }

        private void ResolvePanelSettingsAssets()
        {
#if UNITY_EDITOR
            if (worldPanelSettingsSource == null)
            {
                worldPanelSettingsSource = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(WorldPanelSettingsPath);
            }

            if (screenPanelSettingsSource == null)
            {
                screenPanelSettingsSource = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(ScreenPanelSettingsPath);
            }
#endif
        }

        private void EnsurePanelEventHandler()
        {
            if (GetComponent<PanelEventHandler>() == null)
                gameObject.AddComponent<PanelEventHandler>();
        }

        private void ScheduleColliderRefresh()
        {
            SyncUidocumentWorldTransform();
        }

        private void RegisterGeometryCallback()
        {
            if (_geometryCallbackRegistered || uiDocument == null)
                return;

            VisualElement root = uiDocument.rootVisualElement;
            if (root == null)
                return;

            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            _geometryCallbackRegistered = true;
        }

        private void UnregisterGeometryCallback()
        {
            if (!_geometryCallbackRegistered || uiDocument == null)
                return;

            VisualElement root = uiDocument.rootVisualElement;
            root?.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            _geometryCallbackRegistered = false;
        }

        private void OnRootGeometryChanged(GeometryChangedEvent _)
        {
            if (!_configuredForVr)
                return;

            SyncUidocumentWorldTransform();
        }

        private void SyncUidocumentWorldTransform()
        {
            if (uiDocument == null)
                return;

            TryInvokeUidocumentLateUpdate(uiDocument);
            uiDocument.rootVisualElement?.MarkDirtyRepaint();
        }

        private static void TryInvokeUidocumentLateUpdate(UIDocument document)
        {
            if (document == null)
                return;

            _uidocumentLateUpdate ??= typeof(UIDocument).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);

            _uidocumentLateUpdate?.Invoke(document, null);
        }

        private static void TrySetColliderUpdateAlways(PanelSettings settings)
        {
            if (settings == null)
                return;

            SerializedFieldHelper.TrySetEnumByName(settings, "m_ColliderUpdateMode", "Always");
        }

        private static class SerializedFieldHelper
        {
            public static void TrySetEnumByName(object target, string fieldName, string enumValueName)
            {
                FieldInfo field = target.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (field == null || !field.FieldType.IsEnum)
                    return;

                try
                {
                    object value = System.Enum.Parse(field.FieldType, enumValueName);
                    field.SetValue(target, value);
                }
                catch
                {
                    // Ignore — collider update mode is best-effort.
                }
            }
        }
    }
}
