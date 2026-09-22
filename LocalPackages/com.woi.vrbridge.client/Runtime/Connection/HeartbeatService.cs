using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Protocol;
using Woi.VrBridge.Client.Protocol.Messages;

namespace Woi.VrBridge.Client.Connection
{
    public sealed class HeartbeatService : IDisposable
    {
        readonly Func<UniTask> _sendHeartbeat;
        int _intervalMs;
        CancellationTokenSource _cts;
        long _sequence;

        public long Sequence => Interlocked.Read(ref _sequence);

        public HeartbeatService(Func<UniTask> sendHeartbeat, int intervalMs)
        {
            _sendHeartbeat = sendHeartbeat ?? throw new ArgumentNullException(nameof(sendHeartbeat));
            _intervalMs = intervalMs;
        }

        public void Configure(int intervalMs)
        {
            _intervalMs = intervalMs;
        }

        public void Start(CancellationToken parentToken)
        {
            Stop();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
            Loop(_cts.Token).Forget();
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public long NextSequence() => Interlocked.Increment(ref _sequence);

        async UniTaskVoid Loop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await UniTask.Delay(_intervalMs, cancellationToken: cancellationToken);
                    await _sendHeartbeat();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[VRBridge] Heartbeat loop error: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
