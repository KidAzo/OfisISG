using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Protocol;
using Woi.VrBridge.Client.Protocol.Messages;

namespace Woi.VrBridge.Client.Connection
{
    /// <summary>
    /// Phase 2D authorization pipeline: validates session.authorize.command, redeems the Bridge
    /// one-time ticket for a Firebase customer session, and stores an immutable
    /// <see cref="BridgeSessionContext"/> for the UI to present. This handler NEVER calls
    /// <see cref="IBridgeModuleLauncher.LaunchSessionAsync"/> — only <see cref="TryBeginStart"/> +
    /// the owning <c>VrBridgeClient.StartAuthorizedTrainingAsync</c> may do that, and only in
    /// response to the player pressing Start Training inside VR.
    /// </summary>
    public sealed class SessionAuthorizationHandler
    {
        readonly BridgeClientConfig _config;
        readonly IBridgeModuleLauncher _moduleLauncher;
        readonly IVrCustomerAuthSession _authSession;
        readonly Func<SessionAuthorizationAckPayload, UniTask> _sendAckAsync;
        readonly Func<string, string, UniTask> _setActiveSessionAsync;
        readonly Action<BridgeSessionContext> _onAuthorizationReady;
        readonly Action<string, string> _onAuthorizationRevoked;
        readonly Action<string, string> _onAuthorizationExpired;
        readonly Func<string> _getDeviceId;
        readonly Func<string> _getConnectionId;

        BridgeSessionContext _activeAuthorization;
        string _lastAuthorizationId;
        string _lastPayloadHash;
        bool _lastAccepted;
        string _lastRejectionCode;
        string _lastRejectionDetail;
        int _started;

        public SessionAuthorizationHandler(
            BridgeClientConfig config,
            IBridgeModuleLauncher moduleLauncher,
            IVrCustomerAuthSession authSession,
            Func<SessionAuthorizationAckPayload, UniTask> sendAckAsync,
            Func<string, string, UniTask> setActiveSessionAsync,
            Action<BridgeSessionContext> onAuthorizationReady,
            Action<string, string> onAuthorizationRevoked,
            Action<string, string> onAuthorizationExpired,
            Func<string> getDeviceId,
            Func<string> getConnectionId)
        {
            _config = config;
            _moduleLauncher = moduleLauncher;
            _authSession = authSession ?? throw new ArgumentNullException(nameof(authSession));
            _sendAckAsync = sendAckAsync ?? throw new ArgumentNullException(nameof(sendAckAsync));
            _setActiveSessionAsync = setActiveSessionAsync;
            _onAuthorizationReady = onAuthorizationReady;
            _onAuthorizationRevoked = onAuthorizationRevoked;
            _onAuthorizationExpired = onAuthorizationExpired;
            _getDeviceId = getDeviceId ?? (() => string.Empty);
            _getConnectionId = getConnectionId ?? (() => string.Empty);
        }

        public BridgeSessionContext ActiveAuthorization => _activeAuthorization;

        public bool CanStartTraining(DateTimeOffset utcNow)
        {
            var context = _activeAuthorization;
            if (context == null)
            {
                return false;
            }

            if (Volatile.Read(ref _started) != 0)
            {
                return false;
            }

            if (context.ExpiresAtUtc <= utcNow)
            {
                return false;
            }

            return _moduleLauncher != null && _moduleLauncher.CanLaunch(context.ModuleId);
        }

        /// <summary>Atomic guard so a double button press (or re-entrant call) can only launch once.</summary>
        public bool TryBeginStart()
        {
            if (_activeAuthorization == null)
            {
                return false;
            }

            return Interlocked.CompareExchange(ref _started, 1, 0) == 0;
        }

        public void ClearAuthorization()
        {
            _activeAuthorization = null;
            Interlocked.Exchange(ref _started, 0);
        }

        public async UniTask HandleAuthorizeAsync(ProtocolEnvelope envelope, CancellationToken cancellationToken)
        {
            var command = ProtocolJson.DeserializePayload<SessionAuthorizeCommandPayload>(envelope.Payload);
            if (command == null || string.IsNullOrWhiteSpace(command.AuthorizationId) || string.IsNullOrWhiteSpace(command.SessionId))
            {
                await _sendAckAsync(new SessionAuthorizationAckPayload
                {
                    AuthorizationId = command?.AuthorizationId ?? string.Empty,
                    SessionId = command?.SessionId ?? string.Empty,
                    CommandMessageId = envelope.MessageId,
                    Accepted = false,
                    RejectionCode = ErrorCodes.AuthorizationInvalidCommand,
                    RejectionDetail = "Malformed authorize command.",
                    DeviceState = "Online"
                });
                return;
            }

            var payloadHash = ComputeHash(command);

            // Idempotent replay of the exact same command (e.g. Bridge redelivery after a dropped ack).
            if (string.Equals(_lastAuthorizationId, command.AuthorizationId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_lastPayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
            {
                await _sendAckAsync(BuildAck(
                    command,
                    envelope.MessageId,
                    _lastAccepted,
                    _lastRejectionCode,
                    _lastRejectionDetail,
                    _lastAccepted ? _authSession?.FirebaseUid : null));
                return;
            }

            if (string.IsNullOrWhiteSpace(envelope.DeviceId)
                || !string.Equals(envelope.DeviceId, _getDeviceId(), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(command.DeviceId, _getDeviceId(), StringComparison.OrdinalIgnoreCase))
            {
                await RejectAsync(command, envelope.MessageId, payloadHash, ErrorCodes.AuthorizationDeviceMismatch, "Device mismatch.");
                return;
            }

            if (_moduleLauncher == null || !_moduleLauncher.CanLaunch(command.ModuleId) || !IsModuleSupported(command.ModuleId))
            {
                await RejectAsync(command, envelope.MessageId, payloadHash, ErrorCodes.AuthorizationModuleUnsupported, "Module unavailable.");
                return;
            }

            if (command.Participant == null
                || string.IsNullOrWhiteSpace(command.Participant.FullName)
                || string.IsNullOrWhiteSpace(command.Participant.PersonnelId))
            {
                await RejectAsync(command, envelope.MessageId, payloadHash, ErrorCodes.AuthorizationParticipantInvalid, "Invalid participant.");
                return;
            }

            if (!ParticipantHashUtility.Matches(command.Participant.FullName, command.Participant.PersonnelId, command.ParticipantHash))
            {
                await RejectAsync(command, envelope.MessageId, payloadHash, ErrorCodes.AuthorizationHashMismatch, "Participant hash mismatch.");
                return;
            }

            if (command.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                await RejectAsync(command, envelope.MessageId, payloadHash, ErrorCodes.AuthorizationExpired, "Authorization already expired.");
                return;
            }

            if (_activeAuthorization != null
                && !string.Equals(_activeAuthorization.AuthorizationId, command.AuthorizationId, StringComparison.OrdinalIgnoreCase))
            {
                await RejectAsync(command, envelope.MessageId, payloadHash, ErrorCodes.AuthorizationConflict, "Another authorization is already active on this device.");
                return;
            }

            if (string.IsNullOrWhiteSpace(command.FirebaseAuthorizationTicket))
            {
                await RejectAsync(command, envelope.MessageId, payloadHash, ErrorCodes.AuthorizationInvalidCommand, "Missing redemption ticket.");
                return;
            }

            try
            {
                var redeemRequest = new RedeemRequest(
                    command.FirebaseAuthorizationTicket,
                    command.AuthorizationId,
                    command.SessionId,
                    _getDeviceId(),
                    command.ModuleId,
                    command.ParticipantHash);

                // Tickets are one-time: always redeem per authorize turn, even if already
                // authenticated as the same Firebase uid from a previous session.
                await _authSession.RedeemAndSignInAsync(redeemRequest, cancellationToken);
            }
            catch (VrCustomerAuthException authEx)
            {
                await RejectAsync(command, envelope.MessageId, payloadHash, authEx.ErrorCode, "Ticket redemption failed.");
                return;
            }
            catch (Exception)
            {
                await RejectAsync(command, envelope.MessageId, payloadHash, ErrorCodes.AuthorizationRedeemFailed, "Ticket redemption failed.");
                return;
            }

            var context = new BridgeSessionContext(
                command.SessionId,
                _getDeviceId(),
                command.ModuleId,
                envelope.MessageId,
                command.Participant,
                command.RequestedAtUtc,
                command.ExpiresAtUtc,
                DateTimeOffset.UtcNow,
                _getConnectionId(),
                command.AttemptNumber,
                command.AuthorizationId);

            _activeAuthorization = context;
            Interlocked.Exchange(ref _started, 0);
            _lastAuthorizationId = command.AuthorizationId;
            _lastPayloadHash = payloadHash;
            _lastAccepted = true;
            _lastRejectionCode = null;
            _lastRejectionDetail = null;

            if (_setActiveSessionAsync != null)
            {
                await _setActiveSessionAsync(command.SessionId, command.ModuleId);
            }

            // Accepted = participant authorized and device reserved. The module is NOT launched here.
            await _sendAckAsync(BuildAck(command, envelope.MessageId, true, null, null, _authSession?.FirebaseUid));
            _onAuthorizationReady?.Invoke(context);
        }

        public UniTask HandleRevokedAsync(SessionAuthorizationLifecyclePayload payload, CancellationToken cancellationToken)
        {
            return HandleLifecycleAsync(payload, isExpiry: false);
        }

        public UniTask HandleExpiredAsync(SessionAuthorizationLifecyclePayload payload, CancellationToken cancellationToken)
        {
            return HandleLifecycleAsync(payload, isExpiry: true);
        }

        public async UniTask HandleCancelAsync(SessionLifecyclePayload payload, CancellationToken cancellationToken)
        {
            if (payload == null || _activeAuthorization == null
                || !string.Equals(_activeAuthorization.SessionId, payload.SessionId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_moduleLauncher != null)
            {
                await _moduleLauncher.CancelSessionAsync(payload.SessionId, cancellationToken);
            }

            ClearAuthorization();
        }

        UniTask HandleLifecycleAsync(SessionAuthorizationLifecyclePayload payload, bool isExpiry)
        {
            if (payload == null || _activeAuthorization == null)
            {
                return UniTask.CompletedTask;
            }

            var matches = string.Equals(_activeAuthorization.AuthorizationId, payload.AuthorizationId, StringComparison.OrdinalIgnoreCase)
                          || (!string.IsNullOrWhiteSpace(payload.SessionId)
                              && string.Equals(_activeAuthorization.SessionId, payload.SessionId, StringComparison.OrdinalIgnoreCase));

            if (!matches)
            {
                return UniTask.CompletedTask;
            }

            var sessionId = _activeAuthorization.SessionId;
            ClearAuthorization();

            if (isExpiry)
            {
                _onAuthorizationExpired?.Invoke(sessionId, payload.Reason);
            }
            else
            {
                _onAuthorizationRevoked?.Invoke(sessionId, payload.Reason);
            }

            return UniTask.CompletedTask;
        }

        async UniTask RejectAsync(SessionAuthorizeCommandPayload command, string commandMessageId, string payloadHash, string code, string detail)
        {
            _lastAuthorizationId = command.AuthorizationId;
            _lastPayloadHash = payloadHash;
            _lastAccepted = false;
            _lastRejectionCode = code;
            _lastRejectionDetail = detail;
            await _sendAckAsync(BuildAck(command, commandMessageId, false, code, detail, null));
        }

        static SessionAuthorizationAckPayload BuildAck(
            SessionAuthorizeCommandPayload command,
            string commandMessageId,
            bool accepted,
            string rejectionCode,
            string rejectionDetail,
            string firebaseUid)
        {
            return new SessionAuthorizationAckPayload
            {
                AuthorizationId = command.AuthorizationId,
                SessionId = command.SessionId,
                CommandMessageId = commandMessageId,
                Accepted = accepted,
                RejectionCode = rejectionCode,
                RejectionDetail = rejectionDetail,
                DeviceState = accepted ? "Reserved" : "Online",
                FirebaseUid = accepted ? firebaseUid : null
            };
        }

        bool IsModuleSupported(string moduleId)
        {
            if (_config?.SupportedModuleIds == null)
            {
                return false;
            }

            foreach (var supported in _config.SupportedModuleIds)
            {
                if (string.Equals(supported, moduleId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        static string ComputeHash(SessionAuthorizeCommandPayload command)
        {
            var canonical = string.Join(
                "|",
                command.AuthorizationId,
                command.SessionId,
                command.ModuleId,
                command.Participant?.PersonnelId,
                command.Participant?.FullName,
                command.ParticipantHash,
                command.AttemptNumber.ToString(),
                command.ExpiresAtUtc.ToString("O"));

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
