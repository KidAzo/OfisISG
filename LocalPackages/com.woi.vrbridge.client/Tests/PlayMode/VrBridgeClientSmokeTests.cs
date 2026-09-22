using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Connection;
using Woi.VrBridge.Client.Storage;
using Woi.VrBridge.Client.Tests.Support;
using Woi.VrBridge.Client.Threading;

namespace Woi.VrBridge.Client.Tests.PlayMode
{
    public sealed class VrBridgeClientSmokeTests
    {
        [UnityTest]
        public IEnumerator Client_SurvivesSceneLoad()
        {
            var go = new GameObject("VrBridgeClientTest");
            var client = go.AddComponent<VrBridgeClient>();
            var config = ScriptableObject.CreateInstance<BridgeClientConfig>();
            config.SupportedModuleIds = new[] { "default" };
            config.GatewayUrlOverride = "ws://127.0.0.1:17881/ws/device";
            config.AllowLoopbackManualEndpoint = true;
            config.AllowManualHostFallback = true;
            config.DiscoveryEnabled = false;

            client.Initialize(
                config,
                transport: new FakeWebSocketTransport(),
                discovery: null,
                credentialStore: new MemoryCredentialStore(),
                identityStore: new DeviceIdentityStore(),
                dispatcher: VrBridgeMainThreadDispatcher.Instance,
                moduleLauncher: new DefaultBridgeModuleLauncher(config.SupportedModuleIds));

            yield return client.InitializeAsync(CancellationToken.None).ToCoroutine();

            Object.DontDestroyOnLoad(go);
            yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
            Assert.IsTrue(client != null);
            Assert.AreEqual(VrBridgeConnectionState.Disconnected, client.State);
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator MainThreadDispatcher_ExecutesQueuedAction()
        {
            var ran = false;
            VrBridgeMainThreadDispatcher.Instance.Enqueue(() => ran = true);
            yield return null;
            Assert.IsTrue(ran);
        }
    }
}
