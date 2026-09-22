using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Connection;

namespace Woi.VrBridge.Client.UI
{
    [RequireComponent(typeof(Canvas))]
    public sealed class VrBridgePairingPanel : MonoBehaviour
    {
        [SerializeField] VrBridgeClient _client;
        [SerializeField] BridgeClientConfig _config;
        [SerializeField] InputField _codeInput;
        [SerializeField] Text _brandText;
        [SerializeField] Text _titleText;
        [SerializeField] Text _instructionsText;
        [SerializeField] Text _statusText;
        [SerializeField] Button _submitButton;
        [SerializeField] Button _retryButton;
        [SerializeField] Button _repairButton;
        [SerializeField] MonoBehaviour _localizerBehaviour;

        IBridgeStringLocalizer _localizer;

        void Awake()
        {
            _localizer = _localizerBehaviour as IBridgeStringLocalizer;
            ApplyStrings();
            gameObject.SetActive(false);

            if (_submitButton != null)
            {
                _submitButton.onClick.AddListener(OnSubmitClicked);
            }

            if (_retryButton != null)
            {
                _retryButton.onClick.AddListener(() => _client?.ReconnectAsync(this.GetCancellationTokenOnDestroy()).Forget());
            }

            if (_repairButton != null)
            {
                _repairButton.onClick.AddListener(() => _client?.RequestRepairAsync(this.GetCancellationTokenOnDestroy()).Forget());
            }

            if (_client != null)
            {
                _client.StateChanged += OnStateChanged;
            }
        }

        void OnDestroy()
        {
            if (_client != null)
            {
                _client.StateChanged -= OnStateChanged;
            }
        }

        void OnStateChanged(VrBridgeConnectionState state, string reason)
        {
            var visible = state == VrBridgeConnectionState.PairingRequired
                          || state == VrBridgeConnectionState.AuthenticationFailed;
            gameObject.SetActive(visible);
            if (_statusText != null)
            {
                _statusText.text = BridgeUiStrings.Get("pairing.waiting", Locale(), _localizer);
            }
        }

        void OnSubmitClicked()
        {
            if (_client == null || _codeInput == null)
            {
                return;
            }

            _client.SubmitPairingCodeAsync(_codeInput.text, this.GetCancellationTokenOnDestroy()).Forget();
            _codeInput.text = string.Empty;
        }

        void ApplyStrings()
        {
            var locale = Locale();
            if (_brandText != null)
            {
                _brandText.text = BridgeUiStrings.Get("pairing.brand", locale, _localizer);
            }

            if (_titleText != null)
            {
                _titleText.text = BridgeUiStrings.Get("pairing.title", locale, _localizer);
            }

            if (_instructionsText != null)
            {
                _instructionsText.text = BridgeUiStrings.Get("pairing.instructions", locale, _localizer);
            }

            SetButtonLabel(_submitButton, "pairing.submit", locale);
            SetButtonLabel(_retryButton, "pairing.retry", locale);
            SetButtonLabel(_repairButton, "pairing.repair", locale);
        }

        void SetButtonLabel(Button button, string key, string locale)
        {
            if (button == null)
            {
                return;
            }

            var label = button.GetComponentInChildren<Text>();
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
