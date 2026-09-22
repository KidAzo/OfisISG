using System;
using System.Collections;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Connection;
using Woi.VrBridge.Client.Protocol;
using Woi.VrBridge.Client.Protocol.Messages;
using Woi.VrBridge.Client.Storage;
using Woi.VrBridge.Client.Threading;
using Woi.VrBridge.Client.Tests.Support;

namespace Woi.VrBridge.Client.Tests.PlayMode
{
    /// <summary>
    /// Live Editor integration against a local Bridge service (127.0.0.1:17880 / 17881).
    /// Skips when Bridge health is unavailable so CI without Bridge still passes.
    /// </summary>
    public sealed class VrBridgeLiveIntegrationTests
    {
        const string ControlApi = "http://127.0.0.1:17880";
        const string Gateway = "ws://127.0.0.1:17881/ws/device";

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Live_Pair_Heartbeat_Reconnect_Session_Result()
        {
            return UniTask.ToCoroutine(async () =>
            {
                if (!await IsBridgeHealthyAsync())
                {
                    Assert.Ignore("Bridge service not running at 127.0.0.1:17880 — start scripts/run-service.ps1");
                }

                // Auth retries emit safe warnings; do not fail the test on those logs.
                LogAssert.ignoreFailingMessages = true;

                var root = System.IO.Path.Combine(Application.temporaryCachePath, "vrbridge-live-" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(root);

                var go = new GameObject("VrBridgeLiveTest");
                var client = go.AddComponent<VrBridgeClient>();
                var config = ScriptableObject.CreateInstance<BridgeClientConfig>();
                config.DisplayName = "Editor Live Test";
                config.AppVersion = "2c-live";
                config.GatewayUrlOverride = Gateway;
                config.AllowLoopbackManualEndpoint = true;
                config.AllowManualHostFallback = true;
                config.SupportedModuleIds = new[] { "fire-training", "default" };
                config.DefaultHeartbeatIntervalMilliseconds = 2000;
                config.DefaultLocale = "en";

                var credentials = new MemoryCredentialStore();
                var identity = new DeviceIdentityStore(root);
                var launcher = new RecordingLauncher();
                var authSession = new FakeVrCustomerAuthSession();

                client.Initialize(
                    config,
                    credentialStore: credentials,
                    identityStore: identity,
                    dispatcher: VrBridgeMainThreadDispatcher.Instance,
                    moduleLauncher: launcher,
                    authSession: authSession);

                await client.InitializeAsync(CancellationToken.None);
                await client.ConnectAsync(CancellationToken.None);

                await WaitUntilAsync(() => client.State == VrBridgeConnectionState.PairingRequired
                                          || client.State == VrBridgeConnectionState.Connected, 20f);

                if (client.State == VrBridgeConnectionState.PairingRequired
                    || client.State == VrBridgeConnectionState.Authenticating
                    || !credentials.HasToken)
                {
                    var code = await CreatePairingCodeAsync();
                    Assert.False(string.IsNullOrWhiteSpace(code), "Pairing code empty");
                    Debug.Log("[VRBridge-Live] Pairing code acquired (not logged)");
                    await client.SubmitPairingCodeAsync(code, CancellationToken.None);
                }

                await WaitUntilAsync(() => client.State == VrBridgeConnectionState.Connected, 30f);
                Assert.AreEqual(VrBridgeConnectionState.Connected, client.State);
                Assert.IsTrue(credentials.HasToken, "Token should be stored after device.accepted");
                Debug.Log($"[VRBridge-Live] Paired deviceIdShort={(client.DeviceId?.Length >= 8 ? client.DeviceId.Substring(0, 8) : client.DeviceId)}");

                var startHb = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - startHb < 60f)
                {
                    Assert.AreEqual(VrBridgeConnectionState.Connected, client.State, "Heartbeat connection dropped");
                    await UniTask.Delay(2000);
                }

                Debug.Log($"[VRBridge-Live] Heartbeat stable 60s seq={client.HeartbeatSequence} rtt={client.ApproximateRttMilliseconds}");

                await client.DisconnectAsync(CancellationToken.None);
                await UniTask.Delay(500);
                await client.ConnectAsync(CancellationToken.None);
                await WaitUntilAsync(() => client.State == VrBridgeConnectionState.Connected, 30f);
                Assert.AreEqual(VrBridgeConnectionState.Connected, client.State);
                Assert.IsTrue(credentials.HasToken, "Token reconnect must not require a new pairing code");
                Debug.Log("[VRBridge-Live] Token reconnect OK");

                BridgeSessionContext authorized = null;
                client.AuthorizationReady += ctx => authorized = ctx;

                var prepared = await PrepareAuthorizationAsync(client.DeviceId, "Ada Lovelace", "P-1001");
                Assert.False(string.IsNullOrWhiteSpace(prepared.SessionId), "sessionId empty");
                Assert.False(string.IsNullOrWhiteSpace(prepared.AuthorizationId), "authorizationId empty");

                await WaitUntilAsync(() => authorized != null, 20f);
                Assert.AreEqual(0, launcher.LaunchCount, "Authorization must not auto-launch the module");
                Assert.IsTrue(client.CanStartAuthorizedTraining, "WaitingForPlayer / Start should be available");
                Debug.Log($"[VRBridge-Live] Authorized WaitingForPlayer sessionId={prepared.SessionId}");

                // Prove no auto-launch for a short wait window.
                await UniTask.Delay(3000);
                Assert.AreEqual(0, launcher.LaunchCount, "Module must still not launch while waiting for player");

                var started = await client.StartAuthorizedTrainingAsync(CancellationToken.None);
                Assert.IsTrue(started, "Manual Start should launch exactly once");
                Assert.AreEqual(1, launcher.LaunchCount);
                Assert.AreEqual(prepared.SessionId, launcher.LastContext.SessionId);

                var secondStart = await client.StartAuthorizedTrainingAsync(CancellationToken.None);
                Assert.IsFalse(secondStart, "Duplicate Start must be rejected");
                Assert.AreEqual(1, launcher.LaunchCount);

                client.NotifySessionStarted(prepared.SessionId, "fire-training");
                Debug.Log($"[VRBridge-Live] session.started sessionId={prepared.SessionId}");

                await client.QueueResultAsync(
                    "live-result-" + prepared.SessionId,
                    prepared.SessionId,
                    "fire-training",
                    new ResultBodyPayload { Outcome = "pass", Score = "1", DurationSeconds = 12 },
                    CancellationToken.None);

                client.NotifySessionCompleted(prepared.SessionId);
                await UniTask.Delay(4000);
                Debug.Log($"[VRBridge-Live] Result queued/submitted pending={client.PendingResultCount}");

                await client.QueueResultAsync(
                    "live-result-" + prepared.SessionId,
                    prepared.SessionId,
                    "fire-training",
                    new ResultBodyPayload { Outcome = "pass", Score = "1", DurationSeconds = 12 },
                    CancellationToken.None);

                Debug.Log("[VRBridge-Live] Duplicate result enqueue tolerated");
                UnityEngine.Object.Destroy(go);
                LogAssert.ignoreFailingMessages = false;
            });
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator Live_InvalidPairingCode_ShowsAuthFailure()
        {
            return UniTask.ToCoroutine(async () =>
            {
                if (!await IsBridgeHealthyAsync())
                {
                    Assert.Ignore("Bridge service not running");
                }

                LogAssert.ignoreFailingMessages = true;

                var go = new GameObject("VrBridgeLiveInvalidPair");
                var client = go.AddComponent<VrBridgeClient>();
                var config = ScriptableObject.CreateInstance<BridgeClientConfig>();
                config.GatewayUrlOverride = Gateway;
                config.AllowLoopbackManualEndpoint = true;
                config.SupportedModuleIds = new[] { "default" };
                client.Initialize(config, credentialStore: new MemoryCredentialStore(), dispatcher: VrBridgeMainThreadDispatcher.Instance);
                await client.InitializeAsync(CancellationToken.None);
                await client.ConnectAsync(CancellationToken.None);
                await WaitUntilAsync(() => client.State == VrBridgeConnectionState.PairingRequired, 20f);
                await client.SubmitPairingCodeAsync("000000", CancellationToken.None);
                // Wait for auth attempt to finish — do not treat the pre-submit PairingRequired as success.
                await WaitUntilAsync(
                    () => client.State == VrBridgeConnectionState.AuthenticationFailed
                          || client.LastErrorCode == "VRB-AUTH-012"
                          || client.LastErrorCode == "VRB-PAIR-018",
                    20f);
                Assert.IsFalse(string.IsNullOrEmpty(client.LastErrorCode), "Expected error code for invalid pairing");
                Debug.Log($"[VRBridge-Live] Invalid pairing errorCode={client.LastErrorCode}");
                UnityEngine.Object.Destroy(go);
                LogAssert.ignoreFailingMessages = false;
            });
        }

        static async UniTask<string> CreatePairingCodeAsync()
        {
            // Bridge rate-limits pairing create (default 10/min). Retry quietly on 429.
            for (var attempt = 0; attempt < 8; attempt++)
            {
                using var req = new UnityWebRequest(ControlApi + "/api/v1/pairing/codes", UnityWebRequest.kHttpVerbPOST);
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 5;
                await SendWebRequestNoThrowAsync(req);
                if (req.responseCode == 429)
                {
                    Debug.Log("[VRBridge-Live] Pairing create rate-limited; waiting before retry");
                    await UniTask.Delay(8000);
                    continue;
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception("Create pairing code failed: " + req.responseCode + " " + req.downloadHandler?.text);
                }

                var root = JObject.Parse(req.downloadHandler.text);
                return root.SelectToken("data.pairingCode")?.ToString()
                       ?? root.SelectToken("data.PairingCode")?.ToString()
                       ?? root.SelectToken("pairingCode")?.ToString();
            }

            throw new Exception("Create pairing code failed: rate limited after retries");
        }

        sealed class PreparedAuthorization
        {
            public string AuthorizationId;
            public string SessionId;
        }

        static async UniTask<PreparedAuthorization> PrepareAuthorizationAsync(
            string deviceId,
            string fullName,
            string personnelId)
        {
            var draftBody = new JObject
            {
                ["deviceId"] = deviceId,
                ["moduleId"] = "fire-training",
                ["firebaseOperatorUid"] = "playmode-operator",
                ["participant"] = new JObject
                {
                    ["fullName"] = fullName,
                    ["personnelId"] = personnelId
                },
                ["requestedAtUtc"] = DateTimeOffset.UtcNow.ToString("o")
            };

            var draftRoot = await PostJsonAsync("/api/v1/training-authorizations/drafts", draftBody);
            var authorizationId = draftRoot.SelectToken("data.authorizationId")?.ToString();
            var sessionId = draftRoot.SelectToken("data.sessionId")?.ToString();
            if (string.IsNullOrWhiteSpace(authorizationId) || string.IsNullOrWhiteSpace(sessionId))
            {
                throw new Exception("Draft authorization missing ids: " + draftRoot);
            }

            // Opaque dummy ticket — FakeVrCustomerAuthSession redeems without calling Hub.
            var authorizeBody = new JObject
            {
                ["firebaseAuthorizationTicket"] = "playmode-one-time-ticket-" + Guid.NewGuid().ToString("N"),
                ["ticketExpiresAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(2).ToString("o")
            };
            await PostJsonAsync(
                "/api/v1/training-authorizations/" + Uri.EscapeDataString(authorizationId) + "/authorize",
                authorizeBody);

            return new PreparedAuthorization
            {
                AuthorizationId = authorizationId,
                SessionId = sessionId
            };
        }

        static async UniTask<JObject> PostJsonAsync(string path, JObject body)
        {
            var bytes = Encoding.UTF8.GetBytes(body.ToString());
            using var req = new UnityWebRequest(ControlApi + path, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 15;
            await SendWebRequestNoThrowAsync(req);
            var json = req.downloadHandler?.text;
            if (req.result != UnityWebRequest.Result.Success)
            {
                throw new Exception("POST " + path + " failed: " + req.responseCode + " " + json);
            }

            return JObject.Parse(json ?? "{}");
        }

        static async UniTask SendWebRequestNoThrowAsync(UnityWebRequest req)
        {
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                await UniTask.Yield();
            }
        }

        static async UniTask<bool> IsBridgeHealthyAsync()
        {
            try
            {
                using var req = UnityWebRequest.Get(ControlApi + "/api/v1/health");
                req.timeout = 3;
                await SendWebRequestNoThrowAsync(req);
                return req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300;
            }
            catch
            {
                return false;
            }
        }

        static async UniTask WaitUntilAsync(Func<bool> predicate, float timeoutSeconds)
        {
            var start = Time.realtimeSinceStartup;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup - start > timeoutSeconds)
                {
                    throw new TimeoutException("WaitUntil timed out");
                }

                await UniTask.Delay(200);
            }
        }

        sealed class RecordingLauncher : IBridgeModuleLauncher
        {
            public int LaunchCount;
            public BridgeSessionContext LastContext;

            public bool CanLaunch(string moduleId) => true;

            public UniTask LaunchSessionAsync(BridgeSessionContext context, CancellationToken cancellationToken)
            {
                LaunchCount++;
                LastContext = context;
                return UniTask.CompletedTask;
            }

            public UniTask CancelSessionAsync(string sessionId, CancellationToken cancellationToken) => UniTask.CompletedTask;
        }
    }
}
