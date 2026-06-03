using System.Collections;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Systems.SceneManagement;
using Woi.InputSystem;
using Woi.Player;
using Woi.Settings;
using WOI.Modules.SDK;

namespace WOI.Module.Fire.DI
{
    /// <summary>
    /// Lives in the Fire bootstrap scene. Loads core managers via Addressables and registers them on the ServiceLocator.
    /// Briefly waits for AddressablesInitializationState when AddressablesStartupCacheGuard runs first.
    /// </summary>
    [DefaultExecutionOrder(-5000)]
    public sealed class FireServiceInstaller : MonoBehaviour
    {
        public static event Action OnServicesReady;

        [SerializeField] private string inputManagerAddress = "Managers/InputManager";
        [SerializeField] private string sceneLoaderAddress = "Managers/SceneLoader";

        private void Awake()
        {
            StartCoroutine(BootAfterAddressablesReady());
        }

        private IEnumerator BootAfterAddressablesReady()
        {
            Debug.Log("[FireServiceInstaller] Awake — waiting for Addressables startup guard (up to ~3s)…");

            int frames = 0;
            const int maxFramesWaitForGuard = 180;
            while (!AddressablesInitializationState.StartupGuardFinished && frames < maxFramesWaitForGuard)
            {
                frames++;
                yield return null;
            }

            if (!AddressablesInitializationState.StartupGuardFinished)
            {
                Debug.LogWarning(
                    "[FireServiceInstaller] AddressablesStartupCacheGuard did not report in time — continuing (Addressables may lazy-init). Add guard to bootstrap scene for cache/version safety.");
            }
            else if (!AddressablesInitializationState.InitializationSucceeded)
            {
                Debug.LogError("[FireServiceInstaller] Addressables initialization failed — manager loads may stall.");
            }

            Debug.Log("[FireServiceInstaller] Awake START");

            var inputManager = LoadAndInstantiate<InputManager>(inputManagerAddress);
            var sceneLoader = LoadAndInstantiate<SceneLoader>(sceneLoaderAddress);

            if (inputManager == null || sceneLoader == null)
            {
                Debug.LogError("[FireServiceInstaller] Failed to load InputManager or SceneLoader from Addressables.");
                Debug.Log("[FireServiceInstaller] Awake END (failed)");
                yield break;
            }

            DontDestroyOnLoad(inputManager.gameObject);
            DontDestroyOnLoad(sceneLoader.gameObject);

            inputManager.InitializePortingRuntime();

            // Start() may run next frame; ensure gameplay map + Soap chain for any early player.
            inputManager.EnsurePcGameplayInputEnabled();

            Debug.Log("[FireServiceInstaller] InstallBindings START");

            ServiceLocator.Register<IInputProvider>(inputManager);
            ServiceLocator.Register<InputManager>(inputManager);
            ServiceLocator.Register<ISceneLoaderService>(sceneLoader);
            ServiceLocator.Register<SceneLoader>(sceneLoader);

            var playerService = new PlayerService();
            ServiceLocator.Register<IPlayerService>(playerService);

            Debug.Log("[FireServiceInstaller] InstallBindings END");

            OnServicesReady?.Invoke();
            Debug.Log("[FireServiceInstaller] Awake END");
        }

        private static T LoadAndInstantiate<T>(string address) where T : Component
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(address);
            var prefab = handle.WaitForCompletion();

            if (handle.Status != AsyncOperationStatus.Succeeded || prefab == null)
            {
                Debug.LogError($"[FireServiceInstaller] Could not load Addressable: '{address}'");
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(prefab);
            var component = instance.GetComponent<T>();

            if (component == null)
            {
                Debug.LogError($"[FireServiceInstaller] Prefab at '{address}' has no {typeof(T).Name}.");
                UnityEngine.Object.Destroy(instance);
                return null;
            }

            return component;
        }
    }
}
