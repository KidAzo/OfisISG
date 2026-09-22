#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Woi.OfficeFire.VrBridge;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Protocol;

namespace Woi.OfficeFire.Editor
{
    /// <summary>
    /// Creates BridgeClientConfig and injects VrBridgeShellInstaller into FireModule_Bootstrapper.
    /// Menu: WOI → VR Bridge → Wire Phase 2C Shell
    /// Batchmode: -executeMethod Woi.OfficeFire.Editor.VrBridgePhase2CWireUp.Execute
    /// </summary>
    public static class VrBridgePhase2CWireUp
    {
        const string BootstrapScenePath = "Assets/Project/Scenes/FireModule/FireModule_Bootstrapper.unity";
        const string ConfigDir = "Assets/Project/Data/VRBridge";
        const string ConfigPath = ConfigDir + "/BridgeClientConfig.asset";

        [MenuItem("WOI/VR Bridge/Wire Phase 2C Shell")]
        public static void ExecuteFromMenu()
        {
            Execute();
        }

        public static void Execute()
        {
            var config = EnsureConfigAsset();
            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

            var existing = UnityEngine.Object.FindObjectsByType<VrBridgeShellInstaller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            VrBridgeShellInstaller installer;
            if (existing != null && existing.Length > 0)
            {
                installer = existing[0];
                for (var i = 1; i < existing.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(existing[i].gameObject);
                }
            }
            else
            {
                var go = new GameObject(VrBridgeShellInstaller.RootName);
                installer = go.AddComponent<VrBridgeShellInstaller>();
            }

            var so = new SerializedObject(installer);
            Assign(so, "_config", config);
            Assign(so, "_connectOnStart", true);
            Assign(so, "_buildUiIfMissing", true);
            Assign(so, "_gameplaySceneGroup", "FireModule_Office");
            Assign(so, "_moduleId", "fire-training");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(installer);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new IOException("Failed to save bootstrap scene: " + BootstrapScenePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Verify serialized reference survived save.
            var reloaded = AssetDatabase.LoadAssetAtPath<BridgeClientConfig>(ConfigPath);
            var verify = new SerializedObject(installer);
            var configProp = verify.FindProperty("_config");
            if (reloaded == null || configProp == null || configProp.objectReferenceValue == null)
            {
                Debug.LogError("[VRBridge] Config reference missing after wire-up — check BridgeClientConfig.asset");
            }

            Debug.Log($"[VRBridge] Phase 2C shell wired. config={ConfigPath} scene={BootstrapScenePath}");
#if UNITY_EDITOR
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
#endif
        }

        static BridgeClientConfig EnsureConfigAsset()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Project/Data/VRBridge"));
            var existing = AssetDatabase.LoadAssetAtPath<BridgeClientConfig>(ConfigPath);
            if (existing != null)
            {
                ApplyDefaults(existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var config = ScriptableObject.CreateInstance<BridgeClientConfig>();
            ApplyDefaults(config);
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static void Assign(SerializedObject so, string property, UnityEngine.Object value) // UnityEngine.Object
        {
            var prop = so.FindProperty(property);
            if (prop == null)
            {
                throw new InvalidOperationException("Missing serialized property: " + property);
            }

            prop.objectReferenceValue = value;
        }

        static void Assign(SerializedObject so, string property, bool value)
        {
            var prop = so.FindProperty(property);
            if (prop == null)
            {
                throw new InvalidOperationException("Missing serialized property: " + property);
            }

            prop.boolValue = value;
        }

        static void Assign(SerializedObject so, string property, string value)
        {
            var prop = so.FindProperty(property);
            if (prop == null)
            {
                throw new InvalidOperationException("Missing serialized property: " + property);
            }

            prop.stringValue = value;
        }

        static void ApplyDefaults(BridgeClientConfig config)
        {
            config.DisplayName = "OfisISG Quest";
            config.AppVersion = Application.version;
            config.DiscoveryEnabled = true;
            config.DiscoveryPort = ProtocolConstants.DiscoveryPort;
            config.DiscoveryTimeoutSeconds = 5f;
            config.BroadcastIntervalMilliseconds = 1000;
            config.UseBroadcastDiscovery = true;
            // Manual host is used only as fallback after discovery. Quest must not use loopback.
            config.ManualBridgeHost = "127.0.0.1";
            config.AllowManualHostFallback = true;
            config.GatewayUrlOverride = string.Empty;
            config.AllowLoopbackManualEndpoint = true;
            config.AllowCleartextWs = true;
            config.ConnectTimeoutMilliseconds = 5000;
            config.MaximumMessageBytes = ProtocolConstants.MaxMessageBytes;
            config.SupportedCapabilities = new[] { "session", "result", "session-resume", "cancellation" };
            config.SupportedModuleIds = new[] { "fire-training", "default" };
            config.HandshakeTimeoutSeconds = ProtocolConstants.HandshakeTimeoutSeconds;
            config.ReconnectDelaysSeconds = new[] { 1f, 2f, 4f, 8f, 15f };
            config.ReconnectJitterMaxSeconds = 0.5f;
            config.DefaultHeartbeatIntervalMilliseconds = 2000;
            config.DefaultStaleThresholdMilliseconds = 7000;
            config.DefaultOfflineThresholdMilliseconds = 15000;
            config.IdempotencyStoreMaxEntries = 256;
            config.ResultOutboxMaxRetries = 8;
            config.EnablePairingPanel = true;
            config.EnableDiagnosticsPanel = true;
            config.DefaultLocale = "tr";
            config.VerboseProtocolDiagnostics = false;

            var errors = config.Validate();
            foreach (var error in errors)
            {
                Debug.LogError($"[VRBridge] Config validation: {error}");
            }
        }
    }
}
#endif
