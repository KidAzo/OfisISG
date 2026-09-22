using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Woi.VrBridge.Client.Configuration;

namespace Woi.VrBridge.Client.Connection
{
    public sealed class VrBridgeClientBootstrap : MonoBehaviour
    {
        [SerializeField] BridgeClientConfig _config;
        [SerializeField] BridgeModuleLauncherHost _moduleLauncherHost;
        [SerializeField] bool _connectOnStart = true;

        VrBridgeClient _client;

        public VrBridgeClient Client => _client;
        public BridgeClientConfig Config => _config;

        void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("[VRBridge] BridgeClientConfig is not assigned.");
                return;
            }

            var errors = _config.Validate();
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Debug.LogError($"[VRBridge] Config validation: {error}");
                }

                return;
            }

            _client = gameObject.GetComponent<VrBridgeClient>();
            if (_client == null)
            {
                _client = gameObject.AddComponent<VrBridgeClient>();
            }

            var launcher = _moduleLauncherHost != null ? _moduleLauncherHost.Launcher : null;
            _client.Initialize(_config, moduleLauncher: launcher);
        }

        void Start()
        {
            if (_client == null || !_connectOnStart)
            {
                return;
            }

            BootstrapAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid BootstrapAsync(CancellationToken cancellationToken)
        {
            await _client.InitializeAsync(cancellationToken);
            await _client.ConnectAsync(cancellationToken);
        }

        void OnDestroy()
        {
            if (_client != null)
            {
                _client.DisconnectAsync(CancellationToken.None).Forget();
            }
        }
    }
}
