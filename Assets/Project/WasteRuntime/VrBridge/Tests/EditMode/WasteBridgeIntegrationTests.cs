using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Woi.DataHandler;
using Woi.Events.Data;
using Woi.OfficeFire.VrBridge;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Connection;
using Woi.VrBridge.Client.Protocol;
using Woi.VrBridge.Client.Protocol.Messages;
using Woi.VrBridge.Client.Storage;
using Woi.VrBridge.Client.Tests.Support;

namespace Woi.WasteCollectionMode.VrBridge.Tests
{
    public sealed class WasteBridgeIntegrationTests
    {
        const string ConfigAssetPath = "Assets/Project/Data/VRBridge/BridgeClientConfig.asset";
        const string SceneLoaderPrefabPath = "Assets/Project/Reflex/Resources_moved/SceneLoader.prefab";
        const string WasteOfficeScenePath = "Assets/Project/Scenes/WasteModule/Waste_Office.unity";
        const string DeviceId = "quest-under-test";

        sealed class ImmediateDispatcher : IMainThreadDispatcher
        {
            public void Enqueue(Action action) => action?.Invoke();

            public UniTask EnqueueAsync(Func<UniTask> action) => action != null ? action() : UniTask.CompletedTask;
        }

        readonly List<GameObject> _objects = new List<GameObject>();
        readonly List<IDisposable> _disposables = new List<IDisposable>();
        string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            ResetStatics();
            _tempRoot = Path.Combine(Path.GetTempPath(), "woi-waste-bridge-tests", Path.GetRandomFileName());
            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var disposable in _disposables)
                disposable.Dispose();
            _disposables.Clear();

            foreach (var go in _objects)
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }

            _objects.Clear();
            ResetStatics();

            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, true);
        }

        static void ResetStatics()
        {
            WasteBridgeRun.Clear();
            GameSessionData.Clear();
            WasteLoginSession.Clear();
            SessionManager.AutoTestSessionSuppressed = false;
        }

        static BridgeSessionContext Context(
            string sessionId = "sess-1",
            string authorizationId = "auth-1",
            string fullName = "Ayşe Yılmaz",
            string personnelId = "P-1001",
            string moduleId = "office-waste")
        {
            return new BridgeSessionContext(
                sessionId,
                DeviceId,
                moduleId,
                "msg-" + sessionId,
                new SessionParticipantPayload { FullName = fullName, PersonnelId = personnelId },
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                DateTimeOffset.UtcNow,
                "connection-1",
                1,
                authorizationId);
        }

        WasteBridgeSessionAdapter Adapter(IVrBridgeClient client, Func<DateTimeOffset> clock = null)
        {
            var adapter = new WasteBridgeSessionAdapter(client, clock);
            _disposables.Add(adapter);
            return adapter;
        }

        static WasteBridgeModuleLauncher Launcher(WasteBridgeSessionAdapter adapter, List<string> loadedGroups)
        {
            return new WasteBridgeModuleLauncher(
                adapter,
                (group, ct) =>
                {
                    loadedGroups.Add(group);
                    return UniTask.CompletedTask;
                },
                ct => UniTask.CompletedTask);
        }

        // A. office-waste is accepted by the shipped BridgeClientConfig.
        [Test]
        public void ConfigAsset_SupportsOfficeWaste_AndKeepsExistingModules()
        {
            var config = AssetDatabase.LoadAssetAtPath<BridgeClientConfig>(ConfigAssetPath);

            Assert.IsNotNull(config, ConfigAssetPath);
            CollectionAssert.Contains(config.SupportedModuleIds, "office-waste");
            CollectionAssert.Contains(config.SupportedModuleIds, "fire-training");
            CollectionAssert.Contains(config.SupportedModuleIds, "default");
            CollectionAssert.IsEmpty(config.Validate());
        }

        // B / C. The Waste launcher only claims office-waste.
        [Test]
        public void Launcher_CanLaunch_OnlyOfficeWaste()
        {
            var launcher = Launcher(Adapter(new FakeWasteBridgeClient()), new List<string>());

            Assert.IsTrue(launcher.CanLaunch("office-waste"));
            Assert.IsFalse(launcher.CanLaunch("fire-training"));
            Assert.IsFalse(launcher.CanLaunch("default"));
            Assert.IsFalse(launcher.CanLaunch("office-hazard"));
            Assert.IsFalse(launcher.CanLaunch("office-waste-new"));
            Assert.IsFalse(launcher.CanLaunch(""));
            Assert.IsFalse(launcher.CanLaunch(null));
        }

        [Test]
        public void Launcher_RejectsUnrelatedModuleContext()
        {
            var adapter = Adapter(new FakeWasteBridgeClient());
            var launcher = Launcher(adapter, new List<string>());

            Assert.Throws<InvalidOperationException>(() =>
                launcher.LaunchSessionAsync(Context(moduleId: "fire-training"), CancellationToken.None).GetAwaiter().GetResult());
            Assert.IsFalse(WasteBridgeRun.IsActive);
        }

        // D + O. Through the shell's router, office-waste is ACKed and fire-training still reaches Office Fire.
        [Test]
        public async Task Authorization_OfficeWaste_IsAcked_AndFireTrainingStillRoutesToOfficeFire()
        {
            var config = AssetDatabase.LoadAssetAtPath<BridgeClientConfig>(ConfigAssetPath);
            var officeFireGo = new GameObject("office-fire-launcher-under-test");
            _objects.Add(officeFireGo);
            var officeFire = officeFireGo.AddComponent<OfficeFireBridgeModuleLauncher>();
            var waste = Launcher(Adapter(new FakeWasteBridgeClient()), new List<string>());
            var router = new BridgeModuleLauncherRouter();
            router.Register(officeFire);
            router.Register(waste);

            SessionAuthorizationAckPayload wasteAck = await AuthorizeAsync(config, router, "office-waste");
            SessionAuthorizationAckPayload fireAck = await AuthorizeAsync(config, router, "fire-training");

            Assert.IsTrue(wasteAck.Accepted, wasteAck.RejectionCode);
            Assert.IsTrue(fireAck.Accepted, fireAck.RejectionCode);
            Assert.AreSame(waste, router.Resolve("office-waste"));
            Assert.AreSame(officeFire, router.Resolve("fire-training"));
            Assert.AreSame(officeFire, router.Resolve("default"));
            Assert.IsFalse(WasteBridgeRun.IsActive, "Authorization alone must not start a Waste run.");
        }

        // F (routing). The group the launcher loads exists in the shipped SceneLoader and activates the addressable Waste_Office.
        [Test]
        public void WasteOfficeGroup_IsDefinedInSceneLoader_AndAddressable()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SceneLoaderPrefabPath);
            Assert.IsNotNull(prefab, SceneLoaderPrefabPath);
            var loader = prefab.GetComponent<Woi.Settings.SceneLoader>();
            Assert.IsNotNull(loader);
            var groups = (Woi.Settings.SceneGroup[])typeof(Woi.Settings.SceneLoader)
                .GetField("sceneGroups", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(loader);

            var group = groups.Single(g => g.GroupName == WasteBridgeModuleLauncher.GameplaySceneGroup);
            Assert.AreEqual("Waste_Office", group.GroupName);
            var active = group.Scenes.Single(s => s.SceneType == Woi.Settings.SceneType.ActiveScene);
            Assert.AreEqual("Waste_Office", active.AddressableKey);
            Assert.IsFalse(groups.Single(g => g.GroupName == "WasteCollector").Scenes.Any(s => s.AddressableKey == "Waste_Office"),
                "Bridge mode must not route through the legacy WasteCollector / FireModule_Office group.");

            string sceneGuid = AssetDatabase.AssetPathToGUID(WasteOfficeScenePath);
            var entry = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(sceneGuid);
            Assert.IsNotNull(entry, WasteOfficeScenePath + " must be addressable.");
            Assert.AreEqual("Waste_Office", entry.address);
        }

        // E. BridgeSessionContext identity reaches the Waste runtime state.
        [Test]
        public async Task Launch_BindsBridgeIdentity_IntoWasteRuntime()
        {
            var launcher = Launcher(Adapter(new FakeWasteBridgeClient()), new List<string>());

            await launcher.LaunchSessionAsync(Context(), CancellationToken.None);

            Assert.IsTrue(WasteBridgeRun.IsActive);
            Assert.AreEqual("auth-1", WasteBridgeRun.AuthorizationId);
            Assert.AreEqual("sess-1", WasteBridgeRun.SessionId);
            Assert.AreEqual("office-waste", WasteBridgeRun.ModuleId);
            Assert.AreEqual("Ayşe Yılmaz", WasteBridgeRun.ParticipantName);
            Assert.AreEqual("P-1001", WasteBridgeRun.PersonnelId);
            Assert.AreEqual("Ayşe Yılmaz", GameSessionData.UserName);
            Assert.AreEqual("P-1001", GameSessionData.UserId);
            Assert.AreEqual("Ayşe Yılmaz", WasteLoginSession.UserName);
            Assert.AreEqual("P-1001", WasteLoginSession.UserId);

            WasteSessionResultCsvExporter.ResolveIdentity(out string name, out string id);
            Assert.AreEqual("Ayşe Yılmaz", name);
            Assert.AreEqual("P-1001", id);
            Assert.AreNotEqual("Test Oyuncu", name);
            Assert.AreNotEqual("9999", id);
        }

        // F + G. Through the real client: Start sends player-start, loads Waste_Office, then session.started once.
        [Test]
        public async Task PlayerStart_LoadsWasteOffice_AndSendsSessionStartedOnce()
        {
            var loadedGroups = new List<string>();
            var transport = new FakeWebSocketTransport();
            var go = new GameObject("waste-bridge-client-under-test");
            _objects.Add(go);
            var client = go.AddComponent<VrBridgeClient>();
            var config = ScriptableObject.CreateInstance<BridgeClientConfig>();
            config.GatewayUrlOverride = "ws://127.0.0.1:17881/ws/device";
            config.SupportedModuleIds = new[] { "fire-training", "default", "office-waste" };

            var router = new BridgeModuleLauncherRouter();
            router.Register(new FakeBridgeModuleLauncher("fire-training", "default"));
            var adapter = Adapter(client);
            var waste = Launcher(adapter, loadedGroups);
            router.Register(waste);

            client.Initialize(
                config,
                transport,
                new BridgeDiscoveryClient(config),
                new MemoryCredentialStore(),
                identityStore: new DeviceIdentityStore(_tempRoot),
                dispatcher: new ImmediateDispatcher(),
                moduleLauncher: router,
                authSession: new FakeVrCustomerAuthSession());
            await client.InitializeAsync(CancellationToken.None);
            await transport.ConnectAsync(new Uri(config.GatewayUrlOverride), CancellationToken.None);

            transport.EnqueueIncoming(AuthorizeJson(client.DeviceId, "office-waste"));
            Assert.IsTrue(client.CanStartAuthorizedTraining, "The prepared office-waste session must be startable.");
            Assert.IsEmpty(loadedGroups, "Authorization alone must not load gameplay.");
            Assert.IsFalse(WasteBridgeRun.IsActive);

            bool started = await client.StartAuthorizedTrainingAsync(CancellationToken.None);
            adapter.MarkGameplayReady();

            Assert.IsTrue(started);
            CollectionAssert.AreEqual(new[] { "Waste_Office" }, loadedGroups);
            List<string> sent = SentTypes(transport);
            Assert.AreEqual(1, sent.Count(t => t == ProtocolConstants.MessageTypes.SessionAuthorizationAck));
            Assert.AreEqual(1, sent.Count(t => t == ProtocolConstants.MessageTypes.SessionPlayerStartRequested));
            Assert.AreEqual(1, sent.Count(t => t == ProtocolConstants.MessageTypes.SessionStarted), "session.started is sent exactly once.");
            Assert.Less(
                sent.IndexOf(ProtocolConstants.MessageTypes.SessionPlayerStartRequested),
                sent.IndexOf(ProtocolConstants.MessageTypes.SessionStarted));
            Assert.IsTrue(AuthorizationAckAccepted(transport), "office-waste must be ACKed, not rejected.");
            Assert.IsTrue(WasteBridgeRun.IsActive);
            Assert.AreEqual("Ada Lovelace", WasteBridgeRun.ParticipantName);
        }

        [Test]
        public async Task SessionStarted_IsNotSent_WhenGameplayNeverBecomesReady()
        {
            var client = new FakeWasteBridgeClient();
            var adapter = Adapter(client);
            var launcher = new WasteBridgeModuleLauncher(
                adapter,
                (group, ct) => UniTask.CompletedTask,
                ct => throw new TimeoutException("Waste_Office did not become interactive."));

            Assert.Throws<TimeoutException>(() =>
                launcher.LaunchSessionAsync(Context(), CancellationToken.None).GetAwaiter().GetResult());
            await Task.Yield();

            Assert.IsEmpty(client.StartedSessions);
            Assert.IsFalse(WasteBridgeRun.IsActive, "A failed launch must not leave Bridge identity behind.");
        }

        // H / I / J / L. Completion queues exactly one V2 result for the Bridge session, completes once, then clears.
        [Test]
        public async Task Completion_QueuesOneResult_CompletesOnce_AndClearsRunState()
        {
            var clock = new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
            var now = clock;
            var client = new FakeWasteBridgeClient();
            var adapter = Adapter(client, () => now);
            var launcher = Launcher(adapter, new List<string>());

            await launcher.LaunchSessionAsync(Context(), CancellationToken.None);
            now = clock.AddSeconds(95.4);

            WasteBridgeRun.PublishResults(new WasteRunResult(correct: 8, incorrect: 2, notFound: 3));
            WasteBridgeRun.PublishResults(new WasteRunResult(correct: 8, incorrect: 2, notFound: 3));
            await WaitUntil(() => client.CompletedSessions.Count > 0);

            Assert.AreEqual(1, client.StartedSessions.Count);
            Assert.AreEqual(("sess-1", "office-waste"), client.StartedSessions[0]);
            Assert.AreEqual(1, client.QueuedResults.Count, "Exactly one V2 result is queued.");
            var queued = client.QueuedResults[0];
            Assert.AreEqual("sess-1", queued.SessionId);
            Assert.AreEqual("office-waste", queued.ModuleId);
            Assert.AreEqual("waste-result-sess-1", queued.ResultId);
            Assert.AreEqual("fail", queued.Body.Outcome);
            Assert.AreEqual("80", queued.Body.Score);
            Assert.AreEqual(95, queued.Body.DurationSeconds);
            Assert.AreEqual("auth-1", queued.Body.Attributes["authorizationId"]);
            Assert.AreEqual("8", queued.Body.Attributes["correct"]);
            Assert.AreEqual("2", queued.Body.Attributes["incorrect"]);
            Assert.AreEqual("10", queued.Body.Attributes["total"]);
            Assert.AreEqual("3", queued.Body.Attributes["notFound"]);
            Assert.AreEqual("percent", queued.Body.Attributes["scoreScale"]);

            CollectionAssert.AreEqual(new[] { "sess-1" }, client.CompletedSessions, "session.completed is sent exactly once.");
            CollectionAssert.AreEqual(
                new[] { "session.started", "result.submit", "session.completed" },
                client.Calls,
                "The result is queued before session.completed.");

            Assert.IsFalse(WasteBridgeRun.IsActive);
            Assert.IsEmpty(WasteBridgeRun.SessionId);
            Assert.IsEmpty(WasteBridgeRun.AuthorizationId);
            Assert.IsEmpty(WasteBridgeRun.ModuleId);
            Assert.IsEmpty(WasteBridgeRun.ParticipantName);
            Assert.IsEmpty(WasteBridgeRun.PersonnelId);
            Assert.IsFalse(GameSessionData.IsSet);
            Assert.IsFalse(WasteLoginSession.IsSet);
            Assert.IsNull(adapter.BoundContext);
            Assert.IsFalse(SessionManager.AutoTestSessionSuppressed);
        }

        [Test]
        public async Task Completion_StillReportsSessionCompleted_WhenTheOutboxThrows()
        {
            var client = new FakeWasteBridgeClient { ThrowOnQueue = true };
            var adapter = Adapter(client);
            await Launcher(adapter, new List<string>()).LaunchSessionAsync(Context(), CancellationToken.None);

            await adapter.CompleteAsync(new WasteRunResult(5, 0, 0));

            CollectionAssert.AreEqual(new[] { "sess-1" }, client.CompletedSessions);
            Assert.IsFalse(WasteBridgeRun.IsActive);
        }

        [Test]
        public void Mapper_PassesOnlyWithoutWrongBins_AndUsesThePercentScore()
        {
            var pass = WasteBridgeResultMapper.Map(new WasteRunResult(4, 0, 2), "auth-x", 30);
            var fail = WasteBridgeResultMapper.Map(new WasteRunResult(2, 1, 0), null, null);
            var empty = WasteBridgeResultMapper.Map(new WasteRunResult(0, 0, 7), "auth-y", 5);

            Assert.AreEqual("pass", pass.Outcome);
            Assert.AreEqual("100", pass.Score);
            Assert.AreEqual(30, pass.DurationSeconds);
            Assert.AreEqual("fail", fail.Outcome);
            Assert.AreEqual("67", fail.Score);
            Assert.IsNull(fail.DurationSeconds);
            Assert.IsFalse(fail.Attributes.ContainsKey("authorizationId"));
            Assert.AreEqual("fail", empty.Outcome, "Finishing without classifying anything is not a pass.");
            Assert.AreEqual("0", empty.Score);
            Assert.AreEqual("7", empty.Attributes["notFound"]);
            Assert.AreEqual("waste-result-sess-9", WasteBridgeResultMapper.StableResultId(" sess-9 "));
            Assert.Throws<ArgumentException>(() => WasteBridgeResultMapper.StableResultId(""));
        }

        [Test]
        public void Mapper_Result_IsAcceptedByTheDurableOutbox()
        {
            var outbox = new ResultOutbox(8, _tempRoot);
            var body = WasteBridgeResultMapper.Map(new WasteRunResult(3, 1, 0), "auth-1", 12);

            outbox.Enqueue(WasteBridgeResultMapper.StableResultId("sess-1"), "sess-1", WasteBridgeResultMapper.ModuleId, body);
            outbox.Enqueue(WasteBridgeResultMapper.StableResultId("sess-1"), "sess-1", WasteBridgeResultMapper.ModuleId, body);

            Assert.AreEqual(1, outbox.Count, "The stable resultId makes a repeated completion idempotent.");
            var entry = outbox.Snapshot()[0];
            Assert.AreEqual("office-waste", entry.ModuleId);
            Assert.AreEqual("sess-1", entry.SessionId);
            Assert.IsTrue(File.Exists(Path.Combine(_tempRoot, "result-outbox.json")));
        }

        // K. The legacy /save-result POST is not allowed during a Bridge run.
        [Test]
        public void LegacySaveResult_IsSkipped_DuringBridgeRun_AndNoSessionManagerIsCreated()
        {
            WasteBridgeRun.Begin("auth-1", "sess-1", "office-waste", "Ayşe Yılmaz", "P-1001");

            Assert.IsFalse(WasteSessionResultCsvExporter.ShouldUploadToPcServer());
            Assert.IsTrue(SessionManager.AutoTestSessionSuppressed, "No auto test session during a Bridge run.");
            Assert.IsFalse(SessionManager.HasInstance);
            Assert.IsNull(UnityEngine.Object.FindFirstObjectByType<SessionManager>(FindObjectsInactive.Include));

            WasteBridgeRun.Clear();
            Assert.IsFalse(SessionManager.AutoTestSessionSuppressed);
        }

        // M. A second participant does not inherit anything from the first.
        [Test]
        public async Task SecondParticipant_DoesNotInheritTheFirst()
        {
            var client = new FakeWasteBridgeClient();
            var adapter = Adapter(client);
            var launcher = Launcher(adapter, new List<string>());

            await launcher.LaunchSessionAsync(Context("sess-a", "auth-a", "Ali Veli", "A-1"), CancellationToken.None);
            await adapter.CompleteAsync(new WasteRunResult(1, 0, 0));
            Assert.IsFalse(WasteBridgeRun.IsActive);

            await launcher.LaunchSessionAsync(Context("sess-b", "auth-b", "Zeynep Kaya", "B-2"), CancellationToken.None);
            WasteSessionResultCsvExporter.ResolveIdentity(out string name, out string id);
            Assert.AreEqual("Zeynep Kaya", name);
            Assert.AreEqual("B-2", id);
            Assert.AreEqual("sess-b", WasteBridgeRun.SessionId);
            Assert.AreEqual("auth-b", WasteBridgeRun.AuthorizationId);

            await adapter.CompleteAsync(new WasteRunResult(2, 0, 0));

            Assert.AreEqual(2, client.QueuedResults.Count);
            var second = client.QueuedResults[1];
            Assert.AreEqual("sess-b", second.SessionId);
            Assert.AreEqual("waste-result-sess-b", second.ResultId);
            Assert.AreEqual("auth-b", second.Body.Attributes["authorizationId"]);
            CollectionAssert.AreEqual(new[] { "sess-a", "sess-b" }, client.CompletedSessions);
            CollectionAssert.AreEqual(
                new[] { ("sess-a", "office-waste"), ("sess-b", "office-waste") },
                client.StartedSessions);
        }

        [Test]
        public async Task BridgeCancel_ClearsRunState_WithoutPublishingAResult()
        {
            var client = new FakeWasteBridgeClient();
            var adapter = Adapter(client);
            await Launcher(adapter, new List<string>()).LaunchSessionAsync(Context(), CancellationToken.None);

            client.RaiseSessionCancelled("sess-1");
            WasteBridgeRun.PublishResults(new WasteRunResult(3, 0, 0));
            await Task.Yield();

            Assert.IsFalse(WasteBridgeRun.IsActive);
            Assert.IsNull(adapter.BoundContext);
            Assert.IsFalse(GameSessionData.IsSet);
            Assert.IsEmpty(client.QueuedResults);
            Assert.IsEmpty(client.CompletedSessions);
        }

        // N. Manual login identity works without the Bridge and is never wiped by Bridge cleanup.
        [Test]
        public void ManualLogin_WorksWithoutBridge_AndBridgeCleanupLeavesItAlone()
        {
            WasteLoginSession.Set("Manuel Kullanıcı", "M-77", "tr");
            GameSessionData.Set(new List<FireExtinguisher.Core.FireClass>(), "Manuel Kullanıcı", "M-77");

            Assert.IsFalse(WasteBridgeRun.IsActive);
            WasteSessionResultCsvExporter.ResolveIdentity(out string name, out string id);
            Assert.AreEqual("Manuel Kullanıcı", name);
            Assert.AreEqual("M-77", id);

            WasteBridgeRun.Clear();

            Assert.IsTrue(WasteLoginSession.IsSet);
            Assert.IsTrue(GameSessionData.IsSet);
            Assert.AreEqual("M-77", GameSessionData.UserId);
            Assert.IsFalse(SessionManager.AutoTestSessionSuppressed);
        }

        static async Task WaitUntil(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                    Assert.Fail("Timed out waiting for the adapter.");

                await Task.Delay(10);
            }
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

            Assert.IsTrue(ProtocolJson.TryDeserializeEnvelope(AuthorizeJson(DeviceId, moduleId), out var envelope, out var error), error);
            await handler.HandleAuthorizeAsync(envelope, CancellationToken.None);
            Assert.IsNotNull(ack);
            return ack;
        }

        static string AuthorizeJson(string deviceId, string moduleId)
        {
            const string fullName = "Ada Lovelace";
            const string personnelId = "PN-001";
            var command = new SessionAuthorizeCommandPayload
            {
                AuthorizationId = "auth-" + moduleId,
                SessionId = "sess-" + moduleId,
                DeviceId = deviceId,
                ModuleId = moduleId,
                Participant = new SessionParticipantPayload { FullName = fullName, PersonnelId = personnelId },
                ParticipantHash = ParticipantHashUtility.Compute(fullName, personnelId),
                Ticket = "one-time-ticket",
                RequestedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
                AttemptNumber = 1
            };

            var typed = ProtocolJson.Create(ProtocolConstants.MessageTypes.SessionAuthorizeCommand, command, deviceId);
            return ProtocolJson.SerializeToString(typed);
        }

        static List<string> SentTypes(FakeWebSocketTransport transport)
        {
            var types = new List<string>();
            foreach (byte[] frame in transport.Sent)
            {
                if (ProtocolJson.TryDeserializeEnvelope(Encoding.UTF8.GetString(frame), out var envelope, out _))
                    types.Add(envelope.Type);
            }

            return types;
        }

        static bool AuthorizationAckAccepted(FakeWebSocketTransport transport)
        {
            foreach (byte[] frame in transport.Sent)
            {
                if (!ProtocolJson.TryDeserializeEnvelope(Encoding.UTF8.GetString(frame), out var envelope, out _)
                    || envelope.Type != ProtocolConstants.MessageTypes.SessionAuthorizationAck)
                {
                    continue;
                }

                var ack = ProtocolJson.DeserializePayload<SessionAuthorizationAckPayload>(envelope.Payload);
                return ack != null && ack.Accepted;
            }

            return false;
        }
    }
}
