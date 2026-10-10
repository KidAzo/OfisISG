using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Protocol.Messages;

namespace Woi.WasteCollectionMode.VrBridge.Tests
{
    /// <summary>Records the lifecycle calls the Waste adapter makes; never touches the real result outbox file.</summary>
    public sealed class FakeWasteBridgeClient : IVrBridgeClient
    {
        public sealed class QueuedResult
        {
            public string ResultId;
            public string SessionId;
            public string ModuleId;
            public ResultBodyPayload Body;
        }

        public readonly List<string> Calls = new List<string>();
        public readonly List<QueuedResult> QueuedResults = new List<QueuedResult>();
        public readonly List<(string SessionId, string ModuleId)> StartedSessions = new List<(string, string)>();
        public readonly List<string> CompletedSessions = new List<string>();
        public bool ThrowOnQueue { get; set; }

        public event Action<string> SessionCancelled;

        public void RaiseSessionCancelled(string sessionId) => SessionCancelled?.Invoke(sessionId);

        public void NotifySessionStarted(string sessionId, string moduleId)
        {
            Calls.Add("session.started");
            StartedSessions.Add((sessionId, moduleId));
        }

        public void NotifySessionCompleted(string sessionId)
        {
            Calls.Add("session.completed");
            CompletedSessions.Add(sessionId);
        }

        public void NotifySessionFailed(string sessionId, string failureCode, string detail)
        {
            Calls.Add("session.failed");
        }

        public UniTask QueueResultAsync(
            string resultId,
            string sessionId,
            string moduleId,
            ResultBodyPayload body,
            CancellationToken cancellationToken)
        {
            Calls.Add("result.submit");
            if (ThrowOnQueue)
                throw new InvalidOperationException("Simulated outbox failure.");

            QueuedResults.Add(new QueuedResult { ResultId = resultId, SessionId = sessionId, ModuleId = moduleId, Body = body });
            return UniTask.CompletedTask;
        }

        public int PendingResultCount => QueuedResults.Count;
        public UniTask RetryPendingResultsAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;

        public string DeviceId => "fake-device";
        public IVrCustomerAuthSession CustomerAuthSession => null;
        public VrBridgeConnectionState State => VrBridgeConnectionState.Connected;
        public BridgeSessionContext ActiveSession => null;
        public BridgeSessionContext AuthorizedSession => null;
        public bool CanStartAuthorizedTraining => false;
        public string LastErrorCode => null;
        public string LastErrorMessage => null;
        public string ConnectionId => "fake-connection";
        public string GatewayUrl => null;
        public string ServiceId => null;
        public long HeartbeatSequence => 0;
        public double? ApproximateRttMilliseconds => null;
        public int ReconnectAttempt => 0;

        public event Action<VrBridgeConnectionState, string> StateChanged { add { } remove { } }
        public event Action<BridgeSessionContext> SessionStarted { add { } remove { } }
        public event Action<string> SessionCompleted { add { } remove { } }
        public event Action<string, string> SessionFailed { add { } remove { } }
        public event Action<BridgeSessionContext> AuthorizationReady { add { } remove { } }
        public event Action<string, string> AuthorizationRevoked { add { } remove { } }
        public event Action<string, string> AuthorizationExpired { add { } remove { } }

        public BridgeDiagnosticsSnapshot CreateSnapshot() => new BridgeDiagnosticsSnapshot();
        public UniTask<bool> StartAuthorizedTrainingAsync(CancellationToken cancellationToken) => UniTask.FromResult(false);
        public UniTask InitializeAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public UniTask ConnectAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public UniTask DisconnectAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public UniTask ReconnectAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public UniTask RequestRepairAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public UniTask SubmitPairingCodeAsync(string pairingCode, CancellationToken cancellationToken) => UniTask.CompletedTask;
        public UniTask ResetIdentityAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;
    }
}
