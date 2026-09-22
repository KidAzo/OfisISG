using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Woi.VrBridge.Client.Connection;
using Woi.VrBridge.Client.Protocol;
using Woi.VrBridge.Client.Protocol.Messages;

namespace Woi.VrBridge.Client.Tests.EditMode
{
    public sealed class SessionAndIdempotencyTests
    {
        string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "woi-vrbridge-tests", Path.GetRandomFileName());
            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        [Test]
        public void IdempotencyStore_DetectsConflict()
        {
            var store = new CommandIdempotencyStore(16, _tempRoot);
            store.Record("cmd1", "sess1", "hash-a");
            store.Record("cmd2", "sess1", "hash-b");

            Assert.IsTrue(store.TryGetBySession("sess1", out var entry));
            Assert.AreEqual("hash-b", entry.PayloadHash);
        }

        [Test]
        public void ResultOutbox_ComputesStableHash()
        {
            var body = new ResultBodyPayload { Outcome = "pass", Score = "100", DurationSeconds = 10 };
            var json = ProtocolJson.SerializePayload(body);
            var hash1 = ResultOutbox.ComputeSha256Hex(json);
            var hash2 = ResultOutbox.ComputeSha256Hex(json);
            Assert.AreEqual(hash1, hash2);
        }

        [Test]
        public void ResultOutbox_DuplicateResultId_ReusesEntry()
        {
            var outbox = new ResultOutbox(5, _tempRoot);
            var body = new ResultBodyPayload { Outcome = "pass", Score = "1", DurationSeconds = 3 };
            outbox.Enqueue("r1", "s1", "fire-training", body);
            outbox.Enqueue("r1", "s1", "fire-training", body);
            Assert.AreEqual(1, outbox.Count);
        }

        [Test]
        public async Task SessionCommandHandler_RejectsDeprecatedStartCommand_WithoutTrackingState()
        {
            SessionCommandAckPayload lastAck = null;
            var handler = new SessionCommandHandler(ack =>
            {
                lastAck = ack;
                return UniTask.CompletedTask;
            });

            var command = new SessionStartCommandPayload
            {
                SessionId = "sess1",
                ModuleId = "default",
                Participant = new SessionParticipantPayload { FullName = "A", PersonnelId = "1" },
                RequestedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
                AttemptNumber = 1
            };

            await handler.HandleSessionStartAsync(
                ToUntyped(ProtocolJson.Create(ProtocolConstants.MessageTypes.SessionStartCommand, command, "dev")),
                CancellationToken.None);

            Assert.IsNotNull(lastAck);
            Assert.IsFalse(lastAck.Accepted);
            Assert.AreEqual(ErrorCodes.SessionStartDeprecated, lastAck.RejectionCode);
            Assert.AreEqual("sess1", lastAck.SessionId);
        }

        static ProtocolEnvelope ToUntyped<T>(ProtocolEnvelope<T> typed) where T : class
        {
            var json = ProtocolJson.SerializeToString(typed);
            Assert.IsTrue(ProtocolJson.TryDeserializeEnvelope(json, out var envelope, out var error), error);
            return envelope;
        }
    }
}
