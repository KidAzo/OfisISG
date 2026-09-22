using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Woi.VrBridge.Client.Abstractions;

namespace Woi.VrBridge.Client.Tests.Support
{
    /// <summary>
    /// Deterministic fake for <see cref="IBridgeModuleLauncher"/>. Tests assert on
    /// <see cref="LaunchCallCount"/> to prove authorization never triggers a launch.
    /// </summary>
    public sealed class FakeBridgeModuleLauncher : IBridgeModuleLauncher
    {
        readonly string[] _supportedModuleIds;

        public FakeBridgeModuleLauncher(params string[] supportedModuleIds)
        {
            _supportedModuleIds = (supportedModuleIds != null && supportedModuleIds.Length > 0)
                ? supportedModuleIds
                : new[] { "fire-training" };
        }

        public int LaunchCallCount { get; private set; }
        public int CancelCallCount { get; private set; }
        public bool ShouldThrowOnLaunch { get; set; }
        public BridgeSessionContext LastLaunchedContext { get; private set; }
        public string LastCancelledSessionId { get; private set; }

        public bool CanLaunch(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                return false;
            }

            foreach (var supported in _supportedModuleIds)
            {
                if (string.Equals(supported, moduleId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public UniTask LaunchSessionAsync(BridgeSessionContext context, CancellationToken cancellationToken)
        {
            LaunchCallCount++;
            LastLaunchedContext = context;
            if (ShouldThrowOnLaunch)
            {
                throw new InvalidOperationException("Simulated launch failure.");
            }

            return UniTask.CompletedTask;
        }

        public UniTask CancelSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            CancelCallCount++;
            LastCancelledSessionId = sessionId;
            return UniTask.CompletedTask;
        }
    }
}
