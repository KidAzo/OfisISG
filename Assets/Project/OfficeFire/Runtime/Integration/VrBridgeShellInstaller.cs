using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Woi.Game.Training.VrBridge;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Connection;
using Woi.VrBridge.Client.Protocol;
using Woi.VrBridge.Client.UI;
using Woi.VrBridge.Client.UnityLifecycle;

namespace Woi.OfficeFire.VrBridge
{
    /// <summary>
    /// Single application-level owner for Protocol V2 Bridge client, UI, launcher, and Fire adapter.
    /// Place once on FireModule_Bootstrapper (or any first scene). Survives scene changes.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    [DisallowMultipleComponent]
    public sealed class VrBridgeShellInstaller : MonoBehaviour
    {
        public const string RootName = "VrBridgeRoot";

        [SerializeField] BridgeClientConfig _config;
        [SerializeField] bool _connectOnStart = true;
        [SerializeField] bool _buildUiIfMissing = true;
        [SerializeField] string _gameplaySceneGroup = "FireModule_Office";
        [SerializeField] string _moduleId = "fire-training";
        [Tooltip("Editor/development only. Never used on Quest player builds.")]
        [SerializeField] string _editorGatewayOverride = "ws://127.0.0.1:17881/ws/device";

        static VrBridgeShellInstaller _instance;

        VrBridgeClientBootstrap _bootstrap;
        VrBridgeClient _client;
        VrBridgeLifecycleHost _lifecycle;
        BridgeModuleLauncherHost _launcherHost;
        OfficeFireBridgeModuleLauncher _moduleLauncher;
        FireBridgeSessionAdapter _fireAdapter;
        VrBridgePairingPanel _pairingPanel;
        VrBridgeDiagnosticsPanel _diagnosticsPanel;
        AuthorizedTrainingPanel _authorizedTrainingPanel;

        public static VrBridgeShellInstaller Instance => _instance;
        public VrBridgeClient Client => _client;
        public BridgeClientConfig Config => _config;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[VRBridge] Duplicate VrBridgeShellInstaller destroyed. One client only.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            gameObject.name = RootName;

            if (_config == null)
            {
                Debug.LogError("[VRBridge] BridgeClientConfig is not assigned on VrBridgeShellInstaller.");
                enabled = false;
                return;
            }

            ApplyEditorDevelopmentEndpoint();
            LogNetworkMode();
            EnsureComponents();
            WireReferences();
            if (_buildUiIfMissing)
            {
                EnsureUi();
            }

            // Phase 2D: authorization only binds the Fire adapter and shows the Start Training UI.
            // LaunchSessionAsync is only ever called from AuthorizedTrainingPanel → StartAuthorizedTrainingAsync.
            _client.AuthorizationReady += OnAuthorizationReady;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void Start()
        {
            if (_client == null || !_connectOnStart)
            {
                return;
            }

            // Wait one frame so FireServiceInstaller / localization can register.
            DelayedConnectAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_client != null)
            {
                _client.AuthorizationReady -= OnAuthorizationReady;
            }

            if (_instance == this)
            {
                if (_client != null)
                {
                    VrBridgeRuntime.Clear(_client);
                }

                _instance = null;
            }
        }

        /// <summary>
        /// Phase 2D: session.authorize.command only prepares participant context and shows the
        /// Start Training overlay. It must NEVER call OfficeFireBridgeModuleLauncher.LaunchSessionAsync —
        /// that only happens from AuthorizedTrainingPanel after the player presses Start Training.
        /// </summary>
        void OnAuthorizationReady(BridgeSessionContext context)
        {
            if (context == null)
            {
                return;
            }

            _fireAdapter?.TryResolveRecorder();
            _fireAdapter?.Bind(_client, context);
            Debug.Log($"[VRBridge] Authorization ready sessionId={context.SessionId} moduleId={context.ModuleId} — waiting for player to press Start Training.");
        }

        async UniTaskVoid DelayedConnectAsync(CancellationToken cancellationToken)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            if (_bootstrap == null || _client == null)
            {
                return;
            }

            // Bootstrap may already connectOnStart — disable duplicate by driving from here.
            await _client.InitializeAsync(cancellationToken);
            await _client.ConnectAsync(cancellationToken);
            Debug.Log($"[VRBridge] Client started. deviceIdShort={ShortId(_client.DeviceId)} state={_client.State}");
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_fireAdapter == null)
            {
                return;
            }

            _fireAdapter.TryResolveRecorder();
            if (_client != null)
            {
                _fireAdapter.EnsureBoundClient(_client);
            }
        }

        void EnsureComponents()
        {
            _client = GetComponent<VrBridgeClient>() ?? gameObject.AddComponent<VrBridgeClient>();
            _bootstrap = GetComponent<VrBridgeClientBootstrap>() ?? gameObject.AddComponent<VrBridgeClientBootstrap>();
            _lifecycle = GetComponent<VrBridgeLifecycleHost>() ?? gameObject.AddComponent<VrBridgeLifecycleHost>();
            _launcherHost = GetComponent<BridgeModuleLauncherHost>() ?? gameObject.AddComponent<BridgeModuleLauncherHost>();
            _moduleLauncher = GetComponent<OfficeFireBridgeModuleLauncher>() ?? gameObject.AddComponent<OfficeFireBridgeModuleLauncher>();
            _fireAdapter = GetComponent<FireBridgeSessionAdapter>() ?? gameObject.AddComponent<FireBridgeSessionAdapter>();
        }

        void WireReferences()
        {
            // Bootstrap: assign via SerializedObject-like reflection-free public API where possible.
            SetPrivateField(_bootstrap, "_config", _config);
            SetPrivateField(_bootstrap, "_moduleLauncherHost", _launcherHost);
            SetPrivateField(_bootstrap, "_connectOnStart", false); // Shell owns connect timing.

            SetPrivateField(_lifecycle, "_client", _client);

            SetPrivateField(_moduleLauncher, "_bridgeClient", _client);
            SetPrivateField(_moduleLauncher, "_host", _launcherHost);
            SetPrivateField(_moduleLauncher, "_fireAdapter", _fireAdapter);
            SetPrivateField(_moduleLauncher, "_moduleId", _moduleId);
            SetPrivateField(_moduleLauncher, "_gameplaySceneGroup", _gameplaySceneGroup);

            SetPrivateField(_fireAdapter, "_bridgeClient", _client);
            SetPrivateField(_fireAdapter, "_moduleId", _moduleId);

            _launcherHost.Launcher = _moduleLauncher;
            _client.Initialize(_config, moduleLauncher: _moduleLauncher);
            VrBridgeRuntime.Register(_client);
        }

        void EnsureUi()
        {
            var canvasGo = transform.Find("BridgeUiCanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("BridgeUiCanvas").transform;
                canvasGo.SetParent(transform, false);
                var canvas = canvasGo.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5000;
                canvasGo.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.gameObject.AddComponent<GraphicRaycaster>();
            }

            _pairingPanel = canvasGo.GetComponentInChildren<VrBridgePairingPanel>(true);
            if (_pairingPanel == null)
            {
                _pairingPanel = BuildPairingPanel(canvasGo);
            }

            _diagnosticsPanel = canvasGo.GetComponentInChildren<VrBridgeDiagnosticsPanel>(true);
            if (_diagnosticsPanel == null)
            {
                _diagnosticsPanel = BuildDiagnosticsPanel(canvasGo);
            }

            _authorizedTrainingPanel = canvasGo.GetComponentInChildren<AuthorizedTrainingPanel>(true);
            if (_authorizedTrainingPanel == null)
            {
                _authorizedTrainingPanel = BuildAuthorizedTrainingPanel(canvasGo);
            }

            SetPrivateField(_pairingPanel, "_client", _client);
            SetPrivateField(_pairingPanel, "_config", _config);
            SetPrivateField(_diagnosticsPanel, "_client", _client);
            SetPrivateField(_diagnosticsPanel, "_config", _config);
            SetPrivateField(_authorizedTrainingPanel, "_client", _client);
            SetPrivateField(_authorizedTrainingPanel, "_config", _config);
        }

        AuthorizedTrainingPanel BuildAuthorizedTrainingPanel(Transform canvas)
        {
            var root = CreatePanel(canvas, "AuthorizedTrainingPanel", new Vector2(560, 380));
            var brand = CreateText(root.transform, "Brand", "WOI VR Bridge", 26, TextAnchor.UpperCenter);
            var module = CreateText(root.transform, "Module", string.Empty, 22, TextAnchor.UpperCenter, -40);
            var participant = CreateText(root.transform, "Participant", string.Empty, 16, TextAnchor.UpperCenter, -80);
            var personnel = CreateText(root.transform, "Personnel", string.Empty, 16, TextAnchor.UpperCenter, -105);
            var status = CreateText(root.transform, "Status", string.Empty, 14, TextAnchor.UpperCenter, -140);
            var startButton = CreateButton(root.transform, "StartButton", "Eğitimi Başlat", new Vector2(0, -180));
            startButton.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 48);

            var panel = root.AddComponent<AuthorizedTrainingPanel>();
            SetPrivateField(panel, "_brandText", brand);
            SetPrivateField(panel, "_moduleText", module);
            SetPrivateField(panel, "_participantText", participant);
            SetPrivateField(panel, "_personnelText", personnel);
            SetPrivateField(panel, "_statusText", status);
            SetPrivateField(panel, "_startButton", startButton);
            root.SetActive(false);
            return panel;
        }

        VrBridgePairingPanel BuildPairingPanel(Transform canvas)
        {
            var root = CreatePanel(canvas, "PairingPanel", new Vector2(520, 360));
            var brand = CreateText(root.transform, "Brand", "WOI VR Bridge", 28, TextAnchor.UpperCenter);
            var title = CreateText(root.transform, "Title", "Pairing Required", 22, TextAnchor.UpperCenter, -40);
            var instructions = CreateText(root.transform, "Instructions", "Enter the 6-digit code.", 16, TextAnchor.UpperCenter, -80);
            var status = CreateText(root.transform, "Status", string.Empty, 14, TextAnchor.UpperCenter, -120);

            var inputGo = new GameObject("CodeInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputGo.transform.SetParent(root.transform, false);
            var inputRt = inputGo.GetComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0.5f, 0.5f);
            inputRt.anchorMax = new Vector2(0.5f, 0.5f);
            inputRt.sizeDelta = new Vector2(240, 40);
            inputRt.anchoredPosition = new Vector2(0, -10);
            var input = inputGo.GetComponent<InputField>();
            input.contentType = InputField.ContentType.IntegerNumber;
            input.characterLimit = 6;
            var placeholder = CreateText(inputGo.transform, "Placeholder", "000000", 18, TextAnchor.MiddleCenter);
            placeholder.color = new Color(1, 1, 1, 0.35f);
            input.placeholder = placeholder;
            input.textComponent = CreateText(inputGo.transform, "Text", string.Empty, 18, TextAnchor.MiddleCenter);

            var submit = CreateButton(root.transform, "Submit", "Pair", new Vector2(-120, -90));
            var retry = CreateButton(root.transform, "Retry", "Retry", new Vector2(0, -90));
            var repair = CreateButton(root.transform, "Repair", "Re-pair", new Vector2(120, -90));

            var panel = root.AddComponent<VrBridgePairingPanel>();
            SetPrivateField(panel, "_codeInput", input);
            SetPrivateField(panel, "_brandText", brand);
            SetPrivateField(panel, "_titleText", title);
            SetPrivateField(panel, "_instructionsText", instructions);
            SetPrivateField(panel, "_statusText", status);
            SetPrivateField(panel, "_submitButton", submit);
            SetPrivateField(panel, "_retryButton", retry);
            SetPrivateField(panel, "_repairButton", repair);
            root.SetActive(false);
            return panel;
        }

        VrBridgeDiagnosticsPanel BuildDiagnosticsPanel(Transform canvas)
        {
            var root = CreatePanel(canvas, "DiagnosticsPanel", new Vector2(640, 520));
            root.transform.localPosition = new Vector3(0, 0, 0);
            var summary = CreateText(root.transform, "Summary", "Diagnostics", 14, TextAnchor.UpperLeft);
            var summaryRt = summary.rectTransform;
            summaryRt.anchorMin = new Vector2(0.05f, 0.25f);
            summaryRt.anchorMax = new Vector2(0.95f, 0.95f);
            summaryRt.offsetMin = Vector2.zero;
            summaryRt.offsetMax = Vector2.zero;
            summary.alignment = TextAnchor.UpperLeft;

            var retry = CreateButton(root.transform, "Retry", "Retry", new Vector2(-180, -210));
            var repair = CreateButton(root.transform, "Repair", "Re-pair", new Vector2(-60, -210));
            var copy = CreateButton(root.transform, "Copy", "Copy Code", new Vector2(60, -210));
            var reset = CreateButton(root.transform, "Reset", "Reset ID", new Vector2(180, -210));
            var export = CreateButton(root.transform, "Export", "Export", new Vector2(0, -250));
            var toggle = CreateButton(canvas, "DiagnosticsToggle", "Bridge Diagnostics", new Vector2(0, -Screen.height * 0.42f));
            toggle.onClick.AddListener(() => root.SetActive(!root.activeSelf));

            var panel = root.AddComponent<VrBridgeDiagnosticsPanel>();
            SetPrivateField(panel, "_summaryText", summary);
            SetPrivateField(panel, "_retryButton", retry);
            SetPrivateField(panel, "_repairButton", repair);
            SetPrivateField(panel, "_copyErrorButton", copy);
            SetPrivateField(panel, "_resetIdentityButton", reset);
            SetPrivateField(panel, "_exportButton", export);
            root.SetActive(false);
            return panel;
        }

        void ApplyEditorDevelopmentEndpoint()
        {
            if (_config == null)
            {
                return;
            }

            // Runtime clone so Editor overrides never dirty the project asset.
            _config = Instantiate(_config);

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(_editorGatewayOverride))
            {
                _config.GatewayUrlOverride = _editorGatewayOverride;
                _config.AllowLoopbackManualEndpoint = true;
                _config.AllowManualHostFallback = true;
            }
#else
            // Quest/player builds must never target loopback.
            _config.GatewayUrlOverride = string.Empty;
            _config.AllowLoopbackManualEndpoint = false;
            if (!string.IsNullOrWhiteSpace(_config.ManualBridgeHost)
                && (_config.ManualBridgeHost == "127.0.0.1" || _config.ManualBridgeHost == "localhost"))
            {
                _config.ManualBridgeHost = string.Empty;
            }
#endif
        }

        void LogNetworkMode()
        {
            Debug.Log(
                "[VRBridge] networkMode=ProtocolV2 " +
                "legacyNetworkingEnabled=false " +
                $"discoveryPort={ProtocolConstants.DiscoveryPort} " +
                $"gatewayPath={ProtocolConstants.GatewayPath}");
        }

        static string ShortId(string id)
        {
            return string.IsNullOrEmpty(id) || id.Length < 8 ? id : id.Substring(0, 8);
        }

        static GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.92f);
            return go;
        }

        static Text CreateText(Transform parent, string name, string value, int size, TextAnchor anchor, float y = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.5f);
            rt.anchorMax = new Vector2(0.95f, 0.95f);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.text = value;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 anchored)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(110, 36);
            rt.anchoredPosition = anchored;
            go.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.75f, 1f);
            var text = CreateText(go.transform, "Label", label, 14, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return go.GetComponent<Button>();
        }

        static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
            {
                return;
            }

            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }
    }
}
