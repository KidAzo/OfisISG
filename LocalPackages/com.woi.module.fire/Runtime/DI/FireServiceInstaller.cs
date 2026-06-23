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



        static FireServiceInstaller s_instance;



        [SerializeField] private string inputManagerAddress = "Managers/InputManager";

        [SerializeField] private string sceneLoaderAddress = "Managers/SceneLoader";



        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]

        static void ResetStatics()

        {

            s_instance = null;

        }



        private void Awake()

        {

            if (s_instance != null && s_instance != this)

            {

                Debug.Log("[FireServiceInstaller] Duplicate installer in loaded scene — destroying extra instance (DDOL installer already exists).");

                Destroy(gameObject);

                return;

            }



            s_instance = this;

            DontDestroyOnLoad(gameObject);

            StartCoroutine(BootAfterAddressablesReady());

        }



        private void OnDestroy()

        {

            if (s_instance == this)

                s_instance = null;

        }



        /// <summary>

        /// Hub module relaunch / close: remove Fire DDOL managers and ServiceLocator entries so bootstrap can register cleanly.

        /// </summary>

        public static void TeardownModuleServices()

        {

            DestroyRegisteredManager<InputManager>();

            DestroyRegisteredManager<SceneLoader>();



            ServiceLocator.Unregister<IInputProvider>();

            ServiceLocator.Unregister<InputManager>();

            ServiceLocator.Unregister<ISceneLoaderService>();

            ServiceLocator.Unregister<SceneLoader>();

            ServiceLocator.Unregister<IPlayerService>();



            if (s_instance != null)

            {

                Destroy(s_instance.gameObject);

                s_instance = null;

            }

        }



        static void DestroyRegisteredManager<T>() where T : Component

        {

            if (!ServiceLocator.TryGet<T>(out var component) || component == null)

                return;



            if (component.gameObject != null)

                Destroy(component.gameObject);

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



            if (TryNotifyServicesAlreadyReady())

                yield break;



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



            Debug.Log("[FireServiceInstaller] InstallBindings START");



            RegisterOrReplace<IInputProvider>(inputManager);

            RegisterOrReplace<InputManager>(inputManager);

            RegisterOrReplace<ISceneLoaderService>(sceneLoader);

            RegisterOrReplace<SceneLoader>(sceneLoader);



            var playerService = new PlayerService();

            RegisterOrReplace<IPlayerService>(playerService);



            Debug.Log("[FireServiceInstaller] InstallBindings END");



            OnServicesReady?.Invoke();

            Debug.Log("[FireServiceInstaller] Awake END");

        }



        static bool TryNotifyServicesAlreadyReady()

        {

            if (!ServiceLocator.TryGet<ISceneLoaderService>(out var loader) || loader == null)

                return false;

            if (!ServiceLocator.TryGet<IInputProvider>(out var input) || input == null)

                return false;



            Debug.Log("[FireServiceInstaller] Core services already registered — skipping duplicate install.");

            OnServicesReady?.Invoke();

            return true;

        }



        static void RegisterOrReplace<T>(T service) where T : class

        {

            if (service == null)

                return;



            if (ServiceLocator.TryGet<T>(out var existing) && ReferenceEquals(existing, service))

                return;



            ServiceLocator.Unregister<T>();

            ServiceLocator.Register(service);

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


