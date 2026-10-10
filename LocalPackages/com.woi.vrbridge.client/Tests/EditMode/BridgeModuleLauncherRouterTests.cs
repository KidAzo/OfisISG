using System;
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
using Woi.VrBridge.Client.Tests.Support;

namespace Woi.VrBridge.Client.Tests.EditMode
{
    public sealed class BridgeModuleLauncherRouterTests
    {
        const string DeviceId = "device-under-test";

        static BridgeSessionContext Context(string moduleId, string sessionId = "sess-1")
        {
            return new BridgeSessionContext(
                sessionId,
                DeviceId,
                moduleId,
                "msg-1",
                new SessionParticipantPayload { FullName = "Ada Lovelace", PersonnelId = "PN-001" },
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                DateTimeOffset.UtcNow,
                "connection-1",
                1,
                "auth-1");
        }

        [Test]
        public void Router_DispatchesByModuleId_AndEarlierRegistrationWins()
        {
            var first = new FakeBridgeModuleLauncher("fire-training", "default");
            var second = new FakeBridgeModuleLauncher("office-waste", "default");
            var router = new BridgeModuleLauncherRouter();
            router.Register(first);
            router.Register(second);

            Assert.AreSame(first, router.Resolve("fire-training"));
            Assert.AreSame(second, router.Resolve("office-waste"));
            Assert.AreSame(first, router.Resolve("default"), "The first registered launcher keeps a shared module id.");
            Assert.IsTrue(router.CanLaunch("office-waste"));
            Assert.IsFalse(router.CanLaunch("office-hazard"));
            Assert.IsFalse(router.CanLaunch(""));
            Assert.IsFalse(router.CanLaunch(null));
        }

        [Test]
        public void Router_RegisterIgnoresDuplicates_AndRejectsItself()
        {
            var launcher = new FakeBridgeModuleLauncher("office-waste");
            var router = new BridgeModuleLauncherRouter();
            router.Register(launcher);
            router.Register(launcher);

            Assert.AreEqual(1, router.Count);
            Assert.Throws<ArgumentException>(() => router.Register(router));
            Assert.Throws<ArgumentNullException>(() => router.Register(null));
        }

        [Test]
        public async Task Router_LaunchesOnlyTheMatchingLauncher()
        {
            var fire = new FakeBridgeModuleLauncher("fire-training", "default");
            var waste = new FakeBridgeModuleLauncher("office-waste");
            var router = new BridgeModuleLauncherRouter();
            router.Register(fire);
            router.Register(waste);

            await router.LaunchSessionAsync(Context("office-waste"), CancellationToken.None);

            Assert.AreEqual(0, fire.LaunchCallCount);
            Assert.AreEqual(1, waste.LaunchCallCount);
            Assert.AreEqual("office-waste", waste.LastLaunchedContext.ModuleId);
        }

        [Test]
        public void Router_LaunchForUnknownModule_Throws()
        {
            var router = new BridgeModuleLauncherRouter();
            router.Register(new FakeBridgeModuleLauncher("fire-training"));

            Assert.Throws<InvalidOperationException>(() => router.LaunchSessionAsync(Context("unknown"), CancellationToken.None));
        }

        [Test]
        public async Task Router_CancelGoesToTheLauncherThatLaunched()
        {
            var fire = new FakeBridgeModuleLauncher("fire-training");
            var waste = new FakeBridgeModuleLauncher("office-waste");
            var router = new BridgeModuleLauncherRouter();
            router.Register(fire);
            router.Register(waste);
            await router.LaunchSessionAsync(Context("office-waste", "sess-w"), CancellationToken.None);

            await router.CancelSessionAsync("sess-w", CancellationToken.None);

            Assert.AreEqual(0, fire.CancelCallCount);
            Assert.AreEqual(1, waste.CancelCallCount);
            Assert.AreEqual("sess-w", waste.LastCancelledSessionId);
        }

        [Test]
        public async Task Router_CancelBeforeLaunch_ReachesEveryLauncher()
        {
            var fire = new FakeBridgeModuleLauncher("fire-training");
            var waste = new FakeBridgeModuleLauncher("office-waste");
            var router = new BridgeModuleLauncherRouter();
            router.Register(fire);
            router.Register(waste);

            await router.CancelSessionAsync("sess-pending", CancellationToken.None);

            Assert.AreEqual(1, fire.CancelCallCount);
            Assert.AreEqual(1, waste.CancelCallCount);
        }

        [Test]
        public void Router_Unregister_RemovesModule()
        {
            var waste = new FakeBridgeModuleLauncher("office-waste");
            var router = new BridgeModuleLauncherRouter();
            router.Register(waste);

            Assert.IsTrue(router.Unregister(waste));
            Assert.IsFalse(router.CanLaunch("office-waste"));
        }

        [Test]
        public async Task AuthorizationHandler_WithRouter_AcksASecondModule_AndKeepsTheFirst()
        {
            var config = ScriptableObject.CreateInstance<BridgeClientConfig>();
            config.SupportedModuleIds = new[] { "fire-training", "default", "office-waste" };
            var router = new BridgeModuleLauncherRouter();
            var fire = new FakeBridgeModuleLauncher("fire-training", "default");
            var waste = new FakeBridgeModuleLauncher("office-waste");
            router.Register(fire);
            router.Register(waste);

            SessionAuthorizationAckPayload wasteAck = await AuthorizeAsync(config, router, "office-waste");
            SessionAuthorizationAckPayload fireAck = await AuthorizeAsync(config, router, "fire-training");
            SessionAuthorizationAckPayload unknownAck = await AuthorizeAsync(config, router, "office-hazard");

            Assert.IsTrue(wasteAck.Accepted, wasteAck.RejectionCode);
            Assert.IsTrue(fireAck.Accepted, fireAck.RejectionCode);
            Assert.IsFalse(unknownAck.Accepted);
            Assert.AreEqual(ErrorCodes.AuthorizationModuleUnsupported, unknownAck.RejectionCode);
            Assert.AreEqual(0, fire.LaunchCallCount + waste.LaunchCallCount, "Authorization never launches a module.");
        }

        static async Task<SessionAuthorizationAckPayload> AuthorizeAsync(
            BridgeClientConfig config,
            IBridgeModuleLauncher launcher,
            string moduleId)
        {
            SessionAuthorizationAckPayload ack = null;
            var handler = new SessionAuthorizationHandler(
                config,
                launcher,
                new FakeVrCustomerAuthSession(),
                a => { ack = a; return UniTask.CompletedTask; },
                (sessionId, module) => UniTask.CompletedTask,
                context => { },
                (sessionId, reason) => { },
                (sessionId, reason) => { },
                () => DeviceId,
                () => "connection-1");

            const string fullName = "Ada Lovelace";
            const string personnelId = "PN-001";
            var command = new SessionAuthorizeCommandPayload
            {
                AuthorizationId = "auth-" + moduleId,
                SessionId = "sess-" + moduleId,
                DeviceId = DeviceId,
                ModuleId = moduleId,
                Participant = new SessionParticipantPayload { FullName = fullName, PersonnelId = personnelId },
                ParticipantHash = ParticipantHashUtility.Compute(fullName, personnelId),
                Ticket = "one-time-ticket",
                RequestedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
                AttemptNumber = 1
            };

            var typed = ProtocolJson.Create(ProtocolConstants.MessageTypes.SessionAuthorizeCommand, command, DeviceId);
            Assert.IsTrue(ProtocolJson.TryDeserializeEnvelope(ProtocolJson.SerializeToString(typed), out var envelope, out var error), error);
            await handler.HandleAuthorizeAsync(envelope, CancellationToken.None);
            Assert.IsNotNull(ack);
            return ack;
        }
    }
}
