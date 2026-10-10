using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Woi.VrBridge.Client.Abstractions;

namespace Woi.WasteCollectionMode.VrBridge
{
    /// <summary>
    /// office-waste launcher for the Bridge module router. Only runs after the player pressed Start
    /// (VrBridgeClient.StartAuthorizedTrainingAsync): binds the session to the Waste run, loads Waste_Office through
    /// the registered scene loader, and reports session.started once the scene is interactive.
    /// </summary>
    public sealed class WasteBridgeModuleLauncher : IBridgeModuleLauncher
    {
        public const string ModuleId = WasteModuleBootstrap.CanonicalModuleId;
        public const string GameplaySceneGroup = WasteModuleBootstrap.OfficeSceneGroup;

        readonly WasteBridgeSessionAdapter _adapter;
        readonly Func<string, CancellationToken, UniTask> _loadSceneGroup;
        readonly Func<CancellationToken, UniTask> _waitForGameplayReady;

        public WasteBridgeModuleLauncher(
            WasteBridgeSessionAdapter adapter,
            Func<string, CancellationToken, UniTask> loadSceneGroup = null,
            Func<CancellationToken, UniTask> waitForGameplayReady = null)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            var route = new WasteOfficeSceneRoute();
            _loadSceneGroup = loadSceneGroup ?? route.LoadAsync;
            _waitForGameplayReady = waitForGameplayReady ?? route.WaitUntilInteractiveAsync;
        }

        public bool CanLaunch(string moduleId)
        {
            return !string.IsNullOrWhiteSpace(moduleId)
                   && string.Equals(moduleId.Trim(), ModuleId, StringComparison.OrdinalIgnoreCase);
        }

        public async UniTask LaunchSessionAsync(BridgeSessionContext context, CancellationToken cancellationToken)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!CanLaunch(context.ModuleId))
                throw new InvalidOperationException($"Unsupported moduleId '{context.ModuleId}'.");

            _adapter.Bind(context);
            try
            {
                await _loadSceneGroup(GameplaySceneGroup, cancellationToken);
                await _waitForGameplayReady(cancellationToken);
            }
            catch
            {
                // VrBridgeClient reports session.failed for a launch that throws.
                _adapter.Reset();
                throw;
            }

            _adapter.MarkGameplayReady();
        }

        public UniTask CancelSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            _adapter.CancelIfBound(sessionId);
            return UniTask.CompletedTask;
        }
    }
}
