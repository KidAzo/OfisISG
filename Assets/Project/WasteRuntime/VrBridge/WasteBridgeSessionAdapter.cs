using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Woi.VrBridge.Client.Abstractions;

namespace Woi.WasteCollectionMode.VrBridge
{
    /// <summary>
    /// Binds one Bridge session to one Waste run: session.started once Waste_Office is interactive, then the
    /// finalized result through the client's durable outbox, then session.completed and per-run cleanup.
    /// </summary>
    public sealed class WasteBridgeSessionAdapter : IDisposable
    {
        const string LogPrefix = "[WasteBridge]";

        readonly IVrBridgeClient _client;
        readonly Func<DateTimeOffset> _utcNow;

        BridgeSessionContext _context;
        DateTimeOffset? _gameplayStartedUtc;
        bool _startedSent;
        bool _completing;
        bool _disposed;

        public WasteBridgeSessionAdapter(IVrBridgeClient client, Func<DateTimeOffset> utcNow = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
            WasteBridgeRun.ResultsFinalized += OnResultsFinalized;
            _client.SessionCancelled += OnSessionCancelled;
        }

        public BridgeSessionContext BoundContext => _context;

        public DateTimeOffset? GameplayStartedUtc => _gameplayStartedUtc;

        public void Bind(BridgeSessionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (_context != null)
                Reset();

            _context = context;
            WasteBridgeRun.Begin(
                context.AuthorizationId,
                context.SessionId,
                WasteBridgeResultMapper.ModuleId,
                context.ParticipantFullName,
                context.PersonnelId);

            Debug.Log(
                $"{LogPrefix} Bound sessionId={context.SessionId} " +
                $"nameLength={context.ParticipantFullName?.Length ?? 0} personnelIdLength={context.PersonnelId?.Length ?? 0}");
        }

        public void MarkGameplayReady()
        {
            if (_context == null || _startedSent)
                return;

            _startedSent = true;
            _gameplayStartedUtc = _utcNow();
            Debug.Log($"{LogPrefix} Waste_Office is interactive — sending session.started sessionId={_context.SessionId}");
            _client.NotifySessionStarted(_context.SessionId, WasteBridgeResultMapper.ModuleId);
        }

        public async UniTask CompleteAsync(WasteRunResult result, CancellationToken cancellationToken = default)
        {
            BridgeSessionContext context = _context;
            if (context == null || result == null || _completing)
                return;

            _completing = true;
            try
            {
                string resultId = WasteBridgeResultMapper.StableResultId(context.SessionId);
                var body = WasteBridgeResultMapper.Map(result, context.AuthorizationId, ElapsedSeconds());
                await _client.QueueResultAsync(resultId, context.SessionId, WasteBridgeResultMapper.ModuleId, body, cancellationToken);
                Debug.Log($"{LogPrefix} Result queued resultId={resultId} pending={_client.PendingResultCount}");
            }
            catch (Exception ex)
            {
                // Anything the outbox accepted is retried by the client; completion is still reported below.
                Debug.LogWarning($"{LogPrefix} Result publish failed: {ex.Message}");
            }

            await UniTask.SwitchToMainThread();
            if (!ReferenceEquals(_context, context))
                return;

            try
            {
                _client.NotifySessionCompleted(context.SessionId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} session.completed could not be sent: {ex.Message}");
            }

            Reset();
        }

        public bool CancelIfBound(string sessionId)
        {
            if (_context == null
                || !string.Equals(_context.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Debug.Log($"{LogPrefix} Bridge cancelled sessionId={sessionId}; Waste run state cleared.");
            Reset();
            return true;
        }

        /// <summary>Drops the bound session and the Waste identity of this run. Device, pairing and outbox state stay.</summary>
        public void Reset()
        {
            _context = null;
            _gameplayStartedUtc = null;
            _startedSent = false;
            _completing = false;
            WasteBridgeRun.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            WasteBridgeRun.ResultsFinalized -= OnResultsFinalized;
            _client.SessionCancelled -= OnSessionCancelled;
        }

        int? ElapsedSeconds()
        {
            if (!_gameplayStartedUtc.HasValue)
                return null;

            double seconds = (_utcNow() - _gameplayStartedUtc.Value).TotalSeconds;
            return (int)Math.Round(Math.Max(0d, seconds), MidpointRounding.AwayFromZero);
        }

        void OnResultsFinalized(WasteRunResult result)
        {
            CompleteAsync(result).Forget();
        }

        void OnSessionCancelled(string sessionId)
        {
            CancelIfBound(sessionId);
        }
    }
}
