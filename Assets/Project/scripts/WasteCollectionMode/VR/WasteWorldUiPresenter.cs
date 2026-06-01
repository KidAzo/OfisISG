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
    public sealed class WasteWorldUiPresenter : MonoBehaviour
    {
        private const string WorldPanelSettingsPath =
            "Assets/Project/OfficeFire/UI/InteractHoverWorldPanelSettings.asset";

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private PanelSettings worldPanelSettingsSource;
        [SerializeField] private Transform cameraOverride;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private float distanceInFrontOfCamera = 1.35f;
        [SerializeField] private float billboardYawOffsetDegrees = 180f;
        [SerializeField] private float worldDocumentScale = 0.0018f;

        private PanelSettings runtimePanelSettings;
        private bool configuredForVr;
        private bool followActive;
        private WasteResultScreenController resultScreen;
        private WasteSelectionMenu selectionMenu;

        private void Start()
        {
            resultScreen = GetComponent<WasteResultScreenController>();
            selectionMenu = GetComponent<WasteSelectionMenu>();
        }

        private void Update()
        {
            if (!configuredForVr)
                return;

            followActive = (resultScreen != null && resultScreen.IsVisible) ||
                           (selectionMenu != null && selectionMenu.IsVisible);
        }

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            ResolveWorldPanelSettings();
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

        private IEnumerator ConfigureWhenReady()
        {
            int safety = 120;
            while (safety-- > 0 && enabled && (uiDocument == null || uiDocument.rootVisualElement == null))
                yield return null;

            if (!enabled || uiDocument == null)
                yield break;

            ConfigureWorldDocument();
        }

        private void LateUpdate()
        {
            if (!followActive || !configuredForVr)
                return;

            Transform eye = ResolveFollowEye();
            if (eye == null)
                return;

            Transform t = transform;
            Vector3 pos = eye.position + eye.forward * distanceInFrontOfCamera;
            t.position = pos;

            Vector3 toEye = eye.position - pos;
            if (toEye.sqrMagnitude > 1e-6f)
            {
                Quaternion look = Quaternion.LookRotation(-toEye.normalized, Vector3.up);
                t.rotation = look * Quaternion.Euler(0f, billboardYawOffsetDegrees, 0f);
            }
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
            uiDocument.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Dynamic;
            uiDocument.pivot = Pivot.Center;
            uiDocument.pivotReferenceSize = PivotReferenceSize.BoundingBox;

            transform.localScale = Vector3.one * worldDocumentScale;

            if (GetComponent<ExitPanelNearFarUiBootstrap>() == null)
                gameObject.AddComponent<ExitPanelNearFarUiBootstrap>();

            configuredForVr = true;
        }

        private Transform ResolveFollowEye()
        {
            if (cameraOverride != null)
                return cameraOverride;

            if (ServiceLocator.TryGet(out IXRPlayerService xrPlayer) && xrPlayer?.PlayerCamera != null)
                return xrPlayer.PlayerCamera.transform;

            Camera main = Camera.main;
            return main != null ? main.transform : null;
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
