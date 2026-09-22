using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Woi.VrBridge.Client.Protocol.Messages
{
    public sealed class DiscoveryRequestPayload
    {
        [JsonProperty("deviceId")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonProperty("nonce")]
        public string Nonce { get; set; } = string.Empty;
    }

    public sealed class DiscoveryOfferPayload
    {
        [JsonProperty("serviceId")]
        public string ServiceId { get; set; } = string.Empty;

        [JsonProperty("serviceName")]
        public string ServiceName { get; set; } = "WOI VR Bridge";

        [JsonProperty("deviceGatewayUrl")]
        public string DeviceGatewayUrl { get; set; } = string.Empty;

        [JsonProperty("nonce")]
        public string Nonce { get; set; } = string.Empty;

        [JsonProperty("requiresPairing")]
        public bool RequiresPairing { get; set; } = true;
    }

    public sealed class DeviceHelloPayload
    {
        [JsonProperty("deviceId")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonProperty("deviceToken")]
        public string DeviceToken { get; set; }

        [JsonProperty("pairingCode")]
        public string PairingCode { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("appVersion")]
        public string AppVersion { get; set; } = string.Empty;

        [JsonProperty("protocolVersion")]
        public int ProtocolVersion { get; set; } = ProtocolConstants.ProtocolVersion;

        [JsonProperty("deviceModel")]
        public string DeviceModel { get; set; }

        [JsonProperty("operatingSystem")]
        public string OperatingSystem { get; set; }

        [JsonProperty("capabilities")]
        public string[] Capabilities { get; set; } = Array.Empty<string>();

        [JsonProperty("lastKnownSessionId")]
        public string LastKnownSessionId { get; set; }

        [JsonProperty("connectionInstanceId")]
        public string ConnectionInstanceId { get; set; } = string.Empty;
    }

    public sealed class DeviceAcceptedPayload
    {
        [JsonProperty("deviceId")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonProperty("connectionId")]
        public string ConnectionId { get; set; } = string.Empty;

        [JsonProperty("serviceId")]
        public string ServiceId { get; set; } = string.Empty;

        [JsonProperty("deviceToken")]
        public string DeviceToken { get; set; }

        [JsonProperty("heartbeatIntervalMilliseconds")]
        public int HeartbeatIntervalMilliseconds { get; set; }

        [JsonProperty("staleThresholdMilliseconds")]
        public int StaleThresholdMilliseconds { get; set; }

        [JsonProperty("offlineThresholdMilliseconds")]
        public int OfflineThresholdMilliseconds { get; set; }

        [JsonProperty("serverTimeUtc")]
        public DateTimeOffset ServerTimeUtc { get; set; }

        [JsonProperty("activeSession")]
        public ActiveSessionSnapshot ActiveSession { get; set; }
    }

    public sealed class ActiveSessionSnapshot
    {
        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("moduleId")]
        public string ModuleId { get; set; }

        [JsonProperty("updatedAtUtc")]
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    public sealed class DeviceRejectedPayload
    {
        [JsonProperty("errorCode")]
        public string ErrorCode { get; set; } = string.Empty;

        [JsonProperty("messageKey")]
        public string MessageKey { get; set; } = string.Empty;

        [JsonProperty("retryable")]
        public bool Retryable { get; set; }

        [JsonProperty("suggestedActionIds")]
        public string[] SuggestedActionIds { get; set; } = Array.Empty<string>();
    }

    public sealed class DeviceHeartbeatPayload
    {
        [JsonProperty("sequence")]
        public long Sequence { get; set; }

        [JsonProperty("clientTimeUtc")]
        public DateTimeOffset ClientTimeUtc { get; set; }

        [JsonProperty("deviceState")]
        public string DeviceState { get; set; } = "Online";

        [JsonProperty("activeSessionId")]
        public string ActiveSessionId { get; set; }

        [JsonProperty("batteryLevelPercent")]
        public int? BatteryLevelPercent { get; set; }

        [JsonProperty("networkType")]
        public string NetworkType { get; set; }
    }

    public sealed class DeviceHeartbeatAckPayload
    {
        [JsonProperty("sequence")]
        public long Sequence { get; set; }

        [JsonProperty("serverTimeUtc")]
        public DateTimeOffset ServerTimeUtc { get; set; }
    }

    public sealed class DeviceStateChangedPayload
    {
        [JsonProperty("deviceState")]
        public string DeviceState { get; set; } = string.Empty;

        [JsonProperty("reason")]
        public string Reason { get; set; }
    }

    public sealed class SessionParticipantPayload
    {
        [JsonProperty("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonProperty("personnelId")]
        public string PersonnelId { get; set; } = string.Empty;
    }

    public sealed class SessionStartCommandPayload
    {
        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonProperty("participant")]
        public SessionParticipantPayload Participant { get; set; } = new SessionParticipantPayload();

        [JsonProperty("moduleId")]
        public string ModuleId { get; set; } = "default";

        [JsonProperty("requestedAtUtc")]
        public DateTimeOffset RequestedAtUtc { get; set; }

        [JsonProperty("expiresAtUtc")]
        public DateTimeOffset ExpiresAtUtc { get; set; }

        [JsonProperty("attemptNumber")]
        public int AttemptNumber { get; set; }
    }

    public sealed class SessionCommandAckPayload
    {
        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonProperty("commandMessageId")]
        public string CommandMessageId { get; set; } = string.Empty;

        [JsonProperty("accepted")]
        public bool Accepted { get; set; }

        [JsonProperty("rejectionCode")]
        public string RejectionCode { get; set; }

        [JsonProperty("rejectionDetail")]
        public string RejectionDetail { get; set; }

        [JsonProperty("deviceState")]
        public string DeviceState { get; set; }
    }

    /// <summary>
    /// Phase 2D: Bridge authorizes a device/participant for a module ahead of gameplay.
    /// Receiving this message must never launch the module — it only prepares an
    /// <see cref="Abstractions.BridgeSessionContext"/> for a subsequent player-initiated start.
    /// </summary>
    public sealed class SessionAuthorizeCommandPayload
    {
        [JsonProperty("authorizationId")]
        public string AuthorizationId { get; set; } = string.Empty;

        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonProperty("deviceId")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonProperty("moduleId")]
        public string ModuleId { get; set; } = "default";

        [JsonProperty("participant")]
        public SessionParticipantPayload Participant { get; set; } = new SessionParticipantPayload();

        [JsonProperty("participantHash")]
        public string ParticipantHash { get; set; } = string.Empty;

        /// <summary>
        /// One-time Firebase handoff ticket. Bridge wire name is
        /// <c>firebaseAuthorizationTicket</c>. Never log this value.
        /// </summary>
        [JsonProperty("firebaseAuthorizationTicket")]
        public string FirebaseAuthorizationTicket { get; set; } = string.Empty;

        /// <summary>Legacy alias accepted for tests / older fixtures.</summary>
        [JsonProperty("ticket")]
        public string Ticket
        {
            get => FirebaseAuthorizationTicket;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    FirebaseAuthorizationTicket = value;
                }
            }
        }

        [JsonProperty("requestedAtUtc")]
        public DateTimeOffset RequestedAtUtc { get; set; }

        [JsonProperty("expiresAtUtc")]
        public DateTimeOffset ExpiresAtUtc { get; set; }

        [JsonProperty("attemptNumber")]
        public int AttemptNumber { get; set; }
    }

    public sealed class SessionAuthorizationAckPayload
    {
        [JsonProperty("authorizationId")]
        public string AuthorizationId { get; set; } = string.Empty;

        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonProperty("commandMessageId")]
        public string CommandMessageId { get; set; } = string.Empty;

        [JsonProperty("accepted")]
        public bool Accepted { get; set; }

        [JsonProperty("rejectionCode")]
        public string RejectionCode { get; set; }

        [JsonProperty("rejectionDetail")]
        public string RejectionDetail { get; set; }

        [JsonProperty("deviceState")]
        public string DeviceState { get; set; }

        /// <summary>Firebase UID resolved after ticket redemption (safe metadata).</summary>
        [JsonProperty("firebaseUid")]
        public string FirebaseUid { get; set; }
    }

    /// <summary>
    /// Shared shape for session.authorization.revoked and session.authorization.expired,
    /// sent by the Bridge when a previously issued authorization is no longer valid.
    /// </summary>
    public sealed class SessionAuthorizationLifecyclePayload
    {
        [JsonProperty("authorizationId")]
        public string AuthorizationId { get; set; } = string.Empty;

        [JsonProperty("sessionId")]
        public string SessionId { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }
    }

    /// <summary>
    /// Sent by the Quest client the moment the player presses Start Training inside VR —
    /// strictly before the module launcher is invoked.
    /// </summary>
    public sealed class SessionPlayerStartRequestedPayload
    {
        [JsonProperty("authorizationId")]
        public string AuthorizationId { get; set; } = string.Empty;

        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonProperty("moduleId")]
        public string ModuleId { get; set; }

        [JsonProperty("requestedAtUtc")]
        public DateTimeOffset RequestedAtUtc { get; set; }
    }

    public sealed class SessionLifecyclePayload
    {
        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonProperty("moduleId")]
        public string ModuleId { get; set; }

        [JsonProperty("detail")]
        public string Detail { get; set; }

        [JsonProperty("failureCode")]
        public string FailureCode { get; set; }
    }

    public sealed class ResultSubmitPayload
    {
        [JsonProperty("resultId")]
        public string ResultId { get; set; } = string.Empty;

        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonProperty("moduleId")]
        public string ModuleId { get; set; } = "default";

        [JsonProperty("completedAtUtc")]
        public DateTimeOffset CompletedAtUtc { get; set; }

        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonProperty("result")]
        public ResultBodyPayload Result { get; set; } = new ResultBodyPayload();

        [JsonProperty("payloadSha256")]
        public string PayloadSha256 { get; set; } = string.Empty;
    }

    public sealed class ResultBodyPayload
    {
        [JsonProperty("outcome")]
        public string Outcome { get; set; }

        [JsonProperty("score")]
        public string Score { get; set; }

        [JsonProperty("durationSeconds")]
        public int? DurationSeconds { get; set; }

        /// <summary>Free-form key/value extensions, e.g. authorizationId, so modules stay generic.</summary>
        [JsonProperty("attributes")]
        public Dictionary<string, string> Attributes { get; set; }
    }

    public sealed class ResultAckPayload
    {
        [JsonProperty("resultId")]
        public string ResultId { get; set; } = string.Empty;

        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonProperty("accepted")]
        public bool Accepted { get; set; }

        [JsonProperty("duplicate")]
        public bool Duplicate { get; set; }

        [JsonProperty("validationCode")]
        public string ValidationCode { get; set; }
    }

    public sealed class ProtocolErrorPayload
    {
        [JsonProperty("errorCode")]
        public string ErrorCode { get; set; } = string.Empty;

        [JsonProperty("detail")]
        public string Detail { get; set; }

        [JsonProperty("retryable")]
        public bool Retryable { get; set; }
    }
}
