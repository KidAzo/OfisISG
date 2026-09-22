using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Connection;

namespace Woi.VrBridge.Client.UI
{
    /// <summary>
    /// Phase 2D overlay shown after session.authorize.command succeeds. It only ever calls
    /// <see cref="VrBridgeClient.StartAuthorizedTrainingAsync"/> in direct response to the player
    /// pressing "Eğitimi Başlat" / "Start Training" — authorization alone never opens the game.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class AuthorizedTrainingPanel : MonoBehaviour
    {
        [SerializeField] VrBridgeClient _client;
        [SerializeField] BridgeClientConfig _config;
        [SerializeField] Text _brandText;
        [SerializeField] Text _moduleText;
        [SerializeField] Text _participantText;
        [SerializeField] Text _personnelText;
        [SerializeField] Text _statusText;
        [SerializeField] Button _startButton;
        [SerializeField] MonoBehaviour _localizerBehaviour;

        IBridgeStringLocalizer _localizer;
        BridgeSessionContext _context;

        /// <summary>Local re-entrancy guard on top of the client's own atomic start guard.</summary>
        bool _startInFlight;

        void Awake()
        {
            _localizer = _localizerBehaviour as IBridgeStringLocalizer;
            gameObject.SetActive(false);

            if (_startButton != null)
            {
                _startButton.onClick.AddListener(OnStartClicked);
            }

            if (_client != null)
            {
                _client.AuthorizationReady += OnAuthorizationReady;
                _client.AuthorizationRevoked += OnAuthorizationRevoked;
                _client.AuthorizationExpired += OnAuthorizationExpired;
                _client.SessionStarted += OnSessionStarted;
                _client.SessionFailed += OnSessionFailed;
                _client.SessionCancelled += OnSessionCancelled;
            }
        }

        void OnDestroy()
        {
            if (_client != null)
            {
                _client.AuthorizationReady -= OnAuthorizationReady;
                _client.AuthorizationRevoked -= OnAuthorizationRevoked;
                _client.AuthorizationExpired -= OnAuthorizationExpired;
                _client.SessionStarted -= OnSessionStarted;
                _client.SessionFailed -= OnSessionFailed;
                _client.SessionCancelled -= OnSessionCancelled;
            }
        }

        void Update()
        {
            if (!gameObject.activeSelf || _context == null || _startButton == null)
            {
                return;
            }

            if (_context.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                SetStatus("authorized.status.expired");
                _startButton.interactable = false;
                return;
            }

            _startButton.interactable = !_startInFlight && (_client?.CanStartAuthorizedTraining ?? false);
        }

        void OnAuthorizationReady(BridgeSessionContext context)
        {
            _context = context;
            _startInFlight = false;
            ApplyContext(context);
            SetStatus("authorized.status.ready");
            if (_startButton != null)
            {
                _startButton.interactable = _client?.CanStartAuthorizedTraining ?? false;
            }

            gameObject.SetActive(true);
        }

        void OnAuthorizationRevoked(string sessionId, string reason)
        {
            if (_context == null || !string.Equals(_context.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SetStatus("authorized.status.revoked");
            if (_startButton != null)
            {
                _startButton.interactable = false;
            }
        }

        void OnAuthorizationExpired(string sessionId, string reason)
        {
            if (_context == null || !string.Equals(_context.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SetStatus("authorized.status.expired");
            if (_startButton != null)
            {
                _startButton.interactable = false;
            }
        }

        void OnSessionStarted(BridgeSessionContext context)
        {
            // The gameplay adapter confirmed module readiness — waiting card is no longer needed.
            HidePanel();
        }

        void OnSessionFailed(string sessionId, string failureCode)
        {
            if (_context != null && !string.Equals(_context.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _startInFlight = false;
            SetStatus("authorized.status.failed");
            if (_startButton != null)
            {
                _startButton.interactable = false;
            }
        }

        void OnSessionCancelled(string sessionId)
        {
            if (_context != null && !string.Equals(_context.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            HidePanel();
        }

        void OnStartClicked()
        {
            if (_startInFlight || _client == null || _context == null)
            {
                return;
            }

            // Disable immediately so a second click before the awaited call resumes can't fire again.
            _startInFlight = true;
            if (_startButton != null)
            {
                _startButton.interactable = false;
            }

            SetStatus("authorized.status.starting");
            StartTrainingAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid StartTrainingAsync(CancellationToken cancellationToken)
        {
            var started = await _client.StartAuthorizedTrainingAsync(cancellationToken);
            if (!started)
            {
                _startInFlight = false;
                SetStatus("authorized.status.failed");
                return;
            }

            SetStatus("authorized.status.started");
        }

        void HidePanel()
        {
            _context = null;
            _startInFlight = false;
            gameObject.SetActive(false);
        }

        void ApplyContext(BridgeSessionContext context)
        {
            var locale = Locale();
            if (_brandText != null)
            {
                _brandText.text = BridgeUiStrings.Get("authorized.brand", locale, _localizer);
            }

            if (_moduleText != null)
            {
                _moduleText.text = BridgeUiStrings.GetModuleName(context.ModuleId, locale, _localizer);
            }

            if (_participantText != null)
            {
                _participantText.text = $"{BridgeUiStrings.Get("authorized.participantLabel", locale, _localizer)}: {context.ParticipantFullName}";
            }

            if (_personnelText != null)
            {
                _personnelText.text = $"{BridgeUiStrings.Get("authorized.personnelLabel", locale, _localizer)}: {context.PersonnelId}";
            }

            SetButtonLabel("authorized.start", locale);
        }

        void SetStatus(string key)
        {
            if (_statusText != null)
            {
                _statusText.text = BridgeUiStrings.Get(key, Locale(), _localizer);
            }
        }

        void SetButtonLabel(string key, string locale)
        {
            if (_startButton == null)
            {
                return;
            }

            var label = _startButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = BridgeUiStrings.Get(key, locale, _localizer);
            }
        }

        string Locale()
        {
            return _config != null ? _config.DefaultLocale : "tr";
        }
    }
}
