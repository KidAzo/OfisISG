using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Protocol;

namespace Woi.VrBridge.Client.Connection
{
    public sealed class ClientWebSocketTransport : IBridgeWebSocketTransport
    {
        readonly IMainThreadDispatcher _dispatcher;
        readonly int _maxQueueSize;
        readonly object _gate = new object();
        ClientWebSocket _socket;
        CancellationTokenSource _receiveCts;
        readonly ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();
        int _queueCount;
        volatile bool _sendLoopRunning;

        public bool IsConnected
        {
            get
            {
                var socket = _socket;
                return socket != null && socket.State == WebSocketState.Open;
            }
        }

        public event Action<string> MessageReceived;
        public event Action Closed;

        public ClientWebSocketTransport(IMainThreadDispatcher dispatcher, int maxQueueSize = 32)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _maxQueueSize = maxQueueSize;
        }

        public async UniTask ConnectAsync(Uri gatewayUri, CancellationToken cancellationToken)
        {
            await DisconnectAsync(CancellationToken.None);

            _socket = new ClientWebSocket();
            await _socket.ConnectAsync(gatewayUri, cancellationToken);

            _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ReceiveLoop(_receiveCts.Token).Forget();
        }

        public async UniTask SendAsync(byte[] payload, CancellationToken cancellationToken)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (payload.Length > ProtocolConstants.MaxMessageBytes)
            {
                throw new InvalidOperationException("Message exceeds protocol size limit.");
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("WebSocket is not connected.");
            }

            if (Interlocked.Increment(ref _queueCount) > _maxQueueSize)
            {
                Interlocked.Decrement(ref _queueCount);
                throw new InvalidOperationException("Send queue capacity exceeded.");
            }

            _sendQueue.Enqueue(payload);
            if (!_sendLoopRunning)
            {
                SendLoop(cancellationToken).Forget();
            }

            await UniTask.CompletedTask;
        }

        async UniTaskVoid SendLoop(CancellationToken cancellationToken)
        {
            _sendLoopRunning = true;
            try
            {
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    if (!_sendQueue.TryDequeue(out var payload))
                    {
                        await UniTask.Yield();
                        continue;
                    }

                    try
                    {
                        await _socket.SendAsync(
                            new ArraySegment<byte>(payload),
                            WebSocketMessageType.Text,
                            true,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[VRBridge] WebSocket send failed: {ex.Message}");
                        RaiseClosed();
                        break;
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _queueCount);
                    }
                }
            }
            finally
            {
                _sendLoopRunning = false;
            }
        }

        async UniTaskVoid ReceiveLoop(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            try
            {
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    using var ms = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            RaiseClosed();
                            return;
                        }

                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (ms.Length > ProtocolConstants.MaxMessageBytes)
                    {
                        Debug.LogWarning("[VRBridge] Received oversized message; dropping.");
                        continue;
                    }

                    var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                    _dispatcher.Enqueue(() => MessageReceived?.Invoke(text));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VRBridge] WebSocket receive failed: {ex.Message}");
                RaiseClosed();
            }
        }

        void RaiseClosed()
        {
            _dispatcher.Enqueue(() => Closed?.Invoke());
        }

        public async UniTask DisconnectAsync(CancellationToken cancellationToken)
        {
            ClientWebSocket socket;
            CancellationTokenSource receiveCts;
            lock (_gate)
            {
                socket = _socket;
                _socket = null;
                receiveCts = _receiveCts;
                _receiveCts = null;
            }

            try
            {
                receiveCts?.Cancel();
            }
            catch
            {
                // ignore
            }

            try
            {
                receiveCts?.Dispose();
            }
            catch
            {
                // ignore
            }

            if (socket != null)
            {
                try
                {
                    if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                    {
                        using var closeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        closeCts.CancelAfter(1000);
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client disconnect", closeCts.Token);
                    }
                }
                catch
                {
                    // ignore close errors
                }

                try
                {
                    socket.Dispose();
                }
                catch
                {
                    // Unity ClientWebSocket dispose can race with receive/close
                }
            }

            while (_sendQueue.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _queueCount);
            }
        }
    }
}
