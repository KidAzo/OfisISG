using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Auth;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Diagnostics;
using Woi.VrBridge.Client.Protocol;
using Woi.VrBridge.Client.Protocol.Messages;
using Woi.VrBridge.Client.Storage;
using Woi.VrBridge.Client.Threading;

namespace Woi.VrBridge.Client.Connection
{
    [DefaultExecutionOrder(-500)]
    public sealed class VrBridgeClient : MonoBehaviour, IVrBridgeClient
    {
        BridgeClientConfig _config;
        IBridgeWebSocketTransport _transport;
        IBridgeDiscoveryClient _discovery;
        IDeviceCredentialStore _credentialStore;
        DeviceIdentityStore _identityStore;
        IMainThreadDispatcher _dispatcher;
        IBridgeModuleLauncher _moduleLauncher;
        IVrCustomerAuthSession _authSession;

        ReconnectPolicy _reconnectPolicy;
        HeartbeatService _heartbeat;
        CommandIdempotencyStore _idempotencyStore;
        ResultOutbox _resultOutbox;
        SessionCommandHandler _sessionHandler;
        SessionAuthorizationHandler _authorizationHandler;

        CancellationTokenSource _runCts;
        string _deviceId;
        string _connectionId;
        string _serviceId;
        string _gatewayUrl;
        string _connectionInstanceId;
        string _activeSessionId;
        string _activeModuleId;
        string _pendingPairingCode;
        string _lastKnownSessionId;
        int _malformedCount;
        int _heartbeatIntervalMs = 2000;
        int _missedHeartbeatAcks;
        long _lastHeartbeatSequence;
        DateTimeOffset? _lastHeartbeatSentUtc;
        DateTimeOffset? _lastHeartbeatAckUtc;
        double? _approximateRttMs;
        bool _initialized;
        bool _repairRequested;

        VrBridgeConnectionState _state = VrBridgeConnectionState.Uninitialized;
        string _lastErrorCode;
        string _lastErrorMessage;

        UniTaskCompletionSource<DeviceAcceptedPayload> _helloAcceptedTcs;
        UniTaskCompletionSource<bool> _helloRejectedTcs;

        public string DeviceId => _deviceId;
        public IVrCustomerAuthSession CustomerAuthSession => _authSession;
        public BridgeClientConfig Config => _config;
        public VrBridgeConnectionState State => _state;
        public BridgeSessionContext ActiveSession => _authorizationHandler?.ActiveAuthorization;
        public BridgeSessionContext AuthorizedSession => _authorizationHandler?.ActiveAuthorization;
        public bool CanStartAuthorizedTraining => _authorizationHandler?.CanStartTraining(DateTimeOffset.UtcNow) ?? false;
        public string LastErrorCode => _lastErrorCode;
        public string LastErrorMessage => _lastErrorMessage;
        public string ConnectionId => _connectionId;
        public string GatewayUrl => _gatewayUrl;
        public string ServiceId => _serviceId;
        public long HeartbeatSequence => _heartbeat?.Sequence ?? 0;
        public double? ApproximateRttMilliseconds => _approximateRttMs;
        public int ReconnectAttempt => _reconnectPolicy?.Attempt ?? 0;
        public int PendingResultCount => _resultOutbox?.Count ?? 0;

        public event Action<VrBridgeConnectionState, string> StateChanged;
        public event Action<BridgeSessionContext> SessionStarted;
        public event Action<string> SessionCompleted;
        public event Action<string, string> SessionFailed;
        public event Action<string> SessionCancelled;
        public event Action<BridgeSessionContext> AuthorizationReady;
        public event Action<string, string> AuthorizationRevoked;
        public event Action<string, string> AuthorizationExpired;

        public void Initialize(
            BridgeClientConfig config,
            IBridgeWebSocketTransport transport = null,
            IBridgeDiscoveryClient discovery = null,
            IDeviceCredentialStore credentialStore = null,
            DeviceIdentityStore identityStore = null,
            IMainThreadDispatcher dispatcher = null,
            IBridgeModuleLauncher moduleLauncher = null,
            IVrCustomerAuthSession authSession = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _dispatcher = dispatcher ?? VrBridgeMainThreadDispatcher.Instance;
            _transport = transport ?? new ClientWebSocketTransport(_dispatcher, _config.MaxSendQueueSize);
            _discovery = discovery ?? new BridgeDiscoveryClient(_config);
            _credentialStore = credentialStore ?? DeviceCredentialStoreFactory.Create();
            _identityStore = identityStore ?? new DeviceIdentityStore();
            _moduleLauncher = moduleLauncher ?? new DefaultBridgeModuleLauncher(_config.SupportedModuleIds);
            _authSession = authSession ?? CreateDefaultAuthSession();

            _reconnectPolicy = new ReconnectPolicy(_config);
            _idempotencyStore = new CommandIdempotencyStore(_config.IdempotencyStoreMaxEntries);
            _resultOutbox = new ResultOutbox(_config.ResultOutboxMaxRetries);
            _heartbeat = new HeartbeatService(() => SendHeartbeatAsync(_runCts?.Token ?? CancellationToken.None), _heartbeatIntervalMs);

            _sessionHandler = new SessionCommandHandler(SendCommandAckAsync);

            _authorizationHandler = new SessionAuthorizationHandler(
                _config,
                _moduleLauncher,
                _authSession,
                SendAuthorizationAckAsync,
                SetActiveSessionAsync,
                context => AuthorizationReady?.Invoke(context),
                (sessionId, reason) => AuthorizationRevoked?.Invoke(sessionId, reason),
                (sessionId, reason) => AuthorizationExpired?.Invoke(sessionId, reason),
                () => _deviceId,
                () => _connectionId);

            _transport.MessageReceived += OnTransportMessage;
            _transport.Closed += OnTransportClosed;
            _initialized = true;
            SetState(VrBridgeConnectionState.Disconnected, "initialized");
        }

        IVrCustomerAuthSession CreateDefaultAuthSession()
        {
            if (_config.FirebaseAuth != null && _config.FirebaseAuth.IsConfigured)
            {
                return new VrCustomerAuthSession(_config.FirebaseAuth, FirebaseAuthTokenStoreFactory.Create());
            }

            return new NullVrCustomerAuthSession();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (_transport != null)
            {
                _transport.MessageReceived -= OnTransportMessage;
                _transport.Closed -= OnTransportClosed;
            }

            _heartbeat?.Dispose();
            _runCts?.Cancel();
            _runCts?.Dispose();
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            if (_config == null)
            {
                throw new InvalidOperationException("Call Initialize(...) before InitializeAsync.");
            }

            _deviceId = await _identityStore.GetOrCreateDeviceIdAsync();
            _connectionInstanceId = Guid.NewGuid().ToString("N");
            SetState(VrBridgeConnectionState.Disconnected, "ready");
            await UniTask.CompletedTask;
        }

        public UniTask ConnectAsync(CancellationToken cancellationToken) => StartRunLoopAsync(cancellationToken);

        public async UniTask DisconnectAsync(CancellationToken cancellationToken)
        {
            _runCts?.Cancel();
            _heartbeat?.Stop();
            if (_transport != null)
            {
                await _transport.DisconnectAsync(cancellationToken);
            }

            SetState(VrBridgeConnectionState.Disconnected, "user-disconnect");
        }

        public UniTask ReconnectAsync(CancellationToken cancellationToken)
        {
            _reconnectPolicy.Reset();
            return StartRunLoopAsync(cancellationToken);
        }

        public async UniTask RequestRepairAsync(CancellationToken cancellationToken)
        {
            _repairRequested = true;
            await _credentialStore.DeleteTokenAsync(cancellationToken);
            _pendingPairingCode = null;
            await ReconnectAsync(cancellationToken);
        }

        async UniTask StartRunLoopAsync(CancellationToken cancellationToken)
        {
            if (!_initialized || _config == null)
            {
                throw new InvalidOperationException("VrBridgeClient is not initialized.");
            }

            if (string.IsNullOrWhiteSpace(_deviceId))
            {
                await InitializeAsync(cancellationToken);
            }

            _runCts?.Cancel();
            _runCts?.Dispose();
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _reconnectPolicy.Reset();
            RunLoop(_runCts.Token).Forget();
            await UniTask.CompletedTask;
        }

        public UniTask SubmitPairingCodeAsync(string pairingCode, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(pairingCode))
            {
                SetError(ErrorCodes.PairingCodeInvalid, BridgeDiagnosticCatalog.Localize(ErrorCodes.PairingCodeInvalid, _config.DefaultLocale));
                return UniTask.CompletedTask;
            }

            _pendingPairingCode = pairingCode.Trim();
            return UniTask.CompletedTask;
        }

        public async UniTask ResetIdentityAsync(CancellationToken cancellationToken)
        {
            await DisconnectAsync(cancellationToken);
            await _credentialStore.DeleteTokenAsync(cancellationToken);
            await _identityStore.ResetAsync();
            _idempotencyStore.Clear();
            _deviceId = await _identityStore.GetOrCreateDeviceIdAsync();
            _connectionInstanceId = Guid.NewGuid().ToString("N");
            _lastKnownSessionId = null;
            _activeSessionId = null;
            _authorizationHandler?.ClearAuthorization();
            SetState(VrBridgeConnectionState.Disconnected, "identity-reset");
        }

        public void NotifySessionStarted(string sessionId, string moduleId)
        {
            SendLifecycleAsync(ProtocolConstants.MessageTypes.SessionStarted, sessionId, moduleId, null, null).Forget();
            SessionStarted?.Invoke(_authorizationHandler?.ActiveAuthorization);
        }

        public void NotifySessionCompleted(string sessionId)
        {
            SendLifecycleAsync(ProtocolConstants.MessageTypes.SessionCompleted, sessionId, _activeModuleId, null, null).Forget();
            _lastKnownSessionId = sessionId;
            _authorizationHandler?.ClearAuthorization();
            _activeSessionId = null;
            _activeModuleId = null;
            SessionCompleted?.Invoke(sessionId);
        }

        public void NotifySessionFailed(string sessionId, string failureCode, string detail)
        {
            SendLifecycleAsync(ProtocolConstants.MessageTypes.SessionFailed, sessionId, _activeModuleId, detail, failureCode).Forget();
            _authorizationHandler?.ClearAuthorization();
            _activeSessionId = null;
            _activeModuleId = null;
            SessionFailed?.Invoke(sessionId, failureCode);
        }

        /// <summary>
        /// The ONLY code path allowed to invoke the module launcher. Must be called exclusively when
        /// the player presses "Eğitimi Başlat" / "Start Training" inside VR — never from an
        /// authorize/start command handler.
        /// </summary>
        public async UniTask<bool> StartAuthorizedTrainingAsync(CancellationToken cancellationToken)
        {
            if (_authorizationHandler == null || !_authorizationHandler.TryBeginStart())
            {
                return false;
            }

            var context = _authorizationHandler.ActiveAuthorization;
            if (context == null)
            {
                return false;
            }

            await SendPlayerStartRequestedAsync(context, cancellationToken);

            try
            {
                await _moduleLauncher.LaunchSessionAsync(context, cancellationToken);
                _activeSessionId = context.SessionId;
                _activeModuleId = context.ModuleId;
                return true;
            }
            catch (Exception ex)
            {
                _authorizationHandler.ClearAuthorization();
                NotifySessionFailed(context.SessionId, ErrorCodes.SessionCommandRejected, $"Launch failed after player start: {ex.Message}");
                return false;
            }
        }

        public async UniTask QueueResultAsync(string resultId, string sessionId, string moduleId, ResultBodyPayload body, CancellationToken cancellationToken)
        {
            _resultOutbox.Enqueue(resultId, sessionId, moduleId, body);
            await FlushOutboxAsync(cancellationToken);
        }

        public UniTask RetryPendingResultsAsync(CancellationToken cancellationToken) => FlushOutboxAsync(cancellationToken);

        public BridgeDiagnosticsSnapshot CreateSnapshot()
        {
            return new BridgeDiagnosticsSnapshot
            {
                State = _state,
                DeviceId = _deviceId,
                DeviceDisplayName = _config?.DisplayName,
                ConnectionId = _connectionId,
                ServiceId = _serviceId,
                GatewayUrl = _gatewayUrl,
                AppVersion = _config?.AppVersion,
                ProtocolVersion = ProtocolConstants.ProtocolVersion,
                IsPaired = _credentialStore?.HasToken == true,
                LastErrorCode = _lastErrorCode,
                LastErrorMessage = _lastErrorMessage,
                SuggestedAction = BridgeDiagnosticCatalog.SuggestedAction(_lastErrorCode, _config?.DefaultLocale ?? "en"),
                HeartbeatSequence = HeartbeatSequence,
                LastHeartbeatSentUtc = _lastHeartbeatSentUtc,
                LastHeartbeatAckUtc = _lastHeartbeatAckUtc,
                ApproximateRttMilliseconds = _approximateRttMs,
                ReconnectAttempt = ReconnectAttempt,
                ActiveSessionId = _activeSessionId,
                PendingResultCount = PendingResultCount
            };
        }

        async UniTaskVoid RunLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _connectionInstanceId = Guid.NewGuid().ToString("N");
                    var offer = await ResolveGatewayAsync(cancellationToken);
                    _gatewayUrl = offer.DeviceGatewayUrl;
                    _serviceId = offer.ServiceId;
                    SetState(VrBridgeConnectionState.BridgeFound, "gateway-resolved");

                    SetState(VrBridgeConnectionState.Connecting, "websocket");
                    try
                    {
                        await _transport.ConnectAsync(new Uri(_gatewayUrl), cancellationToken);
                    }
                    catch (Exception)
                    {
                        throw new InvalidOperationException(ErrorCodes.WebSocketConnectFailed);
                    }

                    SetState(VrBridgeConnectionState.Authenticating, "device-hello");
                    await SendHelloAsync(offer.RequiresPairing || _repairRequested, cancellationToken);

                    var accepted = await WaitForHelloResponseAsync(cancellationToken);
                    if (accepted == null)
                    {
                        throw new InvalidOperationException(ErrorCodes.DeviceConnectionFailed);
                    }

                    await HandleAcceptedAsync(accepted, cancellationToken);
                    _repairRequested = false;
                    SetState(VrBridgeConnectionState.Connected, "authenticated");
                    _reconnectPolicy.Reset();
                    _missedHeartbeatAcks = 0;

                    _heartbeat.Configure(_heartbeatIntervalMs);
                    _heartbeat.Start(cancellationToken);
                    await FlushOutboxAsync(cancellationToken);

                    await UniTask.WaitUntil(() => !_transport.IsConnected, cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (DeviceIdentityCorruptionException ex)
                {
                    SetError(ex.ErrorCode, BridgeDiagnosticCatalog.Localize(ex.ErrorCode, _config.DefaultLocale));
                    SetState(VrBridgeConnectionState.Error, ex.ErrorCode);
                    break;
                }
                catch (Exception ex)
                {
                    var code = ExtractErrorCode(ex.Message);
                    SetError(code, BridgeDiagnosticCatalog.Localize(code, _config.DefaultLocale, ex.Message));

                    if (code == ErrorCodes.DeviceAuthenticationFailed || code == ErrorCodes.PairingCodeInvalid)
                    {
                        SetState(VrBridgeConnectionState.AuthenticationFailed, code);
                    }
                    else if (code == ErrorCodes.ProtocolInvalidMessage)
                    {
                        SetState(VrBridgeConnectionState.ProtocolRejected, code);
                    }
                    else if (code == ErrorCodes.NoActiveNetworkAdapter)
                    {
                        SetState(VrBridgeConnectionState.Offline, code);
                    }
                    else
                    {
                        SetState(VrBridgeConnectionState.Error, code);
                    }
                }

                _heartbeat.Stop();
                await _transport.DisconnectAsync(CancellationToken.None);

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (_state == VrBridgeConnectionState.AuthenticationFailed && !_repairRequested)
                {
                    SetState(VrBridgeConnectionState.PairingRequired, ErrorCodes.PairingRequired);
                    await UniTask.WaitUntil(
                        () => !string.IsNullOrWhiteSpace(_pendingPairingCode) || _repairRequested || cancellationToken.IsCancellationRequested,
                        cancellationToken: cancellationToken);
                    continue;
                }

                SetState(VrBridgeConnectionState.Reconnecting, "backoff");
                if (_reconnectPolicy.Attempt >= 4)
                {
                    SetState(VrBridgeConnectionState.Degraded, "rapid-reconnect");
                }

                await _reconnectPolicy.DelayBeforeNextAttemptAsync(cancellationToken);
            }
        }

        async UniTask<DiscoveryOfferPayload> ResolveGatewayAsync(CancellationToken cancellationToken)
        {
            var allowLoopback = Application.isEditor || _config.AllowLoopbackManualEndpoint;

            // 0) Explicit Editor/dev gateway override (never baked into Quest player builds).
            if (!string.IsNullOrWhiteSpace(_config.GatewayUrlOverride))
            {
                SetState(VrBridgeConnectionState.Connecting, "gateway-override");
                return BridgeDiscoveryClient.CreateManualOffer(_config.GatewayUrlOverride, allowLoopback);
            }

            // 1) Last successful endpoint
            var record = await _identityStore.GetRecordAsync();
            if (record != null && !string.IsNullOrWhiteSpace(record.LastGatewayUrl))
            {
                try
                {
                    SetState(VrBridgeConnectionState.Connecting, "last-endpoint");
                    var last = BridgeDiscoveryClient.CreateManualOffer(record.LastGatewayUrl, allowLoopback);
                    await ProbeConnectAsync(last.DeviceGatewayUrl, cancellationToken);
                    await _transport.DisconnectAsync(CancellationToken.None);
                    return last;
                }
                catch
                {
                    await _transport.DisconnectAsync(CancellationToken.None);
                }
            }

            // 2) UDP discovery
            try
            {
                if (_config.DiscoveryEnabled)
                {
                    SetState(VrBridgeConnectionState.Discovering, "udp");
                    return await _discovery.DiscoverUdpAsync(_deviceId, cancellationToken);
                }

                throw new InvalidOperationException(ErrorCodes.DiscoveryTimeout);
            }
            catch (Exception discoveryEx)
            {
                // 3) Manual host fallback
                var manual = _config.ManualBridgeHost;
                if (!string.IsNullOrWhiteSpace(manual) && _config.AllowManualHostFallback)
                {
                    SetState(VrBridgeConnectionState.Connecting, "manual-fallback");
                    if (!manual.Contains("://", StringComparison.Ordinal))
                    {
                        manual = $"ws://{manual}:{ProtocolConstants.GatewayPort}{ProtocolConstants.GatewayPath}";
                    }

                    return BridgeDiscoveryClient.CreateManualOffer(manual, allowLoopback);
                }

                throw discoveryEx;
            }
        }

        async UniTask ProbeConnectAsync(string gatewayUrl, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(500, _config.ConnectTimeoutMilliseconds)));
            await _transport.ConnectAsync(new Uri(gatewayUrl), cts.Token);
        }

        async UniTask SendHelloAsync(bool requiresPairing, CancellationToken cancellationToken)
        {
            var token = _credentialStore.HasToken ? await _credentialStore.ReadTokenAsync(cancellationToken) : null;
            var pairingCode = token == null ? _pendingPairingCode : null;

            if ((requiresPairing || token == null) && string.IsNullOrWhiteSpace(pairingCode) && string.IsNullOrWhiteSpace(token))
            {
                SetState(VrBridgeConnectionState.PairingRequired, ErrorCodes.PairingRequired);
                await UniTask.WaitUntil(
                    () => !string.IsNullOrWhiteSpace(_pendingPairingCode) || cancellationToken.IsCancellationRequested,
                    cancellationToken: cancellationToken);
                pairingCode = _pendingPairingCode;
                SetState(VrBridgeConnectionState.Authenticating, "pairing-code-submitted");
            }

            var hello = ProtocolJson.Create(
                ProtocolConstants.MessageTypes.DeviceHello,
                new DeviceHelloPayload
                {
                    DeviceId = _deviceId,
                    DeviceToken = token,
                    PairingCode = pairingCode,
                    DisplayName = _config.DisplayName,
                    AppVersion = _config.AppVersion,
                    ProtocolVersion = ProtocolConstants.ProtocolVersion,
                    DeviceModel = SystemInfo.deviceModel,
                    OperatingSystem = SystemInfo.operatingSystem,
                    Capabilities = _config.SupportedCapabilities,
                    LastKnownSessionId = _lastKnownSessionId,
                    ConnectionInstanceId = _connectionInstanceId
                },
                _deviceId);

            await SendEnvelopeAsync(hello, cancellationToken);
            _pendingPairingCode = null;
        }

        async UniTask<DeviceAcceptedPayload> WaitForHelloResponseAsync(CancellationToken cancellationToken)
        {
            _helloAcceptedTcs = new UniTaskCompletionSource<DeviceAcceptedPayload>();
            _helloRejectedTcs = new UniTaskCompletionSource<bool>();

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(_config.HandshakeTimeoutSeconds));

                var acceptedTask = _helloAcceptedTcs.Task.AttachExternalCancellation(timeout.Token);
                var rejectedTask = _helloRejectedTcs.Task.AttachExternalCancellation(timeout.Token);

                var (winIndex, accepted, _) = await UniTask.WhenAny(acceptedTask, rejectedTask);
                if (winIndex == 1)
                {
                    throw new InvalidOperationException(ErrorCodes.DeviceAuthenticationFailed);
                }

                return accepted;
            }
            finally
            {
                _helloAcceptedTcs = null;
                _helloRejectedTcs = null;
            }
        }

        async UniTask HandleAcceptedAsync(DeviceAcceptedPayload accepted, CancellationToken cancellationToken)
        {
            _connectionId = accepted.ConnectionId;
            _serviceId = accepted.ServiceId;
            _heartbeatIntervalMs = accepted.HeartbeatIntervalMilliseconds > 0
                ? accepted.HeartbeatIntervalMilliseconds
                : _config.DefaultHeartbeatIntervalMilliseconds;

            if (!string.IsNullOrWhiteSpace(accepted.DeviceToken))
            {
                await _credentialStore.WriteTokenAsync(accepted.DeviceToken, cancellationToken);
            }

            await _identityStore.UpdateEndpointMetadataAsync(_config.DisplayName, accepted.ServiceId, _gatewayUrl);

            if (accepted.ActiveSession != null)
            {
                _activeSessionId = accepted.ActiveSession.SessionId;
                _lastKnownSessionId = accepted.ActiveSession.SessionId;
            }
        }

        void OnTransportMessage(string json)
        {
            HandleMessageAsync(json, _runCts?.Token ?? CancellationToken.None).Forget();
        }

        async UniTask HandleMessageAsync(string json, CancellationToken cancellationToken)
        {
            var bytes = System.Text.Encoding.UTF8.GetByteCount(json);
            if (!ProtocolJson.TryDeserializeEnvelope(json, out var envelope, out _))
            {
                _malformedCount++;
                if (_malformedCount >= _config.MaxMalformedMessagesBeforeClose)
                {
                    await _transport.DisconnectAsync(cancellationToken);
                }

                return;
            }

            var validation = ProtocolEnvelopeValidator.Validate(envelope, bytes, requireDeviceId: false, DateTimeOffset.UtcNow);
            if (!validation.IsValid)
            {
                _malformedCount++;
                return;
            }

            switch (envelope.Type)
            {
                case ProtocolConstants.MessageTypes.DeviceAccepted:
                    var accepted = ProtocolJson.DeserializePayload<DeviceAcceptedPayload>(envelope.Payload);
                    if (accepted != null)
                    {
                        _helloAcceptedTcs?.TrySetResult(accepted);
                    }
                    break;
                case ProtocolConstants.MessageTypes.DeviceRejected:
                    await HandleRejectedMessageAsync(envelope, cancellationToken);
                    break;
                case ProtocolConstants.MessageTypes.DeviceHeartbeatAck:
                    HandleHeartbeatAck(envelope);
                    break;
                case ProtocolConstants.MessageTypes.SessionStartCommand:
                    await _sessionHandler.HandleSessionStartAsync(envelope, cancellationToken);
                    break;
                case ProtocolConstants.MessageTypes.SessionAuthorizeCommand:
                    await _authorizationHandler.HandleAuthorizeAsync(envelope, cancellationToken);
                    break;
                case ProtocolConstants.MessageTypes.SessionAuthorizationRevoked:
                    var revoked = ProtocolJson.DeserializePayload<SessionAuthorizationLifecyclePayload>(envelope.Payload);
                    await _authorizationHandler.HandleRevokedAsync(revoked, cancellationToken);
                    break;
                case ProtocolConstants.MessageTypes.SessionAuthorizationExpired:
                    var expired = ProtocolJson.DeserializePayload<SessionAuthorizationLifecyclePayload>(envelope.Payload);
                    await _authorizationHandler.HandleExpiredAsync(expired, cancellationToken);
                    break;
                case ProtocolConstants.MessageTypes.SessionCancelCommand:
                    var cancel = ProtocolJson.DeserializePayload<SessionLifecyclePayload>(envelope.Payload);
                    await _authorizationHandler.HandleCancelAsync(cancel, cancellationToken);
                    SessionCancelled?.Invoke(cancel?.SessionId);
                    break;
                case ProtocolConstants.MessageTypes.ResultAck:
                    HandleResultAck(envelope);
                    break;
                case ProtocolConstants.MessageTypes.ProtocolError:
                    var error = ProtocolJson.DeserializePayload<ProtocolErrorPayload>(envelope.Payload);
                    SetError(error?.ErrorCode ?? ErrorCodes.ProtocolInvalidMessage, error?.Detail);
                    break;
            }
        }

        async UniTask HandleRejectedMessageAsync(ProtocolEnvelope envelope, CancellationToken cancellationToken)
        {
            var rejected = ProtocolJson.DeserializePayload<DeviceRejectedPayload>(envelope.Payload);
            var code = rejected?.ErrorCode ?? ErrorCodes.DeviceAuthenticationFailed;
            SetError(code, BridgeDiagnosticCatalog.Localize(code, _config.DefaultLocale, rejected?.MessageKey));

            // Handshake-only handling. RunLoop owns transport disconnect to avoid Dispose races.
            var handshakeInFlight = _state == VrBridgeConnectionState.Authenticating
                                    || _helloAcceptedTcs != null
                                    || _helloRejectedTcs != null;
            if (!handshakeInFlight || _state == VrBridgeConnectionState.Connected)
            {
                return;
            }

            _helloRejectedTcs?.TrySetResult(true);

            var authFailure = code.IndexOf("AUTH", StringComparison.OrdinalIgnoreCase) >= 0
                              || code.IndexOf("PAIR", StringComparison.OrdinalIgnoreCase) >= 0
                              || code == ErrorCodes.DeviceAuthenticationFailed
                              || code == ErrorCodes.PairingCodeInvalid;
            if (authFailure)
            {
                await _credentialStore.DeleteTokenAsync(cancellationToken);
            }
        }

        void HandleHeartbeatAck(ProtocolEnvelope envelope)
        {
            var ack = ProtocolJson.DeserializePayload<DeviceHeartbeatAckPayload>(envelope.Payload);
            if (ack == null)
            {
                return;
            }

            _lastHeartbeatAckUtc = DateTimeOffset.UtcNow;
            _missedHeartbeatAcks = 0;
            if (_lastHeartbeatSentUtc.HasValue && ack.Sequence == _lastHeartbeatSequence)
            {
                _approximateRttMs = (_lastHeartbeatAckUtc.Value - _lastHeartbeatSentUtc.Value).TotalMilliseconds;
            }
        }

        void HandleResultAck(ProtocolEnvelope envelope)
        {
            var ack = ProtocolJson.DeserializePayload<ResultAckPayload>(envelope.Payload);
            if (ack == null)
            {
                return;
            }

            if (ack.Accepted || ack.Duplicate)
            {
                _resultOutbox.Remove(ack.ResultId);
            }
            else
            {
                _resultOutbox.MarkAttempt(ack.ResultId);
            }
        }

        async UniTask FlushOutboxAsync(CancellationToken cancellationToken)
        {
            if (!_transport.IsConnected)
            {
                return;
            }

            foreach (var entry in _resultOutbox.Snapshot())
            {
                var envelope = ProtocolJson.Create(
                    ProtocolConstants.MessageTypes.ResultSubmit,
                    new ResultSubmitPayload
                    {
                        ResultId = entry.ResultId,
                        SessionId = entry.SessionId,
                        ModuleId = entry.ModuleId,
                        CompletedAtUtc = entry.QueuedAtUtc,
                        SchemaVersion = 1,
                        Result = entry.Body,
                        PayloadSha256 = entry.PayloadSha256
                    },
                    _deviceId);

                await SendEnvelopeAsync(envelope, cancellationToken);
                _resultOutbox.MarkAttempt(entry.ResultId);
            }
        }

        async UniTask SendHeartbeatAsync(CancellationToken cancellationToken)
        {
            if (!_transport.IsConnected)
            {
                return;
            }

            if (_lastHeartbeatSentUtc.HasValue && !_lastHeartbeatAckUtc.HasValue
                || (_lastHeartbeatSentUtc.HasValue && _lastHeartbeatAckUtc.HasValue && _lastHeartbeatAckUtc < _lastHeartbeatSentUtc))
            {
                _missedHeartbeatAcks++;
                if (_missedHeartbeatAcks >= 3)
                {
                    SetState(VrBridgeConnectionState.Degraded, ErrorCodes.HeartbeatTimedOut);
                }
            }

            var sequence = _heartbeat.NextSequence();
            _lastHeartbeatSequence = sequence;
            _lastHeartbeatSentUtc = DateTimeOffset.UtcNow;

            var payload = new DeviceHeartbeatPayload
            {
                Sequence = sequence,
                ClientTimeUtc = DateTimeOffset.UtcNow,
                DeviceState = string.IsNullOrWhiteSpace(_activeSessionId) ? "Online" : "Busy",
                ActiveSessionId = _activeSessionId,
                BatteryLevelPercent = SystemInfo.batteryLevel >= 0 ? Mathf.RoundToInt(SystemInfo.batteryLevel * 100f) : (int?)null,
                NetworkType = Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork ? "WiFi" : "Unknown"
            };

            var envelope = ProtocolJson.Create(ProtocolConstants.MessageTypes.DeviceHeartbeat, payload, _deviceId);
            await SendEnvelopeAsync(envelope, cancellationToken);
        }

        async UniTask SendCommandAckAsync(SessionCommandAckPayload ack)
        {
            var envelope = ProtocolJson.Create(
                ProtocolConstants.MessageTypes.SessionCommandAck,
                ack,
                _deviceId,
                ack.CommandMessageId);
            await SendEnvelopeAsync(envelope, _runCts?.Token ?? CancellationToken.None);
        }

        async UniTask SendAuthorizationAckAsync(SessionAuthorizationAckPayload ack)
        {
            var envelope = ProtocolJson.Create(
                ProtocolConstants.MessageTypes.SessionAuthorizationAck,
                ack,
                _deviceId,
                ack.CommandMessageId);
            await SendEnvelopeAsync(envelope, _runCts?.Token ?? CancellationToken.None);
        }

        async UniTask SendPlayerStartRequestedAsync(BridgeSessionContext context, CancellationToken cancellationToken)
        {
            var envelope = ProtocolJson.Create(
                ProtocolConstants.MessageTypes.SessionPlayerStartRequested,
                new SessionPlayerStartRequestedPayload
                {
                    AuthorizationId = context.AuthorizationId,
                    SessionId = context.SessionId,
                    ModuleId = context.ModuleId,
                    RequestedAtUtc = DateTimeOffset.UtcNow
                },
                _deviceId);
            await SendEnvelopeAsync(envelope, cancellationToken);
        }

        async UniTask SendLifecycleAsync(string type, string sessionId, string moduleId, string detail, string failureCode)
        {
            var envelope = ProtocolJson.Create(
                type,
                new SessionLifecyclePayload
                {
                    SessionId = sessionId,
                    ModuleId = moduleId,
                    Detail = detail,
                    FailureCode = failureCode
                },
                _deviceId);
            await SendEnvelopeAsync(envelope, _runCts?.Token ?? CancellationToken.None);
        }

        async UniTask SendEnvelopeAsync<T>(ProtocolEnvelope<T> envelope, CancellationToken cancellationToken) where T : class
        {
            var bytes = ProtocolJson.Serialize(envelope);
            await _transport.SendAsync(bytes, cancellationToken);
        }

        UniTask SetActiveSessionAsync(string sessionId, string moduleId)
        {
            _activeSessionId = sessionId;
            _activeModuleId = moduleId;
            return UniTask.CompletedTask;
        }

        void OnTransportClosed()
        {
            _heartbeat.Stop();
            if (_state == VrBridgeConnectionState.Connected || _state == VrBridgeConnectionState.Degraded)
            {
                SetState(VrBridgeConnectionState.Reconnecting, "socket-closed");
            }
        }

        void SetState(VrBridgeConnectionState state, string reason)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
            StateChanged?.Invoke(state, reason);
        }

        void SetError(string code, string message)
        {
            _lastErrorCode = code;
            _lastErrorMessage = message;
            Debug.LogWarning($"[VRBridge] {code}");
        }

        static string ExtractErrorCode(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return ErrorCodes.DeviceConnectionFailed;
            }

            var colon = message.IndexOf(':');
            var candidate = colon > 0 ? message.Substring(0, colon) : message;
            return candidate.StartsWith("VRB-", StringComparison.Ordinal) ? candidate.Trim() : ErrorCodes.DeviceConnectionFailed;
        }
    }
}
