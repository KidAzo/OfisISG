using System;
using System.Collections.Generic;
using UnityEngine;

namespace Woi.VrBridge.Client.Configuration
{
    [CreateAssetMenu(fileName = "BridgeClientConfig", menuName = "WOI/VR Bridge/Client Config")]
    public sealed class BridgeClientConfig : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "Quest Device";
        public string AppVersion = "1.0.0";

        [Header("Discovery")]
        public bool DiscoveryEnabled = true;
        public int DiscoveryPort = Protocol.ProtocolConstants.DiscoveryPort;
        public float DiscoveryTimeoutSeconds = 5f;
        public int BroadcastIntervalMilliseconds = 1000;
        public string DiscoveryBroadcastAddress = "255.255.255.255";
        public bool UseBroadcastDiscovery = true;

        [Header("Gateway")]
        [Tooltip("LAN development only. Prefer wss:// for production.")]
        public string GatewayUrlOverride;
        public string ManualBridgeHost;
        public bool AllowManualHostFallback = true;
        [Tooltip("Editor / controlled LAN only. Discovery offers must never be loopback.")]
        public bool AllowLoopbackManualEndpoint = true;
        public int ConnectTimeoutMilliseconds = 5000;
        public int MaximumMessageBytes = Protocol.ProtocolConstants.MaxMessageBytes;

        [Header("Capabilities")]
        public string[] SupportedCapabilities = { "session", "result", "session-resume", "cancellation" };
        public string[] SupportedModuleIds = { "fire-training", "default" };

        [Header("Connection")]
        public float HandshakeTimeoutSeconds = Protocol.ProtocolConstants.HandshakeTimeoutSeconds;
        public int MaxSendQueueSize = 32;
        public int MaxMalformedMessagesBeforeClose = 8;
        public bool AllowCleartextWs = true;
        public bool VerboseProtocolDiagnostics = false;

        [Header("Reconnect Backoff (seconds)")]
        public float[] ReconnectDelaysSeconds = { 1f, 2f, 4f, 8f, 15f };
        public float ReconnectJitterMaxSeconds = 0.5f;

        [Header("Heartbeat Defaults (overridden by device.accepted)")]
        public int DefaultHeartbeatIntervalMilliseconds = 2000;
        public int DefaultStaleThresholdMilliseconds = 7000;
        public int DefaultOfflineThresholdMilliseconds = 15000;

        [Header("Storage")]
        public int IdempotencyStoreMaxEntries = 256;
        public int ResultOutboxMaxRetries = 5;

        [Header("UI")]
        public bool EnablePairingPanel = true;
        public bool EnableDiagnosticsPanel = true;
        public bool EnableAuthorizedTrainingPanel = true;
        public string DefaultLocale = "en";

        [Header("Phase 2D — Customer Authorization")]
        [Tooltip("Digitech Hub Firebase project used to redeem session.authorize.command tickets. May be null in Editor/dev builds.")]
        public FirebaseAuthConfig FirebaseAuth;

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                errors.Add("DisplayName is required.");
            }

            if (string.IsNullOrWhiteSpace(AppVersion))
            {
                errors.Add("AppVersion is required.");
            }

            if (DiscoveryPort <= 0 || DiscoveryPort > 65535)
            {
                errors.Add("DiscoveryPort must be between 1 and 65535.");
            }

            if (DiscoveryTimeoutSeconds <= 0f)
            {
                errors.Add("DiscoveryTimeoutSeconds must be positive.");
            }

            if (SupportedCapabilities == null || SupportedCapabilities.Length == 0)
            {
                errors.Add("At least one capability is required.");
            }

            if (SupportedModuleIds == null || SupportedModuleIds.Length == 0)
            {
                errors.Add("At least one supported module id is required.");
            }

            if (ReconnectDelaysSeconds == null || ReconnectDelaysSeconds.Length == 0)
            {
                errors.Add("ReconnectDelaysSeconds must contain at least one delay.");
            }
            else
            {
                foreach (var delay in ReconnectDelaysSeconds)
                {
                    if (delay < 0f)
                    {
                        errors.Add("Reconnect delays cannot be negative.");
                        break;
                    }
                }
            }

            if (MaxSendQueueSize <= 0)
            {
                errors.Add("MaxSendQueueSize must be positive.");
            }

            if (IdempotencyStoreMaxEntries <= 0)
            {
                errors.Add("IdempotencyStoreMaxEntries must be positive.");
            }

            if (ResultOutboxMaxRetries <= 0)
            {
                errors.Add("ResultOutboxMaxRetries must be positive.");
            }

            if (!string.IsNullOrWhiteSpace(GatewayUrlOverride))
            {
                if (!Uri.TryCreate(GatewayUrlOverride, UriKind.Absolute, out var uri)
                    || (uri.Scheme != "ws" && uri.Scheme != "wss"))
                {
                    errors.Add("GatewayUrlOverride must be a valid ws:// or wss:// URI.");
                }
            }

            return errors;
        }
    }
}
