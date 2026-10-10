using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using Woi.Events;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// VR: shows the waste counter as an always-visible, head-locked HUD in the top-right of
    /// the view. Independent of the shared selection-menu world panel (which only appears while
    /// a modal is open), so the counter no longer opens/closes with the menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteVrCounterHud : MonoBehaviour
    {
        private const string WorldPanelSettingsPath =
            "Assets/Project/OfficeFire/UI/InteractHoverWorldPanelSettings.asset";

        private const string CounterIconPath =
            "Assets/Project/WasteCollection/UI/IconsPng/trash-2.png";

        private static readonly Color IconTint = new(0f, 1f, 0.698f, 1f);

        [Header("Sources")]
        [SerializeField] private PanelSettings worldPanelSettingsSource;
        [SerializeField] private Texture2D counterIcon;
        [SerializeField] private Transform xrRigRoot;

        [Header("Placement (head-locked, in metres)")]
        [Tooltip("Local offset from the eye: +x right, +y up, +z forward.")]
        [SerializeField] private Vector3 localOffsetFromEye = new(0.26f, 0.18f, 0.7f);

        [Tooltip("Pixel UI → world metres for the HUD panel.")]
        [SerializeField] private float worldDocumentScale = 0.05f;

        [SerializeField] private Vector2 fixedWorldPanelPixels = new(360f, 140f);

        private UIDocument hudDocument;
        private PanelSettings runtimePanelSettings;
        private Label counterLabel;
        private Image counterIconImage;
        private GameObject hudRoot;

        private int totalCount;
        private int collectedCount;
        private bool built;

        private void Awake()
        {
            ResolveXrRigRoot();
            ResolveWorldPanelSettings();
            ResolveCounterIcon();
            totalCount = CountSceneWastes();
        }

        private void OnEnable()
        {
            EventBus.Register<WasteCollectedEvent>(OnWasteCollected);

            if (WasteCollectTracker.TryGetActive(out WasteCollectTracker tracker))
                collectedCount = tracker.Records.Count;

            StartCoroutine(BootstrapWhenReady());
        }

        private void OnDisable()
        {
            EventBus.Deregister<WasteCollectedEvent>(OnWasteCollected);
        }

        private void OnDestroy()
        {
            if (hudRoot != null)
                Destroy(hudRoot);

            if (runtimePanelSettings != null)
                Destroy(runtimePanelSettings);
        }

        private void LateUpdate()
        {
            if (!built || hudRoot == null)
                return;

            if (!WasteVrHeadCameraResolver.TryGetHeadCamera(xrRigRoot, out Camera head))
                return;

            Transform eye = head.transform;
            Vector3 pos = eye.TransformPoint(localOffsetFromEye);

            // Yaw-only billboard: keep the panel upright (X and Z rotation = 0) so it faces the
            // viewer without tilting/flipping — same approach as the menu.
            Vector3 toCamera = eye.position - pos;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 1e-6f)
                toCamera = new Vector3(-eye.forward.x, 0f, -eye.forward.z);

            Quaternion rot = toCamera.sqrMagnitude < 1e-6f
                ? Quaternion.identity
                : Quaternion.LookRotation(toCamera.normalized, Vector3.up);

            // Panel front faces the opposite side; flip 180° around Y so it reads toward the viewer.
            rot *= Quaternion.Euler(0f, 180f, 0f);

            hudRoot.transform.SetPositionAndRotation(pos, rot);
        }

        private IEnumerator BootstrapWhenReady()
        {
            while (enabled && !WasteCollectionPlatform.ShouldUseVrPresentation())
            {
                if (FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsPC)
                    yield break;

                yield return null;
            }

            if (!enabled || !WasteCollectionPlatform.ShouldUseVrPresentation())
                yield break;

            HideSharedMenuCounter();
            BuildHud();

            int safety = 120;
            while (safety-- > 0 && (hudDocument == null || hudDocument.rootVisualElement == null))
                yield return null;

            if (hudDocument == null || hudDocument.rootVisualElement == null)
                yield break;

            BuildHudVisualTree();
            RefreshLabel();
        }

        /// <summary>The shared selection-menu document also contains a counter HUD; hide it in VR so
        /// it does not show a second counter inside the menu panel.</summary>
        private void HideSharedMenuCounter()
        {
            UIDocument sharedDocument = GetComponent<UIDocument>();
            VisualElement sharedRoot = sharedDocument != null ? sharedDocument.rootVisualElement : null;
            VisualElement sharedHud = sharedRoot?.Q<VisualElement>("WasteCounterHud");
            if (sharedHud != null)
                sharedHud.style.display = DisplayStyle.None;
        }

        private void BuildHud()
        {
            if (built)
                return;

            if (worldPanelSettingsSource == null)
            {
                Debug.LogError("[WasteVrCounterHud] World PanelSettings not assigned.", this);
                return;
            }

            runtimePanelSettings = Instantiate(worldPanelSettingsSource);
            runtimePanelSettings.name = worldPanelSettingsSource.name + " (Waste VR Counter)";
            runtimePanelSettings.renderMode = PanelRenderMode.WorldSpace;

            hudRoot = new GameObject("WasteVrCounterHud (runtime)");
            hudDocument = hudRoot.AddComponent<UIDocument>();
            hudDocument.panelSettings = runtimePanelSettings;

            hudDocument.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            hudDocument.worldSpaceSize = new Vector2(
                Mathf.Max(120f, fixedWorldPanelPixels.x),
                Mathf.Max(60f, fixedWorldPanelPixels.y));
            hudDocument.pivot = Pivot.Center;
            hudDocument.pivotReferenceSize = PivotReferenceSize.BoundingBox;

            float scale = Mathf.Clamp(worldDocumentScale, 0.0005f, 0.2f);
            hudRoot.transform.localScale = Vector3.one * scale;

            built = true;
        }

        private void BuildHudVisualTree()
        {
            VisualElement root = hudDocument.rootVisualElement;
            if (root == null)
                return;

            root.Clear();
            root.style.flexGrow = 0;
            root.style.width = fixedWorldPanelPixels.x;
            root.style.height = fixedWorldPanelPixels.y;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            var hud = new VisualElement();
            hud.style.flexDirection = FlexDirection.Row;
            hud.style.alignItems = Align.Center;
            hud.style.paddingLeft = 18;
            hud.style.paddingRight = 22;
            hud.style.paddingTop = 14;
            hud.style.paddingBottom = 14;
            hud.style.backgroundColor = new Color(0.067f, 0.086f, 0.137f, 0.92f);
            SetBorderWidth(hud, 1f);
            SetBorderColor(hud, new Color(0.122f, 0.161f, 0.216f, 1f));
            SetBorderRadius(hud, 16f);

            counterIconImage = new Image();
            counterIconImage.style.width = 28;
            counterIconImage.style.height = 28;
            counterIconImage.style.flexShrink = 0;
            counterIconImage.style.marginRight = 12;
            counterIconImage.scaleMode = ScaleMode.ScaleToFit;
            if (counterIcon != null)
            {
                counterIconImage.image = counterIcon;
                counterIconImage.tintColor = IconTint;
            }

            counterLabel = new Label("0/0");
            counterLabel.style.fontSize = 26;
            counterLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            counterLabel.style.color = Color.white;
            counterLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            hud.Add(counterIconImage);
            hud.Add(counterLabel);
            root.Add(hud);
        }

        private static void SetBorderWidth(VisualElement ve, float w)
        {
            ve.style.borderTopWidth = w;
            ve.style.borderBottomWidth = w;
            ve.style.borderLeftWidth = w;
            ve.style.borderRightWidth = w;
        }

        private static void SetBorderColor(VisualElement ve, Color c)
        {
            ve.style.borderTopColor = c;
            ve.style.borderBottomColor = c;
            ve.style.borderLeftColor = c;
            ve.style.borderRightColor = c;
        }

        private static void SetBorderRadius(VisualElement ve, float r)
        {
            ve.style.borderTopLeftRadius = r;
            ve.style.borderTopRightRadius = r;
            ve.style.borderBottomLeftRadius = r;
            ve.style.borderBottomRightRadius = r;
        }

        private void OnWasteCollected(WasteCollectedEvent evt)
        {
            collectedCount = evt.TotalCollected;
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (counterLabel != null)
                counterLabel.text = $"{collectedCount}/{totalCount}";
        }

        private void ResolveXrRigRoot()
        {
            if (xrRigRoot != null)
                return;

            WasteVrLocomotionGate gate = GetComponent<WasteVrLocomotionGate>();
            if (gate != null && gate.XrRigRoot != null)
                xrRigRoot = gate.XrRigRoot;
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

        private void ResolveCounterIcon()
        {
#if UNITY_EDITOR
            if (counterIcon == null)
                counterIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(CounterIconPath);
#endif
        }

        private static int CountSceneWastes()
        {
            WasteController[] wastes = FindObjectsByType<WasteController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            return wastes != null ? wastes.Length : 0;
        }
    }
}
