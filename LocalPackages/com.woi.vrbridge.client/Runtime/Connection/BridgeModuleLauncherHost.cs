using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Woi.VrBridge.Client.Abstractions;

namespace Woi.VrBridge.Client.Connection
{
    public sealed class DefaultBridgeModuleLauncher : IBridgeModuleLauncher
    {
        readonly string[] _supportedModuleIds;

        public DefaultBridgeModuleLauncher(string[] supportedModuleIds)
        {
            _supportedModuleIds = supportedModuleIds ?? new[] { "default" };
        }

        public bool CanLaunch(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                return false;
            }

            foreach (var supported in _supportedModuleIds)
            {
                if (string.Equals(supported, moduleId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public UniTask LaunchSessionAsync(BridgeSessionContext context, CancellationToken cancellationToken)
        {
            Debug.Log($"[VRBridge] Module launch requested for session {context.SessionId} module {context.ModuleId}");
            return UniTask.CompletedTask;
        }

        public UniTask CancelSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            Debug.Log($"[VRBridge] Module cancel requested for session {sessionId}");
            return UniTask.CompletedTask;
        }
    }

    public sealed class BridgeModuleLauncherHost : MonoBehaviour
    {
        IBridgeModuleLauncher _launcher;

        public IBridgeModuleLauncher Launcher
        {
            get => _launcher ??= new DefaultBridgeModuleLauncher(null);
            set => _launcher = value;
        }
    }
}
