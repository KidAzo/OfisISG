using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Woi.Settings;
using WOI.Modules.SDK;

namespace Woi.WasteCollectionMode.VrBridge
{
    /// <summary>
    /// Loads Waste_Office through the registered <see cref="ISceneLoaderService"/> and waits until a freshly loaded
    /// Waste_Office is the active scene with its <see cref="WasteCollectTracker"/> running.
    /// </summary>
    public sealed class WasteOfficeSceneRoute
    {
        const string SceneName = "Waste_Office";
        const float ReadyTimeoutSeconds = 60f;
        const int SettleFrames = 2;

        bool _hadPreviousScene;
        int _previousSceneHandle;

        public async UniTask LoadAsync(string sceneGroup, CancellationToken cancellationToken)
        {
            if (!TryResolveSceneLoader(out ISceneLoaderService loader))
                throw new InvalidOperationException($"ISceneLoaderService is not registered; cannot load '{sceneGroup}'.");

            // The loader skips scenes it still sees as loaded, so a Waste_Office left from the last run must not count as ready.
            _hadPreviousScene = TryFindLoadedScene(out Scene previous);
            _previousSceneHandle = _hadPreviousScene ? previous.handle : 0;

            Task loadTask = loader.LoadScene(sceneGroup);
            if (loadTask != null)
                await loadTask.AsUniTask().AttachExternalCancellation(cancellationToken);
        }

        public async UniTask WaitUntilInteractiveAsync(CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + ReadyTimeoutSeconds;
            while (!IsInteractive())
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException($"{SceneName} did not become the active, freshly loaded gameplay scene.");

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            // Lets WasteCollectionPlayerRigController.Start pick the PC or XR rig before gameplay counts as started.
            for (int i = 0; i < SettleFrames; i++)
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        bool IsInteractive()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded || active.name != SceneName)
                return false;

            if (_hadPreviousScene && active.handle == _previousSceneHandle)
                return false;

            return WasteCollectTracker.TryGetActive(out WasteCollectTracker tracker)
                   && tracker != null
                   && tracker.gameObject.scene == active;
        }

        static bool TryFindLoadedScene(out Scene scene)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (candidate.isLoaded && candidate.name == SceneName)
                {
                    scene = candidate;
                    return true;
                }
            }

            scene = default;
            return false;
        }

        static bool TryResolveSceneLoader(out ISceneLoaderService loader)
        {
            if (ServiceLocator.TryGet(out ISceneLoaderService service) && service != null)
            {
                loader = service;
                return true;
            }

            if (ServiceLocator.TryGet(out SceneLoader concrete) && concrete != null)
            {
                loader = concrete;
                return true;
            }

            loader = UnityEngine.Object.FindFirstObjectByType<SceneLoader>();
            return loader != null;
        }
    }
}
