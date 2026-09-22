#if WOI_VRBRIDGE_CLIENT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Auth;
using Woi.VrBridge.Client.Connection;
namespace Woi.Game.Training.VrBridge
{
    /// <summary>
    /// Fire Training ↔ WOI VR Bridge adapter.
    /// Publishes Bridge local results first, then best-effort Hub upload under the redeemed Firebase UID.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FireBridgeSessionAdapter : MonoBehaviour
    {
        [SerializeField] ExtinguisherSessionRecorder _recorder;
        [SerializeField] VrBridgeClient _bridgeClient;
        [SerializeField] string _moduleId = "fire-training";
        [SerializeField] bool _notifyStartedOnRecorderStart = true;

        IVrBridgeClient _client;
        BridgeSessionContext _boundContext;
        string _lastPublishedResultId;
        string _lastStartedSessionId;
        bool _subscribed;

        public BridgeSessionContext BoundContext => _boundContext;

        public void Bind(IVrBridgeClient client, BridgeSessionContext context)
        {
            _client = client;
            _boundContext = context;
            ApplyParticipantToLoginSession(context);
            TryResolveRecorder();
            Subscribe();
        }

        public void EnsureBoundClient(IVrBridgeClient client)
        {
            if (client != null)
            {
                _client = client;
            }
        }

        public void TryResolveRecorder()
        {
            if (_recorder == null)
            {
                _recorder = FindFirstObjectByType<ExtinguisherSessionRecorder>();
            }

            if (_recorder != null && !_subscribed)
            {
                Subscribe();
            }
        }

        void Awake()
        {
            if (_bridgeClient != null)
            {
                _client = _bridgeClient;
            }

            TryResolveRecorder();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Unsubscribe();
        }

        void OnEnable()
        {
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryResolveRecorder();
        }

        void Subscribe()
        {
            if (_subscribed || _recorder == null)
            {
                return;
            }

            _recorder.OnSessionStarted += HandleRecorderStarted;
            _recorder.OnSessionEnded += HandleRecorderEnded;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed || _recorder == null)
            {
                return;
            }

            _recorder.OnSessionStarted -= HandleRecorderStarted;
            _recorder.OnSessionEnded -= HandleRecorderEnded;
            _subscribed = false;
        }

        void HandleRecorderStarted()
        {
            if (!_notifyStartedOnRecorderStart || _client == null || _boundContext == null)
            {
                return;
            }

            if (string.Equals(_lastStartedSessionId, _boundContext.SessionId, StringComparison.Ordinal))
            {
                return;
            }

            _lastStartedSessionId = _boundContext.SessionId;
            Debug.Log($"[FireBridge] Module ready — sending session.started sessionId={_boundContext.SessionId}");
            _client.NotifySessionStarted(_boundContext.SessionId, _boundContext.ModuleId ?? _moduleId);
        }

        void HandleRecorderEnded(SessionReport report)
        {
            if (_client == null || report == null)
            {
                return;
            }

            var sessionId = _boundContext?.SessionId ?? report.Client?.SessionId;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            PublishCompletionAsync(sessionId, report, this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid PublishCompletionAsync(string sessionId, SessionReport report, CancellationToken cancellationToken)
        {
            try
            {
                var resultId = FireBridgeResultMapper.StableResultId(sessionId, report);
                if (string.Equals(_lastPublishedResultId, resultId, StringComparison.Ordinal))
                {
                    Debug.Log($"[FireBridge] Duplicate completion ignored resultId={resultId}");
                    return;
                }

                var body = FireBridgeResultMapper.Map(report, _boundContext?.AuthorizationId);

                // Durable Bridge outbox first — must succeed even if Hub is offline.
                await _client.QueueResultAsync(resultId, sessionId, _boundContext?.ModuleId ?? _moduleId, body, cancellationToken);
                _lastPublishedResultId = resultId;
                var pending = ((IVrBridgeResultPublisher)_client).PendingResultCount;
                Debug.Log($"[FireBridge] Result queued resultId={resultId} sessionId={sessionId} pending={pending}");

                await TryUploadHubResultAsync(sessionId, report, cancellationToken);

                _client.NotifySessionCompleted(sessionId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FireBridge] Result publish failed independently of gameplay: {ex.Message}");
                try
                {
                    _client.NotifySessionCompleted(sessionId);
                }
                catch
                {
                    // ignore
                }
            }
        }

        async UniTask TryUploadHubResultAsync(string sessionId, SessionReport report, CancellationToken cancellationToken)
        {
            try
            {
                var auth = _client?.CustomerAuthSession;
                var config = (_client as VrBridgeClient)?.Config?.FirebaseAuth;
                if (auth == null || !auth.IsAuthenticated || config == null || !config.IsConfigured)
                {
                    return;
                }

                var summary = report.Client;
                var rulesEvaluated = summary?.OverallTrainingPassed != null;
                var request = new HubModuleRunResultRequest
                {
                    ModuleId = _boundContext?.ModuleId ?? _moduleId,
                    SessionId = sessionId,
                    Platform = "vr",
                    DeviceId = _boundContext?.DeviceId,
                    ModuleVersion = Application.version,
                    Result = new HubModuleRunResultBody
                    {
                        TraineeId = _boundContext?.PersonnelId,
                        TraineeName = _boundContext?.ParticipantFullName,
                        ResultStatus = "completed",
                        Score = summary != null ? Math.Clamp(summary.FinalScore, 0d, 100d) : 0d,
                        OverallTrainingPassed = rulesEvaluated ? summary.OverallTrainingPassed : null,
                        RulesEvaluated = rulesEvaluated,
                        DurationSeconds = summary != null ? Math.Max(0d, summary.SessionDurationSeconds) : 0d,
                        ClientCompletedAt = DateTimeOffset.UtcNow.ToString("o")
                    }
                };

                var uploader = new HubModuleResultUploader(config, auth);
                await uploader.TrySubmitAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FireBridge] Hub result upload skipped: {ex.Message}");
            }
        }

        static void ApplyParticipantToLoginSession(BridgeSessionContext context)
        {
            if (context == null)
            {
                return;
            }

            Debug.Log(
                $"[FireBridge] Participant bound sessionId={context.SessionId} " +
                $"nameLength={context.ParticipantFullName?.Length ?? 0} personnelIdLength={context.PersonnelId?.Length ?? 0}");
        }
    }
}
#endif
