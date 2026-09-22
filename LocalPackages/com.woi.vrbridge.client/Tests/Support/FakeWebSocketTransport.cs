using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Woi.VrBridge.Client.Abstractions;

namespace Woi.VrBridge.Client.Tests.Support
{
    public sealed class FakeWebSocketTransport : IBridgeWebSocketTransport
    {
        readonly Queue<string> _incoming = new Queue<string>();
        readonly List<byte[]> _sent = new List<byte[]>();

        public bool IsConnected { get; private set; }
        public IReadOnlyList<byte[]> Sent => _sent;

        public event Action<string> MessageReceived;
        public event Action Closed;

        public UniTask ConnectAsync(Uri gatewayUri, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsConnected = true;
            return UniTask.CompletedTask;
        }

        public UniTask SendAsync(byte[] payload, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected.");
            }

            _sent.Add(payload);
            return UniTask.CompletedTask;
        }

        public UniTask DisconnectAsync(CancellationToken cancellationToken)
        {
            IsConnected = false;
            Closed?.Invoke();
            return UniTask.CompletedTask;
        }

        public void EnqueueIncoming(string json)
        {
            if (!IsConnected)
            {
                return;
            }

            MessageReceived?.Invoke(json);
        }

        public void SimulateClose()
        {
            IsConnected = false;
            Closed?.Invoke();
        }
    }
}
