using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using Woi.UI.Result;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Positions the outdoor / assembly <see cref="OfficeFireResultScreenController"/> UIDocument in world space for XR,
    /// using the same scale and head offset as <see cref="OfficeFireLoginWorldUiPresenter"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(-50)]
    public sealed class OfficeFireResultWorldUiPresenter : MonoBehaviour
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

        [SerializeField, Range(0.05f, 1f)]
        private float worldObjectScale = 0.2f;

        [SerializeField]
        private bool followHeadEachFrame = false;

        [SerializeField]
        private float billboardYawOffsetDegrees = 180f;

        private PanelSettings _runtimePanelSettings;
        private bool _configuredForVr;
        private bool _geometryCallbackRegistered;
        private Coroutine _deferredSnapRoutine;
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
            CancelDeferredSnap();
        }

        private void LateUpdate()
        {
            if (!_configuredForVr || !followHeadEachFrame)
                return;

            SnapInFrontOfEye();
        }

        public void NotifyContentReady()
        {
            if (!_configuredForVr || uiDocument == null)
                return;

            ApplyVrRootLayout();
            RegisterGeometryCallback();
            RepositionInFrontOfPlayer();
            ScheduleColliderRefresh();
        }

        /// <summary>Places the panel once in front of the XR camera (used when followHeadEachFrame is off).</summary>
        public void RepositionInFrontOfPlayer()
        {
            if (!_configuredForVr)
                return;

            SnapInFrontOfEye();
            ScheduleDeferredSnap();
        }

        public void ApplyForCurrentMode()
        {
            if (FirePlatformRuntime.IsVR)
            {
                ConfigureForVr();
                if (_configuredForVr)
                    RepositionInFrontOfPlayer();
            }
            else
            {
                ConfigureForPc();
            }
        }

        private void ConfigureForVr()
        {
            if (_configuredForVr || uiDocument == null)
                return;

            PanelSettings source = ResolveWorldPanelSettingsSource();
            if (source == null)
            {
                Debug.LogError(
                    "[OfficeFireResultWorldUiPresenter] World PanelSettings missing — assign InteractHoverWorldPanelSettings or UIDocument panelSettings.",
                    this);
                return;
            }

            _runtimePanelSettings = Instantiate(source);
            _runtimePanelSettings.name = source.name + " (Result VR Runtime)";
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
            transform.localScale = Vector3.one * worldObjectScale;

            EnsurePanelEventHandler();
            if (GetComponent<ExitPanelNearFarUiBootstrap>() == null)
                gameObject.AddComponent<ExitPanelNearFarUiBootstrap>();

            _configuredForVr = true;
            ApplyVrRootLayout();
            RegisterGeometryCallback();
            RepositionInFrontOfPlayer();
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
            root?.EnableInClassList("result-root--vr-world", false);
        }

        private void ApplyVrRootLayout()
        {
            VisualElement root = uiDocument != null ? uiDocument.rootVisualElement : null;
            if (root == null)
                return;

            root.EnableInClassList("result-root--vr-world", true);
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

            if (transform.parent != null)
                transform.SetParent(null, true);

            Vector3 worldPos = head.TransformPoint(localOffsetFromEye);
            Quaternion worldRot = ComputeBillboardRotation(head, worldPos);
            transform.SetPositionAndRotation(worldPos, worldRot);
            transform.localScale = Vector3.one * worldObjectScale;

            SyncUidocumentWorldTransform();
        }

        private Quaternion ComputeBillboardRotation(Transform eye, Vector3 panelWorldPosition)
        {
            Vector3 toCamera = eye.position - panelWorldPosition;
            toCamera.y = 0f;

            if (toCamera.sqrMagnitude < 1e-6f)
            {
                toCamera = new Vector3(-eye.forward.x, 0f, -eye.forward.z);
            }

            if (toCamera.sqrMagnitude < 1e-6f)
            {
                return Quaternion.identity;
            }

            Quaternion face = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            if (Mathf.Abs(billboardYawOffsetDegrees) > 1e-3f)
            {
                face *= Quaternion.Euler(0f, billboardYawOffsetDegrees, 0f);
            }

            return face;
        }

        private void ScheduleDeferredSnap()
        {
            if (!isActiveAndEnabled)
                return;

            CancelDeferredSnap();
            _deferredSnapRoutine = StartCoroutine(DeferredSnapRoutine());
        }

        private void CancelDeferredSnap()
        {
            if (_deferredSnapRoutine == null)
                return;

            StopCoroutine(_deferredSnapRoutine);
            _deferredSnapRoutine = null;
        }

        private System.Collections.IEnumerator DeferredSnapRoutine()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            if (!_configuredForVr)
                yield break;

            SnapInFrontOfEye();
            ScheduleColliderRefresh();
            _deferredSnapRoutine = null;
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

        private PanelSettings ResolveWorldPanelSettingsSource()
        {
            if (worldPanelSettingsSource != null)
                return worldPanelSettingsSource;

            ResolvePanelSettingsAssets();
            if (worldPanelSettingsSource != null)
                return worldPanelSettingsSource;

            return uiDocument != null ? uiDocument.panelSettings : null;
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

            FieldInfo field = settings.GetType().GetField(
                "m_ColliderUpdateMode",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (field == null || !field.FieldType.IsEnum)
                return;

            try
            {
                object value = System.Enum.Parse(field.FieldType, "Always");
                field.SetValue(settings, value);
            }
            catch
            {
                // Best-effort.
            }
        }
    }
}
