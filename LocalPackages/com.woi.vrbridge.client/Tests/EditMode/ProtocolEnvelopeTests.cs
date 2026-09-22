using System;
using NUnit.Framework;
using Woi.VrBridge.Client.Protocol;
using Woi.VrBridge.Client.Protocol.Messages;

namespace Woi.VrBridge.Client.Tests.EditMode
{
    public sealed class ProtocolEnvelopeTests
    {
        [Test]
        public void Serialize_UsesCamelCasePropertyNames()
        {
            var envelope = ProtocolJson.Create(
                ProtocolConstants.MessageTypes.DeviceHello,
                new DeviceHelloPayload
                {
                    DeviceId = "abc123",
                    DisplayName = "Quest",
                    AppVersion = "1.0",
                    Capabilities = new[] { "session" },
                    ConnectionInstanceId = "conn1"
                },
                "abc123");

            var json = ProtocolJson.SerializeToString(envelope);
            StringAssert.Contains("\"protocolVersion\":2", json);
            StringAssert.Contains("\"type\":\"device.hello\"", json);
            StringAssert.Contains("\"deviceId\":\"abc123\"", json);
            StringAssert.Contains("\"connectionInstanceId\":\"conn1\"", json);
            StringAssert.DoesNotContain("DeviceId", json);
        }

        [Test]
        public void Validate_RejectsUnknownType()
        {
            var json =
                "{\"protocolVersion\":2,\"type\":\"unknown.type\",\"messageId\":\"" +
                Guid.NewGuid().ToString("N") +
                "\",\"sentAtUtc\":\"" + DateTimeOffset.UtcNow.ToString("o") +
                "\",\"payload\":{}}";

            Assert.IsTrue(ProtocolJson.TryDeserializeEnvelope(json, out var envelope, out _));
            var result = ProtocolEnvelopeValidator.Validate(envelope, 100, false, DateTimeOffset.UtcNow);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(ErrorCodes.ProtocolInvalidMessage, result.ErrorCode);
        }

        [Test]
        public void Validate_RejectsOversizedMessage()
        {
            var typed = ProtocolJson.Create(
                ProtocolConstants.MessageTypes.DeviceHeartbeat,
                new DeviceHeartbeatPayload
                {
                    Sequence = 1,
                    ClientTimeUtc = DateTimeOffset.UtcNow,
                    DeviceState = "Online"
                },
                "dev");

            var json = ProtocolJson.SerializeToString(typed);
            Assert.IsTrue(ProtocolJson.TryDeserializeEnvelope(json, out var envelope, out _));

            var result = ProtocolEnvelopeValidator.Validate(
                envelope,
                ProtocolConstants.MaxMessageBytes + 1,
                true,
                DateTimeOffset.UtcNow);
            Assert.IsFalse(result.IsValid);
        }
    }
}
