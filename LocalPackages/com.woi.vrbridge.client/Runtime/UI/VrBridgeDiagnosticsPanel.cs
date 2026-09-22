using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Connection;
using Woi.VrBridge.Client.Diagnostics;
using Woi.VrBridge.Client.Protocol;

namespace Woi.VrBridge.Client.UI
{
    public sealed class VrBridgeDiagnosticsPanel : MonoBehaviour
    {
        [SerializeField] VrBridgeClient _client;
        [SerializeField] BridgeClientConfig _config;
        [SerializeField] Text _summaryText;
        [SerializeField] Button _retryButton;
        [SerializeField] Button _repairButton;
        [SerializeField] Button _copyErrorButton;
        [SerializeField] Button _resetIdentityButton;
        [SerializeField] Button _exportButton;
        [SerializeField] MonoBehaviour _localizerBehaviour;

        IBridgeStringLocalizer _localizer;
        bool _resetConfirmPending;
        float _nextRefresh;

        void Awake()
        {
            _localizer = _localizerBehaviour as IBridgeStringLocalizer;

            if (_retryButton != null)
            {
                _retryButton.onClick.AddListener(() => _client?.ReconnectAsync(this.GetCancellationTokenOnDestroy()).Forget());
            }

            if (_repairButton != null)
            {
                _repairButton.onClick.AddListener(() => _client?.RequestRepairAsync(this.GetCancellationTokenOnDestroy()).Forget());
            }

            if (_copyErrorButton != null)
            {
                _copyErrorButton.onClick.AddListener(CopyError);
            }

            if (_resetIdentityButton != null)
            {
                _resetIdentityButton.onClick.AddListener(OnResetClicked);
            }

            if (_exportButton != null)
            {
                _exportButton.onClick.AddListener(ExportSanitized);
            }

            if (_client != null)
            {
                _client.StateChanged += (_, __) => Refresh();
            }
        }

        void Update()
        {
            if (Time.unscaledTime < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.unscaledTime + 0.5f;
            Refresh();
        }

        void Refresh()
        {
            if (_client == null || _summaryText == null)
            {
                return;
            }

            var snapshot = _client.CreateSnapshot();
            var locale = _config != null ? _config.DefaultLocale : "tr";
            var shortId = string.IsNullOrEmpty(snapshot.DeviceId) || snapshot.DeviceId.Length < 8
                ? snapshot.DeviceId
                : snapshot.DeviceId.Substring(0, 8);

            var reconnectHint = snapshot.State == VrBridgeConnectionState.Reconnecting
                ? (locale == "tr"
                    ? $"Quest bağlantısı kesildi.\nYeniden bağlanılıyor: deneme {snapshot.ReconnectAttempt}"
                    : $"Quest connection interrupted.\nReconnecting: attempt {snapshot.ReconnectAttempt}")
                : string.Empty;

            _summaryText.text =
                $"{BridgeUiStrings.Get("diagnostics.deviceName", locale, _localizer)}: {snapshot.DeviceDisplayName}\n" +
                $"{BridgeUiStrings.Get("diagnostics.deviceId", locale, _localizer)}: {shortId}\n" +
                $"{BridgeUiStrings.Get("diagnostics.state", locale, _localizer)}: {snapshot.State} ({(snapshot.IsPaired ? "paired" : "unpaired")})\n" +
                $"{BridgeUiStrings.Get("diagnostics.gateway", locale, _localizer)}: {snapshot.GatewayUrl}\n" +
                $"{BridgeUiStrings.Get("diagnostics.serviceId", locale, _localizer)}: {snapshot.ServiceId}\n" +
                $"App: {snapshot.AppVersion} | Protocol: {snapshot.ProtocolVersion}\n" +
                $"{BridgeUiStrings.Get("diagnostics.heartbeat", locale, _localizer)}: {snapshot.LastHeartbeatAckUtc}\n" +
                $"{BridgeUiStrings.Get("diagnostics.rtt", locale, _localizer)}: {snapshot.ApproximateRttMilliseconds:0} ms\n" +
                $"{BridgeUiStrings.Get("diagnostics.reconnect", locale, _localizer)}: {snapshot.ReconnectAttempt}\n" +
                $"{BridgeUiStrings.Get("diagnostics.session", locale, _localizer)}: {snapshot.ActiveSessionId}\n" +
                $"{BridgeUiStrings.Get("diagnostics.pendingResults", locale, _localizer)}: {snapshot.PendingResultCount}\n" +
                $"{BridgeUiStrings.Get("diagnostics.error", locale, _localizer)}: {snapshot.LastErrorCode}\n" +
                $"{BridgeDiagnosticCatalog.Localize(snapshot.LastErrorCode, locale, snapshot.LastErrorMessage)}\n" +
                $"{BridgeUiStrings.Get("diagnostics.action", locale, _localizer)}: {snapshot.SuggestedAction}\n" +
                reconnectHint;
        }

        void CopyError()
        {
            if (_client == null)
            {
                return;
            }

            var snapshot = _client.CreateSnapshot();
            GUIUtility.systemCopyBuffer = snapshot.LastErrorCode ?? string.Empty;
        }

        void ExportSanitized()
        {
            if (_client == null)
            {
                return;
            }

            var snapshot = _client.CreateSnapshot();
            var sb = new StringBuilder();
            sb.AppendLine("WOI VR Bridge Diagnostics (sanitized)");
            sb.AppendLine($"protocolVersion={ProtocolConstants.ProtocolVersion}");
            sb.AppendLine($"state={snapshot.State}");
            sb.AppendLine($"deviceIdShort={(snapshot.DeviceId?.Length >= 8 ? snapshot.DeviceId.Substring(0, 8) : snapshot.DeviceId)}");
            sb.AppendLine($"serviceId={snapshot.ServiceId}");
            sb.AppendLine($"gateway={snapshot.GatewayUrl}");
            sb.AppendLine($"errorCode={snapshot.LastErrorCode}");
            sb.AppendLine($"pendingResults={snapshot.PendingResultCount}");
            sb.AppendLine($"reconnectAttempt={snapshot.ReconnectAttempt}");
            sb.AppendLine($"activeSessionId={snapshot.ActiveSessionId}");
            // Never include tokens, pairing codes, participant identity, or raw payloads.

            var path = Path.Combine(Application.persistentDataPath, "WOI", "VRBridge", "diagnostics-export.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[VRBridge] Sanitized diagnostics exported to {path}");
        }

        void OnResetClicked()
        {
            var locale = _config != null ? _config.DefaultLocale : "tr";
            if (!_resetConfirmPending)
            {
                _resetConfirmPending = true;
                if (_summaryText != null)
                {
                    _summaryText.text = BridgeUiStrings.Get("diagnostics.resetConfirm", locale, _localizer);
                }

                return;
            }

            _resetConfirmPending = false;
            _client?.ResetIdentityAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
    }
}
