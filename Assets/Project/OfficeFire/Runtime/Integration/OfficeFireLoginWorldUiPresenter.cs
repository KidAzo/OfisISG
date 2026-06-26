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
    [DefaultExecutionOrder(-50)]
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

        private PanelSettings _runtimePanelSettings;
        private bool _configuredForVr;
        private bool _geometryCallbackRegistered;
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
        }

        private void OnDisable()
        {
            UnregisterGeometryCallback();
        }

        private void LateUpdate()
        {
            if (!_configuredForVr || !followHeadEachFrame)
                return;

            SnapInFrontOfEye();
        }

        /// <summary>Call after UXML binds so VR layout/collider refresh runs.</summary>
        public void NotifyContentReady()
        {
            if (!_configuredForVr || uiDocument == null)
                return;

            ApplyVrRootLayout();
            RegisterGeometryCallback();
            ScheduleColliderRefresh();
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
            SnapInFrontOfEye();
            ScheduleColliderRefresh();
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

        private void SnapInFrontOfEye()
        {
            Transform head = ResolveHeadTransform();
            if (head == null)
                return;

            Vector3 worldPos = head.TransformPoint(localOffsetFromEye);
            transform.position = worldPos;
            transform.localRotation = Quaternion.identity;

            SyncUidocumentWorldTransform();
        }

        private Transform ResolveHeadTransform()
        {
            if (xrRigRoot != null)
            {
                Camera rigCamera = xrRigRoot.GetComponentInChildren<Camera>(true);
                if (rigCamera != null)
                    return rigCamera.transform;
            }

            Camera main = Camera.main;
            return main != null ? main.transform : null;
        }

        private void ResolveXrRigRoot()
        {
            if (xrRigRoot != null)
                return;

            System.Type originType = System.Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (originType == null)
                return;

            Object[] found = Resources.FindObjectsOfTypeAll(originType);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] is not Component origin || origin == null)
                    continue;

                GameObject go = origin.gameObject;
                if (!go.scene.IsValid() || !go.scene.isLoaded)
                    continue;

                xrRigRoot = go.transform;
                return;
            }
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
