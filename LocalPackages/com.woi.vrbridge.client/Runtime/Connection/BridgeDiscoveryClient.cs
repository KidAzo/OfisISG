using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Protocol;
using Woi.VrBridge.Client.Protocol.Messages;

namespace Woi.VrBridge.Client.Connection
{
    public sealed class BridgeDiscoveryClient : IBridgeDiscoveryClient
    {
        readonly BridgeClientConfig _config;

        public BridgeDiscoveryClient(BridgeClientConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async UniTask<DiscoveryOfferPayload> DiscoverAsync(string deviceId, CancellationToken cancellationToken)
        {
            return await DiscoverUdpAsync(deviceId, cancellationToken);
        }

        public async UniTask<DiscoveryOfferPayload> DiscoverUdpAsync(string deviceId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("Device id is required.", nameof(deviceId));
            }

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                throw new InvalidOperationException(ErrorCodes.NoActiveNetworkAdapter);
            }

            using var udp = new UdpClient();
            udp.EnableBroadcast = _config.UseBroadcastDiscovery;

            var nonce = Guid.NewGuid().ToString("N");
            var request = ProtocolJson.Create(
                ProtocolConstants.MessageTypes.BridgeDiscoveryRequest,
                new DiscoveryRequestPayload { DeviceId = deviceId, Nonce = nonce },
                deviceId);

            var bytes = ProtocolJson.Serialize(request);
            var broadcast = IPAddress.TryParse(_config.DiscoveryBroadcastAddress, out var parsed)
                ? parsed
                : IPAddress.Broadcast;
            var endpoint = new IPEndPoint(broadcast, _config.DiscoveryPort);
            await udp.SendAsync(bytes, bytes.Length, endpoint);

            // Bounded retries — not every frame.
            var deadline = TimeSpan.FromSeconds(_config.DiscoveryTimeoutSeconds);
            var started = DateTime.UtcNow;
            Exception lastError = null;

            while (DateTime.UtcNow - started < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(250, _config.DiscoveryTimeoutSeconds * 1000 / 4)));

                try
                {
                    var response = await udp.ReceiveAsync().AsUniTask().AttachExternalCancellation(timeoutCts.Token);
                    return ParseAndValidateOffer(response.Buffer, nonce);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    lastError = new InvalidOperationException(ErrorCodes.DiscoveryTimeout);
                    // Re-broadcast once per timeout slice.
                    await udp.SendAsync(bytes, bytes.Length, endpoint);
                }
            }

            throw lastError ?? new InvalidOperationException(ErrorCodes.DiscoveryTimeout);
        }

        public static DiscoveryOfferPayload CreateManualOffer(string gatewayUrl, bool allowLoopback)
        {
            if (!Uri.TryCreate(gatewayUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != "ws" && uri.Scheme != "wss"))
            {
                throw new InvalidOperationException(ErrorCodes.DiscoveryInvalidOffer);
            }

            if (!allowLoopback && IsLoopback(uri))
            {
                throw new InvalidOperationException(ErrorCodes.DiscoveryLoopbackRejected);
            }

            if (!string.Equals(uri.AbsolutePath.TrimEnd('/'), ProtocolConstants.GatewayPath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                && !uri.AbsolutePath.EndsWith(ProtocolConstants.GatewayPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(ErrorCodes.DiscoveryInvalidOffer);
            }

            return new DiscoveryOfferPayload
            {
                ServiceId = "manual",
                ServiceName = "WOI VR Bridge",
                DeviceGatewayUrl = gatewayUrl,
                Nonce = Guid.NewGuid().ToString("N"),
                RequiresPairing = true
            };
        }

        DiscoveryOfferPayload ParseAndValidateOffer(byte[] buffer, string expectedNonce)
        {
            if (!ProtocolJson.TryDeserializeEnvelope(buffer, out var envelope, out var error))
            {
                throw new InvalidOperationException($"{ErrorCodes.DiscoveryInvalidOffer}: {error}");
            }

            if (!string.Equals(envelope.Type, ProtocolConstants.MessageTypes.BridgeDiscoveryOffer, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(ErrorCodes.DiscoveryInvalidOffer);
            }

            if (envelope.ProtocolVersion != ProtocolConstants.ProtocolVersion)
            {
                throw new InvalidOperationException(ErrorCodes.DiscoveryInvalidOffer);
            }

            var offer = ProtocolJson.DeserializePayload<DiscoveryOfferPayload>(envelope.Payload);
            if (offer == null
                || string.IsNullOrWhiteSpace(offer.ServiceId)
                || string.IsNullOrWhiteSpace(offer.DeviceGatewayUrl))
            {
                throw new InvalidOperationException(ErrorCodes.DiscoveryInvalidOffer);
            }

            if (!string.Equals(offer.Nonce, expectedNonce, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(ErrorCodes.DiscoveryNonceMismatch);
            }

            if (!Uri.TryCreate(offer.DeviceGatewayUrl, UriKind.Absolute, out var gatewayUri))
            {
                throw new InvalidOperationException(ErrorCodes.DiscoveryInvalidOffer);
            }

            if (IsLoopback(gatewayUri))
            {
                throw new InvalidOperationException(ErrorCodes.DiscoveryLoopbackRejected);
            }

            if (gatewayUri.Scheme != "ws" && gatewayUri.Scheme != "wss")
            {
                throw new InvalidOperationException(ErrorCodes.DiscoveryInvalidOffer);
            }

            if (gatewayUri.Port != ProtocolConstants.GatewayPort
                || !string.Equals(gatewayUri.AbsolutePath, ProtocolConstants.GatewayPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(ErrorCodes.DiscoveryInvalidOffer);
            }

            return offer;
        }

        static bool IsLoopback(Uri uri)
        {
            if (uri == null || string.IsNullOrEmpty(uri.Host))
            {
                return true;
            }

            if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip);
        }
    }
}
