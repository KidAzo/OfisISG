using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Connection;
using Woi.VrBridge.Client.Protocol;
using Woi.VrBridge.Client.Protocol.Messages;
using Woi.VrBridge.Client.Storage;
using Woi.VrBridge.Client.Tests.Support;

namespace Woi.VrBridge.Client.Tests.EditMode
{
    /// <summary>
    /// Phase 2D coverage: authorize validation, participant hashing, expiry, idempotency, and —
    /// most importantly — proof that neither the deprecated start command nor a successful
    /// authorization ever reaches <see cref="IBridgeModuleLauncher.LaunchSessionAsync"/>. Only an
    /// explicit <see cref="VrBridgeClient.StartAuthorizedTrainingAsync"/> call (the player pressing
    /// Start Training) may do that, and at most once per authorization.
    /// </summary>
    public sealed class SessionAuthorizationTests
    {
        sealed class ImmediateDispatcher : IMainThreadDispatcher
        {
            public void Enqueue(Action action) => action?.Invoke();

            public UniTask EnqueueAsync(Func<UniTask> action) => action != null ? action() : UniTask.CompletedTask;
        }

        const string DeviceId = "device-under-test";
        const string ModuleId = "fire-training";

        string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "woi-vrbridge-authz-tests", Path.GetRandomFileName());
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

        // ---------------------------------------------------------------
        // Participant hash utility
        // ---------------------------------------------------------------

        [Test]
        public void ParticipantHash_IsDeterministic_AndMatchesBridgeRules()
        {
            // Name case preserved; personnel ID uppercased — matches Bridge ParticipantHasher.
            var a = ParticipantHashUtility.Compute("  Ada   Lovelace ", "pn-001");
            var b = ParticipantHashUtility.Compute("Ada Lovelace", "PN-001");

            Assert.AreEqual(a, b, "Whitespace/personnel-id casing must normalize identically.");
            Assert.AreNotEqual(
                ParticipantHashUtility.Compute("ada lovelace", "PN-001"),
                b,
                "Full-name casing is significant (Bridge ParticipantHasher).");
            Assert.IsTrue(ParticipantHashUtility.Matches("Ada Lovelace", "PN-001", a));
        }

        [Test]
        public void ParticipantHash_RejectsTamperedPersonnelId()
        {
            var hash = ParticipantHashUtility.Compute("Ada Lovelace", "PN-001");
            Assert.IsFalse(ParticipantHashUtility.Matches("Ada Lovelace", "PN-002", hash));
        }

        // ---------------------------------------------------------------
        // SessionAuthorizationHandler — unit level (no Unity objects needed except config asset)
        // ---------------------------------------------------------------

        static ProtocolEnvelope BuildAuthorizeEnvelope(SessionAuthorizeCommandPayload command, string deviceId = DeviceId)
        {
            var typed = ProtocolJson.Create(ProtocolConstants.MessageTypes.SessionAuthorizeCommand, command, deviceId);
            var json = ProtocolJson.SerializeToString(typed);
            Assert.IsTrue(ProtocolJson.TryDeserializeEnvelope(json, out var envelope, out var error), error);
            return envelope;
        }

        static SessionAuthorizeCommandPayload ValidCommand(string authorizationId = "auth-1", string sessionId = "sess-1")
        {
            const string fullName = "Ada Lovelace";
            const string personnelId = "PN-001";
            return new SessionAuthorizeCommandPayload
            {
                AuthorizationId = authorizationId,
                SessionId = sessionId,
                DeviceId = DeviceId,
                ModuleId = ModuleId,
                Participant = new SessionParticipantPayload { FullName = fullName, PersonnelId = personnelId },
                ParticipantHash = ParticipantHashUtility.Compute(fullName, personnelId),
                Ticket = "one-time-ticket",
                RequestedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
                AttemptNumber = 1
            };
        }

        [Test]
        public async Task AuthorizationHandler_AcceptsValidCommand_AndNeverCallsLauncher()
        {
            BridgeSessionContext ready = null;
            var config = ScriptableObject.CreateInstance<BridgeClientConfig>();
            config.SupportedModuleIds = new[] { ModuleId, "default" };
            var launcher = new FakeBridgeModuleLauncher(ModuleId, "default");
            var authSession = new FakeVrCustomerAuthSession();
            SessionAuthorizationAckPayload lastAck = null;

            var handler = new SessionAuthorizationHandler(
                config, launcher, authSession,
                ack => { lastAck = ack; return UniTask.CompletedTask; },
                (sessionId, moduleId) => UniTask.CompletedTask,
                context => ready = context,
                (sessionId, reason) => { },
                (sessionId, reason) => { },
                () => DeviceId,
                () => "connection-1");

            await handler.HandleAuthorizeAsync(BuildAuthorizeEnvelope(ValidCommand()), CancellationToken.None);

            Assert.IsNotNull(lastAck);
            Assert.IsTrue(lastAck.Accepted, "Valid authorize command must be accepted.");
            Assert.IsNotNull(ready, "AuthorizationReady must fire once accepted.");
            Assert.IsNotNull(handler.ActiveAuthorization);
            Assert.AreEqual("auth-1", handler.ActiveAuthorization.AuthorizationId);
            Assert.AreEqual(1, authSession.RedeemCallCount, "Ticket must be redeemed exactly once.");

            // The critical product rule: authorization NEVER launches the module.
            Assert.AreEqual(0, launcher.LaunchCallCount, "SessionAuthorizationHandler must never call LaunchSessionAsync.");
        }

        [Test]
        public async Task AuthorizationHandler_RejectsDeviceMismatch()
        {
            var (handler, launcher, _) = CreateHandlerWithAck(out var ackRef);
            var envelope = BuildAuthorizeEnvelope(ValidCommand(), deviceId: "some-other-device");

            await handler.HandleAuthorizeAsync(envelope, CancellationToken.None);

            Assert.IsFalse(ackRef.Value.Accepted);
            Assert.AreEqual(ErrorCodes.AuthorizationDeviceMismatch, ackRef.Value.RejectionCode);
            Assert.AreEqual(0, launcher.LaunchCallCount);
        }

        [Test]
        public async Task AuthorizationHandler_RejectsUnsupportedModule()
        {
            var (handler, launcher, _) = CreateHandlerWithAck(out var ackRef);
            var command = ValidCommand();
            command.ModuleId = "unknown-module";

            await handler.HandleAuthorizeAsync(BuildAuthorizeEnvelope(command), CancellationToken.None);

            Assert.IsFalse(ackRef.Value.Accepted);
            Assert.AreEqual(ErrorCodes.AuthorizationModuleUnsupported, ackRef.Value.RejectionCode);
            Assert.AreEqual(0, launcher.LaunchCallCount);
        }

        [Test]
        public async Task AuthorizationHandler_RejectsInvalidParticipant()
        {
            var (handler, launcher, _) = CreateHandlerWithAck(out var ackRef);
            var command = ValidCommand();
            command.Participant = new SessionParticipantPayload { FullName = "", PersonnelId = "" };

            await handler.HandleAuthorizeAsync(BuildAuthorizeEnvelope(command), CancellationToken.None);

            Assert.IsFalse(ackRef.Value.Accepted);
            Assert.AreEqual(ErrorCodes.AuthorizationParticipantInvalid, ackRef.Value.RejectionCode);
            Assert.AreEqual(0, launcher.LaunchCallCount);
        }

        [Test]
        public async Task AuthorizationHandler_RejectsParticipantHashMismatch()
        {
            var (handler, launcher, _) = CreateHandlerWithAck(out var ackRef);
            var command = ValidCommand();
            command.ParticipantHash = ParticipantHashUtility.Compute("Someone Else", "PN-999");

            await handler.HandleAuthorizeAsync(BuildAuthorizeEnvelope(command), CancellationToken.None);

            Assert.IsFalse(ackRef.Value.Accepted);
            Assert.AreEqual(ErrorCodes.AuthorizationHashMismatch, ackRef.Value.RejectionCode);
            Assert.AreEqual(0, launcher.LaunchCallCount);
        }

        [Test]
        public async Task AuthorizationHandler_RejectsExpiredCommand()
        {
            var (handler, launcher, _) = CreateHandlerWithAck(out var ackRef);
            var command = ValidCommand();
            command.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);

            await handler.HandleAuthorizeAsync(BuildAuthorizeEnvelope(command), CancellationToken.None);

            Assert.IsFalse(ackRef.Value.Accepted);
            Assert.AreEqual(ErrorCodes.AuthorizationExpired, ackRef.Value.RejectionCode);
            Assert.AreEqual(0, launcher.LaunchCallCount);
        }

        [Test]
        public async Task AuthorizationHandler_RejectsConflictingAuthorization()
        {
            var (handler, launcher, _) = CreateHandlerWithAck(out var ackRef);

            await handler.HandleAuthorizeAsync(BuildAuthorizeEnvelope(ValidCommand("auth-1", "sess-1")), CancellationToken.None);
            Assert.IsTrue(ackRef.Value.Accepted);

            await handler.HandleAuthorizeAsync(BuildAuthorizeEnvelope(ValidCommand("auth-2", "sess-2")), CancellationToken.None);

            Assert.IsFalse(ackRef.Value.Accepted);
            Assert.AreEqual(ErrorCodes.AuthorizationConflict, ackRef.Value.RejectionCode);
            Assert.AreEqual("auth-1", handler.ActiveAuthorization.AuthorizationId, "First authorization must remain active.");
            Assert.AreEqual(0, launcher.LaunchCallCount);
        }

        [Test]
        public async Task AuthorizationHandler_IsIdempotentForDuplicateCommand()
        {
            var (handler, launcher, authSession) = CreateHandlerWithAck(out var ackRef);
            var command = ValidCommand();

            await handler.HandleAuthorizeAsync(BuildAuthorizeEnvelope(command), CancellationToken.None);
            Assert.IsTrue(ackRef.Value.Accepted);
            Assert.AreEqual(1, authSession.RedeemCallCount);

            // Exact same command redelivered (e.g. Bridge retried after a dropped ack).
            await handler.HandleAuthorizeAsync(BuildAuthorizeEnvelope(command), CancellationToken.None);

            Assert.IsTrue(ackRef.Value.Accepted);
            Assert.AreEqual(1, authSession.RedeemCallCount, "Duplicate authorize must not redeem the ticket twice.");
            Assert.AreEqual(0, launcher.LaunchCallCount);
        }

        [Test]
        public async Task AuthorizationHandler_CanStartTraining_FalseOnceExpired()
        {
            var (handler, _, _) = CreateHandlerWithAck(out var ackRef);
            var command = ValidCommand();
            command.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5);

            await handler.HandleAuthorizeAsync(BuildAuthorizeEnvelope(command), CancellationToken.None);
            Assert.IsTrue(ackRef.Value.Accepted);

            Assert.IsTrue(handler.CanStartTraining(DateTimeOffset.UtcNow), "Should be startable before expiry.");
            Assert.IsFalse(handler.CanStartTraining(DateTimeOffset.UtcNow.AddMinutes(10)), "Must be disabled once expired.");
        }

        [Test]
        public async Task AuthorizationHandler_TryBeginStart_OnlySucceedsOnce()
        {
            var (handler, launcher, _) = CreateHandlerWithAck(out var ackRef);
            await handler.HandleAuthorizeAsync(BuildAuthorizeEnvelope(ValidCommand()), CancellationToken.None);
            Assert.IsTrue(ackRef.Value.Accepted);

            Assert.IsTrue(handler.TryBeginStart(), "First start attempt should succeed.");
            Assert.IsFalse(handler.TryBeginStart(), "Second concurrent/duplicate start attempt must be rejected.");
            Assert.AreEqual(0, launcher.LaunchCallCount, "The handler itself never launches — only the client does, and only after TryBeginStart succeeds.");
        }

        sealed class AckBox
        {
            public SessionAuthorizationAckPayload Value;
        }

        (SessionAuthorizationHandler handler, FakeBridgeModuleLauncher launcher, FakeVrCustomerAuthSession authSession)
            CreateHandlerWithAck(out AckBox ackBox)
        {
            var config = ScriptableObject.CreateInstance<BridgeClientConfig>();
            config.SupportedModuleIds = new[] { ModuleId, "default" };
            var launcher = new FakeBridgeModuleLauncher(ModuleId, "default");
            var authSession = new FakeVrCustomerAuthSession();
            var box = new AckBox();

            var handler = new SessionAuthorizationHandler(
                config, launcher, authSession,
                ack => { box.Value = ack; return UniTask.CompletedTask; },
                (sessionId, moduleId) => UniTask.CompletedTask,
                context => { },
                (sessionId, reason) => { },
                (sessionId, reason) => { },
                () => DeviceId,
                () => "connection-1");

            ackBox = box;
            return (handler, launcher, authSession);
        }

        // ---------------------------------------------------------------
        // VrBridgeClient — full pipeline, proves no auto-launch end to end
        // ---------------------------------------------------------------

        (VrBridgeClient client, GameObject go, FakeWebSocketTransport transport, FakeBridgeModuleLauncher launcher, FakeVrCustomerAuthSession authSession)
            CreateClient()
        {
            var go = new GameObject("session-authz-test-client");
            var client = go.AddComponent<VrBridgeClient>();
            var config = ScriptableObject.CreateInstance<BridgeClientConfig>();
            config.GatewayUrlOverride = "ws://127.0.0.1:17881/ws/device";
            config.SupportedModuleIds = new[] { ModuleId, "default" };

            var transport = new FakeWebSocketTransport();
            var launcher = new FakeBridgeModuleLauncher(ModuleId, "default");
            var authSession = new FakeVrCustomerAuthSession();

            client.Initialize(
                config,
                transport,
                new BridgeDiscoveryClient(config),
                new MemoryCredentialStore(),
                identityStore: new DeviceIdentityStore(_tempRoot),
                dispatcher: new ImmediateDispatcher(),
                moduleLauncher: launcher,
                authSession: authSession);

            return (client, go, transport, launcher, authSession);
        }

        [Test]
        public async Task Client_SessionStartCommand_IsRejectedWithoutLaunchingModule()
        {
            var (client, go, transport, launcher, _) = CreateClient();
            try
            {
                await client.InitializeAsync(CancellationToken.None);
                await transport.ConnectAsync(new Uri("ws://127.0.0.1:17881/ws/device"), CancellationToken.None);

                var command = new SessionStartCommandPayload
                {
                    SessionId = "sess-deprecated",
                    ModuleId = ModuleId,
                    Participant = new SessionParticipantPayload { FullName = "Ada Lovelace", PersonnelId = "PN-001" },
                    RequestedAtUtc = DateTimeOffset.UtcNow,
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
                    AttemptNumber = 1
                };
                var envelope = ProtocolJson.Create(ProtocolConstants.MessageTypes.SessionStartCommand, command, client.DeviceId);
                transport.EnqueueIncoming(ProtocolJson.SerializeToString(envelope));

                Assert.AreEqual(0, launcher.LaunchCallCount, "session.start.command must NEVER trigger a launch — the game must never auto-open.");
                Assert.IsNull(client.ActiveSession, "No session context should be tracked for the deprecated command.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public async Task Client_AuthorizeCommand_RaisesReadyEvent_WithoutLaunchingModule()
        {
            var (client, go, transport, launcher, authSession) = CreateClient();
            try
            {
                await client.InitializeAsync(CancellationToken.None);
                await transport.ConnectAsync(new Uri("ws://127.0.0.1:17881/ws/device"), CancellationToken.None);

                BridgeSessionContext raised = null;
                client.AuthorizationReady += ctx => raised = ctx;

                var command = ValidCommand();
                command.DeviceId = client.DeviceId;
                var envelope = ProtocolJson.Create(ProtocolConstants.MessageTypes.SessionAuthorizeCommand, command, client.DeviceId);
                transport.EnqueueIncoming(ProtocolJson.SerializeToString(envelope));

                Assert.IsNotNull(raised, "AuthorizationReady must fire once the authorize command is accepted.");
                Assert.AreEqual(1, authSession.RedeemCallCount);
                Assert.IsTrue(client.CanStartAuthorizedTraining);

                // The critical product rule, proven at the full-client level: authorizing never launches.
                Assert.AreEqual(0, launcher.LaunchCallCount, "Authorization alone must never call LaunchSessionAsync.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public async Task Client_StartAuthorizedTrainingAsync_DoubleInvoke_LaunchesExactlyOnce()
        {
            var (client, go, transport, launcher, _) = CreateClient();
            try
            {
                await client.InitializeAsync(CancellationToken.None);
                await transport.ConnectAsync(new Uri("ws://127.0.0.1:17881/ws/device"), CancellationToken.None);

                var command = ValidCommand();
                command.DeviceId = client.DeviceId;
                var envelope = ProtocolJson.Create(ProtocolConstants.MessageTypes.SessionAuthorizeCommand, command, client.DeviceId);
                transport.EnqueueIncoming(ProtocolJson.SerializeToString(envelope));
                Assert.IsTrue(client.CanStartAuthorizedTraining, "Precondition: authorization must be ready before the player presses Start.");

                var first = await client.StartAuthorizedTrainingAsync(CancellationToken.None);
                var second = await client.StartAuthorizedTrainingAsync(CancellationToken.None);

                Assert.IsTrue(first, "First press should launch.");
                Assert.IsFalse(second, "A duplicate press (double tap / re-entrant call) must be rejected.");
                Assert.AreEqual(1, launcher.LaunchCallCount, "Module launcher must be invoked exactly once, only after the explicit player start.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
