using System;
using System.Collections.Generic;

namespace Woi.VrBridge.Client.Protocol
{
    public static class ProtocolConstants
    {
        public const int ProtocolVersion = 2;
        public const int MaxMessageBytes = 64 * 1024;
        public const int MaxResultPayloadBytes = 48 * 1024;
        public const int TimestampSkewMinutes = 10;
        public const int DiscoveryPort = 17778;
        public const int GatewayPort = 17881;
        public const string GatewayPath = "/ws/device";
        public const int HandshakeTimeoutSeconds = 10;

        public static class MessageTypes
        {
            public const string BridgeDiscoveryRequest = "bridge.discovery.request";
            public const string BridgeDiscoveryOffer = "bridge.discovery.offer";
            public const string DeviceHello = "device.hello";
            public const string DeviceAccepted = "device.accepted";
            public const string DeviceRejected = "device.rejected";
            public const string DeviceHeartbeat = "device.heartbeat";
            public const string DeviceHeartbeatAck = "device.heartbeat.ack";
            public const string DeviceStateChanged = "device.state.changed";
            /// <summary>
            /// Deprecated in Phase 2D. Bridge should send <see cref="SessionAuthorizeCommand"/> instead.
            /// Kept only so the client can recognize and reject legacy commands without auto-launching.
            /// </summary>
            public const string SessionStartCommand = "session.start.command";
            public const string SessionCommandAck = "session.command.ack";
            public const string SessionAuthorizeCommand = "session.authorize.command";
            public const string SessionAuthorizationAck = "session.authorization.ack";
            public const string SessionAuthorizationRevoked = "session.authorization.revoked";
            public const string SessionAuthorizationExpired = "session.authorization.expired";
            public const string SessionPlayerStartRequested = "session.player.start.requested";
            public const string SessionStarted = "session.started";
            public const string SessionCompleted = "session.completed";
            public const string SessionFailed = "session.failed";
            public const string SessionCancelCommand = "session.cancel.command";
            public const string SessionCancelled = "session.cancelled";
            public const string ResultSubmit = "result.submit";
            public const string ResultAck = "result.ack";
            public const string ProtocolError = "protocol.error";
        }

        public static readonly HashSet<string> KnownTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            MessageTypes.BridgeDiscoveryRequest,
            MessageTypes.BridgeDiscoveryOffer,
            MessageTypes.DeviceHello,
            MessageTypes.DeviceAccepted,
            MessageTypes.DeviceRejected,
            MessageTypes.DeviceHeartbeat,
            MessageTypes.DeviceHeartbeatAck,
            MessageTypes.DeviceStateChanged,
            MessageTypes.SessionStartCommand,
            MessageTypes.SessionCommandAck,
            MessageTypes.SessionAuthorizeCommand,
            MessageTypes.SessionAuthorizationAck,
            MessageTypes.SessionAuthorizationRevoked,
            MessageTypes.SessionAuthorizationExpired,
            MessageTypes.SessionPlayerStartRequested,
            MessageTypes.SessionStarted,
            MessageTypes.SessionCompleted,
            MessageTypes.SessionFailed,
            MessageTypes.SessionCancelCommand,
            MessageTypes.SessionCancelled,
            MessageTypes.ResultSubmit,
            MessageTypes.ResultAck,
            MessageTypes.ProtocolError
        };
    }
}
