using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Connection;

namespace Woi.VrBridge.Client.UnityLifecycle
{
    public sealed class VrBridgeLifecycleHost : MonoBehaviour
    {
        [SerializeField] VrBridgeClient _client;

        void Reset()
        {
            _client = GetComponent<VrBridgeClient>();
        }

        void OnApplicationPause(bool paused)
        {
            if (_client == null || paused)
            {
                return;
            }

            ResumeBridgeAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (_client == null || !hasFocus)
            {
                return;
            }

            ResumeBridgeAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid ResumeBridgeAsync(CancellationToken cancellationToken)
        {
            if (_client.State == VrBridgeConnectionState.Connected)
            {
                await _client.RetryPendingResultsAsync(cancellationToken);
                return;
            }

            if (_client.State == VrBridgeConnectionState.PairingRequired
                || _client.State == VrBridgeConnectionState.AuthenticationFailed
                || _client.State == VrBridgeConnectionState.Uninitialized)
            {
                return;
            }

            await _client.ReconnectAsync(cancellationToken);
            await _client.RetryPendingResultsAsync(cancellationToken);
        }
    }
}
