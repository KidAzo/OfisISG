using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Woi.Game.Training.VrBridge;
using Woi.OfficeFire;
using Woi.Settings;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Connection;
using WOI.Modules.SDK;

namespace Woi.OfficeFire.VrBridge
{
    /// <summary>
    /// Application-level module launcher that maps Bridge session commands onto Office Fire login/bootstrap.
    /// </summary>
    public sealed class OfficeFireBridgeModuleLauncher : MonoBehaviour, IBridgeModuleLauncher
    {
        [SerializeField] VrBridgeClient _bridgeClient;
        [SerializeField] BridgeModuleLauncherHost _host;
        [SerializeField] FireBridgeSessionAdapter _fireAdapter;
        [SerializeField] string _moduleId = "fire-training";
        [SerializeField] string _gameplaySceneGroup = "FireModule_Office";
        [SerializeField] OfficeFireScenarioId _defaultScenario = OfficeFireScenarioId.ServerRoom;

        BridgeSessionContext _reserved;

        void Awake()
        {
            if (_host != null)
            {
                _host.Launcher = this;
            }
        }

        public bool CanLaunch(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                return false;
            }

            return string.Equals(moduleId, _moduleId, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(moduleId, "default", StringComparison.OrdinalIgnoreCase);
        }

        public async UniTask LaunchSessionAsync(BridgeSessionContext context, CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!CanLaunch(context.ModuleId))
            {
                throw new InvalidOperationException($"Unsupported moduleId '{context.ModuleId}'.");
            }

            _reserved = context;

            var language = string.IsNullOrWhiteSpace(OfficeFireLoginSession.LanguageCode)
                ? "tr"
                : OfficeFireLoginSession.LanguageCode;

            OfficeFireLoginSession.Set(
                context.ParticipantFullName,
                context.PersonnelId,
                language,
                _defaultScenario);

            var client = (IVrBridgeClient)_bridgeClient ?? VrBridgeRuntime.Client;
            _fireAdapter?.Bind(client, context);
            _fireAdapter?.TryResolveRecorder();

            Debug.Log(
                $"[OfficeFireBridge] Launch accepted sessionId={context.SessionId} moduleId={context.ModuleId} " +
                $"personnelIdLength={context.PersonnelId?.Length ?? 0}");

            if (!ServiceLocator.TryGet<ISceneLoaderService>(out var loader) || loader == null)
            {
                Debug.LogError("[OfficeFireBridge] ISceneLoaderService missing — cannot load FireModule_Office.");
                _reserved = null;
                throw new InvalidOperationException("Scene loader unavailable.");
            }

            Task loadTask = loader.LoadScene(_gameplaySceneGroup);
            if (loadTask != null)
            {
                await loadTask.AsUniTask().AttachExternalCancellation(cancellationToken);
            }

            // After load, re-bind recorder in the office scene.
            _fireAdapter?.TryResolveRecorder();
            _fireAdapter?.EnsureBoundClient(client);

            // If recorder already started (or auto-start scenario begins soon), readiness is signaled by adapter.
            Debug.Log($"[OfficeFireBridge] Gameplay scene group '{_gameplaySceneGroup}' load requested.");
        }

        public UniTask CancelSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            Debug.Log($"[OfficeFireBridge] Cancel requested for session {sessionId}");
            if (_reserved != null && string.Equals(_reserved.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            {
                _reserved = null;
            }

            return UniTask.CompletedTask;
        }
    }
}
