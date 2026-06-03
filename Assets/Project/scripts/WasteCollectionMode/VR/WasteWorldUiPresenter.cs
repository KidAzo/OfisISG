using System.Collections;

using System.Reflection;

using UnityEngine;

using UnityEngine.Serialization;

using UnityEngine.UIElements;

using Woi.UI.Result;

using WOI.Modules.SDK;



namespace Woi.WasteCollectionMode

{

    /// <summary>

    /// VR: positions the shared WasteCollection UIDocument in world space in front of the HMD.

    /// Keeps <see cref="WasteCollectionUI"/> under its scene parent — never reparents to the camera.

    /// </summary>

    [DisallowMultipleComponent]

    [RequireComponent(typeof(UIDocument))]

    [DefaultExecutionOrder(-50)]

    public sealed class WasteWorldUiPresenter : MonoBehaviour

    {

        private const string WorldPanelSettingsPath =

            "Assets/Project/OfficeFire/UI/InteractHoverWorldPanelSettings.asset";

        private const string ScreenPanelSettingsPath =

            "Assets/UI Toolkit/PanelSettings.asset";



        [SerializeField] private UIDocument uiDocument;

        [SerializeField] private PanelSettings worldPanelSettingsSource;

        [SerializeField] private Transform xrRigRoot;

        [SerializeField] private Transform cameraOverride;

        [FormerlySerializedAs("distanceInFrontOfCamera")]

        [SerializeField] private Vector3 localOffsetFromEye = new(0f, 0f, 1.35f);

        [SerializeField] private float billboardYawOffsetDegrees;

        [Tooltip("Pixel UI → world metres. Try 0.004–0.008 if UI looks invisible in headset.")]

        [SerializeField] private float worldDocumentScale = 0.005f;



        [Header("World panel size (pixels, before scale)")]

        [SerializeField] private bool useFixedWorldPanelSize = true;

        [SerializeField] private Vector2 fixedWorldPanelPixels = new(960f, 820f);



        private PanelSettings runtimePanelSettings;

        private bool configuredForVr;

        private bool followActive;

        private bool pendingFollowRequest;

        private int layoutRefreshFramesRemaining;

        private bool geometryCallbackRegistered;

        private Vector3 localOffsetFromEyeRuntime;

        private bool warnedNoHeadCamera;

        private static MethodInfo s_uidocumentLateUpdate;



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

                ScheduleLayoutRefresh();

                SnapInFrontOfEye();

            }

        }



        public void SetUiDistance(float distanceMeters)

        {

            float distance = Mathf.Max(0.25f, distanceMeters);

            localOffsetFromEyeRuntime = new Vector3(0f, 0f, distance);

        }



        /// <summary>

        /// Call when modal overlay content changes (exit → result, table rebuild, etc.) so the

        /// world-space pick mesh/collider matches visible buttons for XR UI clicks.

        /// </summary>

        public void NotifyContentLayoutChanged()

        {

            if (!configuredForVr || uiDocument == null)

                return;



            ScheduleLayoutRefresh();

            if (followActive)

                SyncUidocumentWorldTransform();

        }



        public void ApplyLayoutFromInspector()

        {

            if (uiDocument == null)

                return;



            if (!configuredForVr && Application.isPlaying)

                return;



            ApplyVrWorldSpaceLayout();



            if (uiDocument.rootVisualElement != null)

            {

                ApplyWorldSpaceRootLayout();

                RegisterGeometryCallback();

                RefreshPanelAfterLayout();

            }

        }



        private void OnEnable()

        {

            StartCoroutine(BootstrapVrWhenReady());

        }



        private void OnDisable()

        {

            followActive = false;

            UnregisterGeometryCallback();

        }



        // No per-frame follow: the panel is placed once when it opens (see LateUpdate's

        // settle window) and then stays fixed in world space — it does not track the head.



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



        /// <summary>Waits for UI Toolkit root; VR world panel is configured only when follow/modal opens.</summary>

        private IEnumerator ConfigureWhenReady()

        {

            int safety = 120;

            while (safety-- > 0 && enabled && (uiDocument == null || uiDocument.rootVisualElement == null))

                yield return null;



            if (!enabled || uiDocument == null)

                yield break;



            if (pendingFollowRequest)

                SetFollowActive(true);

        }



        private void LateUpdate()

        {

            // Settle window after opening: place the panel in front of the eye and refresh

            // its world mesh for a few frames (head pose can still be moving on the exact

            // open frame), then freeze. After this there is no head-following.

            if (layoutRefreshFramesRemaining > 0 && configuredForVr)

            {

                layoutRefreshFramesRemaining--;

                SnapInFrontOfEye();

                ApplyLayoutFromInspector();

                SyncUidocumentWorldTransform();

            }

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

        }



        private Quaternion ComputeBillboardRotation(Transform eye, Vector3 panelWorldPosition)

        {

            Vector3 toCamera = eye.position - panelWorldPosition;

            // Flatten to the horizontal plane so the panel only yaws (Y). This keeps it

            // vertically upright — X (pitch) and Z (roll) stay 0 even when looking up/down.

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



            float scale = Mathf.Clamp(worldDocumentScale, 0.0005f, 0.2f);

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

            // Fixed height (not Auto) so the root fills the fixed world panel and the mesh

            // size is stable from the first frame instead of growing with content.

            root.style.height = useFixedWorldPanelSize

                ? new StyleLength(fixedWorldPanelPixels.y)

                : new StyleLength(StyleKeyword.Auto);

            root.style.alignItems = Align.Center;

            root.style.justifyContent = Justify.Center;

        }



        private void RefreshPanelAfterLayout()

        {

            SyncUidocumentWorldTransform();

        }



        /// <summary>

        /// Re-sync the world panel for a few frames after it opens. UI Toolkit content

        /// (grid/labels) only resolves its layout one or two frames after display flips to

        /// Flex, so a single refresh runs against a stale bounding box and the world mesh

        /// looks broken until something forces a rebuild.

        /// </summary>

        private void ScheduleLayoutRefresh()

        {

            layoutRefreshFramesRemaining = 4;

        }



        private void RegisterGeometryCallback()

        {

            if (geometryCallbackRegistered || uiDocument == null)

                return;



            VisualElement root = uiDocument.rootVisualElement;

            if (root == null)

                return;



            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            geometryCallbackRegistered = true;

        }



        private void UnregisterGeometryCallback()

        {

            if (!geometryCallbackRegistered || uiDocument == null)

                return;



            VisualElement root = uiDocument.rootVisualElement;

            root?.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            geometryCallbackRegistered = false;

        }



        private void OnRootGeometryChanged(GeometryChangedEvent evt)

        {

            if (!configuredForVr || !followActive)

                return;



            // Content just (re)laid out — regenerate the world-space mesh against the

            // resolved bounding box and repaint so the panel is no longer stale.

            SyncUidocumentWorldTransform();

        }



        /// <summary>

        /// UIDocument copies transform → root style in its own LateUpdate. Re-run after we move the transform.

        /// </summary>

        private void SyncUidocumentWorldTransform()

        {

            if (uiDocument == null)

                return;



            TryInvokeUidocumentLateUpdate(uiDocument);



            VisualElement root = uiDocument.rootVisualElement;

            if (root == null)

                return;



            root.MarkDirtyRepaint();

        }



        private static void TryInvokeUidocumentLateUpdate(UIDocument document)

        {

            if (document == null)

                return;



            s_uidocumentLateUpdate ??= typeof(UIDocument).GetMethod(

                "LateUpdate",

                BindingFlags.Instance | BindingFlags.NonPublic);



            s_uidocumentLateUpdate?.Invoke(document, null);

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

                // Keep the shared document on the SCREEN panel in edit mode so PC renders correctly.
                // VR applies its own world panel at runtime (ConfigureForVr); use the
                // "Apply VR World Space Layout (Editor Preview)" context menu for a manual world preview.
                PanelSettings screenPanel =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(ScreenPanelSettingsPath);

                if (screenPanel != null)
                    uiDocument.panelSettings = screenPanel;

                return;

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



            bool wasConfigured = configuredForVr;

            configuredForVr = true;

            ApplyLayoutFromInspector();

            configuredForVr = wasConfigured;



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


