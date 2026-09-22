using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Connection;
using Woi.VrBridge.Client.Storage;
using Woi.VrBridge.Client.Tests.Support;

namespace Woi.VrBridge.Client.Tests.EditMode
{
    public sealed class PairingFlowTests
    {
        sealed class ImmediateDispatcher : IMainThreadDispatcher
        {
            public void Enqueue(Action action) => action?.Invoke();

            public UniTask EnqueueAsync(Func<UniTask> action)
            {
                return action != null ? action() : UniTask.CompletedTask;
            }
        }

        [Test]
        public async Task SubmitPairingCode_DoesNotPersistInMemoryStoreUntilAccepted()
        {
            var go = new GameObject("pairing-test");
            var client = go.AddComponent<VrBridgeClient>();
            var config = ScriptableObject.CreateInstance<BridgeClientConfig>();
            config.GatewayUrlOverride = "ws://192.168.55.10:17881/ws/device";
            var store = new MemoryCredentialStore();

            client.Initialize(
                config,
                new FakeWebSocketTransport(),
                new BridgeDiscoveryClient(config),
                store,
                dispatcher: new ImmediateDispatcher());

            await client.SubmitPairingCodeAsync("123456", CancellationToken.None);
            Assert.IsFalse(store.HasToken);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
