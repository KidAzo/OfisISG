using System.Threading;
using System;
using Cysharp.Threading.Tasks;
using Woi.VrBridge.Client.Protocol;
using Woi.VrBridge.Client.Protocol.Messages;

namespace Woi.VrBridge.Client.Connection
{
    /// <summary>
    /// Phase 2D: session.start.command is deprecated. The Bridge must send
    /// session.authorize.command (see <see cref="SessionAuthorizationHandler"/>) and only launch
    /// the module after the player presses Start Training inside VR.
    /// This handler's sole responsibility is to reject legacy start commands without ever
    /// reaching <see cref="Abstractions.IBridgeModuleLauncher.LaunchSessionAsync"/>.
    /// </summary>
    public sealed class SessionCommandHandler
    {
        readonly Func<SessionCommandAckPayload, UniTask> _sendAckAsync;

        public SessionCommandHandler(Func<SessionCommandAckPayload, UniTask> sendAckAsync)
        {
            _sendAckAsync = sendAckAsync ?? throw new ArgumentNullException(nameof(sendAckAsync));
        }

        public UniTask HandleSessionStartAsync(ProtocolEnvelope envelope, CancellationToken cancellationToken)
        {
            var command = ProtocolJson.DeserializePayload<SessionStartCommandPayload>(envelope.Payload);
            var sessionId = command?.SessionId ?? string.Empty;

            return _sendAckAsync(new SessionCommandAckPayload
            {
                SessionId = sessionId,
                CommandMessageId = envelope.MessageId,
                Accepted = false,
                RejectionCode = ErrorCodes.SessionStartDeprecated,
                RejectionDetail = "session.start.command is deprecated. Send session.authorize.command and wait for the player to start training in VR.",
                DeviceState = "Online"
            });
        }
    }
}
