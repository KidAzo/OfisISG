using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Woi.VrBridge.Client.Protocol.Messages;

namespace Woi.VrBridge.Client.Abstractions
{
    public enum VrBridgeConnectionState
    {
        Uninitialized = 0,
        Discovering = 1,
        BridgeFound = 2,
        Connecting = 3,
        PairingRequired = 4,
        Authenticating = 5,
        Connected = 6,
        Degraded = 7,
        Reconnecting = 8,
        Offline = 9,
        AuthenticationFailed = 10,
        ProtocolRejected = 11,
        Error = 12,
        Disconnected = 13
    }

    public sealed class BridgeSessionContext
    {
        public string SessionId { get; }
        public string DeviceId { get; }
        public string ModuleId { get; }
        public string CommandMessageId { get; }
        public string ParticipantFullName { get; }
        public string PersonnelId { get; }
        public SessionParticipantPayload Participant { get; }
        public DateTimeOffset RequestedAtUtc { get; }
        public DateTimeOffset ExpiresAtUtc { get; }
        public DateTimeOffset AcceptedAtUtc { get; }
        public string ConnectionId { get; }
        public int AttemptNumber { get; }

        /// <summary>
        /// Present when this context originated from session.authorize.command (Phase 2D).
        /// Null for any context produced by the deprecated session.start.command path.
        /// </summary>
        public string AuthorizationId { get; }

        public BridgeSessionContext(
            string sessionId,
            string deviceId,
            string moduleId,
            string commandMessageId,
            SessionParticipantPayload participant,
            DateTimeOffset requestedAtUtc,
            DateTimeOffset expiresAtUtc,
            DateTimeOffset acceptedAtUtc,
            string connectionId,
            int attemptNumber,
            string authorizationId = null)
        {
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            DeviceId = deviceId ?? string.Empty;
            ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
            CommandMessageId = commandMessageId ?? throw new ArgumentNullException(nameof(commandMessageId));
            Participant = participant ?? throw new ArgumentNullException(nameof(participant));
            ParticipantFullName = participant.FullName ?? string.Empty;
            PersonnelId = participant.PersonnelId ?? string.Empty;
            RequestedAtUtc = requestedAtUtc;
            ExpiresAtUtc = expiresAtUtc;
            AcceptedAtUtc = acceptedAtUtc;
            ConnectionId = connectionId ?? string.Empty;
            AttemptNumber = attemptNumber;
            AuthorizationId = authorizationId;
        }
    }

    public interface IVrBridgeConnectionState
    {
        VrBridgeConnectionState State { get; }
        event Action<VrBridgeConnectionState, string> StateChanged;
    }

    public interface IVrBridgeSessionContext
    {
        BridgeSessionContext ActiveSession { get; }
        event Action<BridgeSessionContext> SessionStarted;
        event Action<string> SessionCompleted;
        event Action<string, string> SessionFailed;
        event Action<string> SessionCancelled;
    }

    public interface IVrBridgeDiagnostics
    {
        string LastErrorCode { get; }
        string LastErrorMessage { get; }
        string ConnectionId { get; }
        string GatewayUrl { get; }
        string ServiceId { get; }
        long HeartbeatSequence { get; }
        double? ApproximateRttMilliseconds { get; }
        int ReconnectAttempt { get; }
        BridgeDiagnosticsSnapshot CreateSnapshot();
    }

    public interface IDeviceCredentialStore
    {
        bool HasToken { get; }
        UniTask<string> ReadTokenAsync(CancellationToken cancellationToken);
        UniTask WriteTokenAsync(string token, CancellationToken cancellationToken);
        UniTask DeleteTokenAsync(CancellationToken cancellationToken);
    }

    public interface IBridgeDiscoveryClient
    {
        UniTask<DiscoveryOfferPayload> DiscoverAsync(string deviceId, CancellationToken cancellationToken);
        UniTask<DiscoveryOfferPayload> DiscoverUdpAsync(string deviceId, CancellationToken cancellationToken);
    }

    public interface IBridgeWebSocketTransport
    {
        bool IsConnected { get; }
        event Action<string> MessageReceived;
        event Action Closed;
        UniTask ConnectAsync(Uri gatewayUri, CancellationToken cancellationToken);
        UniTask SendAsync(byte[] payload, CancellationToken cancellationToken);
        UniTask DisconnectAsync(CancellationToken cancellationToken);
    }

    public interface IBridgeModuleLauncher
    {
        bool CanLaunch(string moduleId);
        UniTask LaunchSessionAsync(BridgeSessionContext context, CancellationToken cancellationToken);
        UniTask CancelSessionAsync(string sessionId, CancellationToken cancellationToken);
    }

    public interface IMainThreadDispatcher
    {
        void Enqueue(Action action);
        UniTask EnqueueAsync(Func<UniTask> action);
    }

    public interface IVrBridgeResultPublisher
    {
        int PendingResultCount { get; }

        UniTask QueueResultAsync(
            string resultId,
            string sessionId,
            string moduleId,
            ResultBodyPayload body,
            CancellationToken cancellationToken);

        UniTask RetryPendingResultsAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Phase 2D authorization surface. Authorization only ever prepares a session for a
    /// player-initiated start — it must never trigger <see cref="IBridgeModuleLauncher.LaunchSessionAsync"/>.
    /// </summary>
    public interface IVrBridgeAuthorizationContext
    {
        BridgeSessionContext AuthorizedSession { get; }
        bool CanStartAuthorizedTraining { get; }

        /// <summary>Raised once an authorization has been redeemed and validated. Never launches the module.</summary>
        event Action<BridgeSessionContext> AuthorizationReady;

        /// <summary>Raised when the Bridge revokes an outstanding authorization (sessionId, reason).</summary>
        event Action<string, string> AuthorizationRevoked;

        /// <summary>Raised when an outstanding authorization expires (sessionId, reason).</summary>
        event Action<string, string> AuthorizationExpired;

        /// <summary>
        /// The ONLY path allowed to call the module launcher. Must be invoked exclusively from the
        /// player pressing "Eğitimi Başlat" / "Start Training" inside VR.
        /// </summary>
        UniTask<bool> StartAuthorizedTrainingAsync(CancellationToken cancellationToken);
    }

    public interface IVrBridgeClient : IVrBridgeConnectionState, IVrBridgeSessionContext, IVrBridgeDiagnostics, IVrBridgeResultPublisher, IVrBridgeAuthorizationContext
    {
        string DeviceId { get; }

        /// <summary>Firebase customer auth session established by ticket redemption (may be unauthenticated).</summary>
        IVrCustomerAuthSession CustomerAuthSession { get; }

        UniTask InitializeAsync(CancellationToken cancellationToken);
        UniTask ConnectAsync(CancellationToken cancellationToken);
        UniTask DisconnectAsync(CancellationToken cancellationToken);
        UniTask ReconnectAsync(CancellationToken cancellationToken);
        UniTask RequestRepairAsync(CancellationToken cancellationToken);
        UniTask SubmitPairingCodeAsync(string pairingCode, CancellationToken cancellationToken);
        UniTask ResetIdentityAsync(CancellationToken cancellationToken);
        void NotifySessionStarted(string sessionId, string moduleId);
        void NotifySessionCompleted(string sessionId);
        void NotifySessionFailed(string sessionId, string failureCode, string detail);
    }

    public interface IBridgeStringLocalizer
    {
        string Get(string key, string fallback);
    }

    /// <summary>Thrown by <see cref="IVrCustomerAuthSession"/> on redemption/sign-in/refresh failures.</summary>
    public sealed class VrCustomerAuthException : Exception
    {
        public string ErrorCode { get; }

        public VrCustomerAuthException(string errorCode, string message, Exception inner = null)
            : base(message ?? errorCode, inner)
        {
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Request to redeem a Bridge-issued one-time ticket for a Firebase custom token via the
    /// <c>redeemVrTrainingAuthorization</c> callable. <see cref="Ticket"/> must never be logged.
    /// </summary>
    public sealed class RedeemRequest
    {
        public string Ticket { get; }
        public string AuthorizationId { get; }
        public string SessionId { get; }
        public string DeviceId { get; }
        public string ModuleId { get; }
        public string ParticipantHash { get; }

        public RedeemRequest(
            string ticket,
            string authorizationId,
            string sessionId,
            string deviceId,
            string moduleId,
            string participantHash)
        {
            Ticket = ticket ?? throw new ArgumentNullException(nameof(ticket));
            AuthorizationId = authorizationId ?? throw new ArgumentNullException(nameof(authorizationId));
            SessionId = sessionId ?? string.Empty;
            DeviceId = deviceId ?? string.Empty;
            ModuleId = moduleId ?? string.Empty;
            ParticipantHash = participantHash ?? string.Empty;
        }
    }

    /// <summary>
    /// Bridge-issued one-time ticket exchange with the Digitech Hub Firebase project.
    /// Firebase Auth SDK is intentionally not used — all calls are plain HTTPS/REST so the
    /// Quest client stays lightweight. Implementations must never log <see cref="RedeemRequest.Ticket"/>
    /// or the resulting tokens.
    /// </summary>
    public interface IVrCustomerAuthSession
    {
        bool IsAuthenticated { get; }
        string FirebaseUid { get; }
        string CustomerId { get; }

        UniTask RedeemAndSignInAsync(RedeemRequest request, CancellationToken cancellationToken);
        UniTask<string> GetIdTokenAsync(CancellationToken cancellationToken);
        UniTask SignOutCustomerAsync(CancellationToken cancellationToken);
    }

    public sealed class BridgeDiagnosticsSnapshot
    {
        public VrBridgeConnectionState State { get; set; }
        public string DeviceId { get; set; }
        public string DeviceDisplayName { get; set; }
        public string ConnectionId { get; set; }
        public string ServiceId { get; set; }
        public string GatewayUrl { get; set; }
        public string AppVersion { get; set; }
        public int ProtocolVersion { get; set; }
        public bool IsPaired { get; set; }
        public string LastErrorCode { get; set; }
        public string LastErrorMessage { get; set; }
        public string SuggestedAction { get; set; }
        public long HeartbeatSequence { get; set; }
        public DateTimeOffset? LastHeartbeatSentUtc { get; set; }
        public DateTimeOffset? LastHeartbeatAckUtc { get; set; }
        public double? ApproximateRttMilliseconds { get; set; }
        public int ReconnectAttempt { get; set; }
        public string ActiveSessionId { get; set; }
        public int PendingResultCount { get; set; }
    }
}
