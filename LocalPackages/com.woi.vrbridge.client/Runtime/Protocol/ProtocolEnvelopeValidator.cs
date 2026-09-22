using System;
using Woi.VrBridge.Client.Protocol.Messages;

namespace Woi.VrBridge.Client.Protocol
{
    public sealed class EnvelopeValidationResult
    {
        public bool IsValid { get; private set; }
        public string ErrorCode { get; private set; }
        public string Detail { get; private set; }

        public static EnvelopeValidationResult Ok() => new EnvelopeValidationResult { IsValid = true };

        public static EnvelopeValidationResult Fail(string code, string detail)
        {
            return new EnvelopeValidationResult
            {
                IsValid = false,
                ErrorCode = code,
                Detail = detail
            };
        }
    }

    public static class ProtocolEnvelopeValidator
    {
        public static EnvelopeValidationResult Validate(
            ProtocolEnvelope envelope,
            int byteLength,
            bool requireDeviceId,
            DateTimeOffset utcNow)
        {
            if (byteLength > ProtocolConstants.MaxMessageBytes)
            {
                return EnvelopeValidationResult.Fail(ErrorCodes.ProtocolInvalidMessage, "Message exceeds size limit.");
            }

            if (envelope.ProtocolVersion != ProtocolConstants.ProtocolVersion)
            {
                return EnvelopeValidationResult.Fail(ErrorCodes.ProtocolInvalidMessage, "Unsupported protocol version.");
            }

            if (string.IsNullOrWhiteSpace(envelope.Type) || !ProtocolConstants.KnownTypes.Contains(envelope.Type))
            {
                return EnvelopeValidationResult.Fail(ErrorCodes.ProtocolInvalidMessage, "Unknown or missing message type.");
            }

            if (string.IsNullOrWhiteSpace(envelope.MessageId))
            {
                return EnvelopeValidationResult.Fail(ErrorCodes.ProtocolInvalidMessage, "messageId is required.");
            }

            if (envelope.SentAtUtc == default)
            {
                return EnvelopeValidationResult.Fail(ErrorCodes.ProtocolInvalidMessage, "sentAtUtc is required.");
            }

            var skew = TimeSpan.FromMinutes(ProtocolConstants.TimestampSkewMinutes);
            if (envelope.SentAtUtc < utcNow - skew || envelope.SentAtUtc > utcNow + skew)
            {
                return EnvelopeValidationResult.Fail(ErrorCodes.ProtocolInvalidMessage, "sentAtUtc is out of bounds.");
            }

            if (requireDeviceId && string.IsNullOrWhiteSpace(envelope.DeviceId))
            {
                return EnvelopeValidationResult.Fail(ErrorCodes.ProtocolInvalidMessage, "deviceId is required after authentication.");
            }

            if (envelope.Payload == null || envelope.Payload.Type == Newtonsoft.Json.Linq.JTokenType.Null)
            {
                return EnvelopeValidationResult.Fail(ErrorCodes.ProtocolInvalidMessage, "payload is required.");
            }

            return EnvelopeValidationResult.Ok();
        }
    }
}
