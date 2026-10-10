using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using Woi.InputSystem;
using Woi.Player;
using Woi.Settings;
using WoiUtils;
using WOI.Modules.SDK;
using WOI.Modules.SDK.Contracts;
using WOI.Modules.SDK.Data;

namespace Woi.Settings
{
    /// <summary>
    /// Hub entry point for the office-safety module. Loads the login scene group after
    /// <see cref="ISceneLoaderService"/> is registered (typically via FireServiceInstaller in Office_Boot).
    /// </summary>
    [Preserve]
    public sealed class OfficeFireBootstrapper : PersistentSingleton<OfficeFireBootstrapper>, IModuleBootstrap
    {
        [SerializeField]
        private string loginSceneGroupName = "OfficeFireModule_Login";

        [Tooltip("Same ScriptableEnumPortingVariable asset as SceneLoader / InputManager (Addressables key Managers/PortingVariable).")]
        [SerializeField]
        private ScriptableEnumPortingVariable portingSettings;

        private static int _initializeGate;
        private static bool _suppressStandaloneFireLogin;

        /// <summary>
        /// True after a Hub <see cref="Initialize"/> in this Play session.
        /// Cleared at Play Mode start so a previous session cannot block standalone.
        /// </summary>
        public static bool HubLaunchRequested { get; private set; }

        public static void MarkHubLaunchRequested()
        {
            HubLaunchRequested = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _initializeGate = 0;
            _suppressStandaloneFireLogin = false;
            HubLaunchRequested = false;
        }

        /// <summary>
        /// When true, standalone Play from the office bootstrap scene must not load Fire login.
        /// Used when another module (currently Hazard Hunt) owns first-scene navigation.
        /// Hub <see cref="Initialize"/> with a real launch context is unchanged.
        /// </summary>
        public static void SetSuppressStandaloneFireLogin(bool suppress)
        {
            _suppressStandaloneFireLogin = suppress;
        }

        private void OnEnable()
        {
            ServiceLocator.Cleared += OnServiceLocatorCleared;
            ServiceLocator.Unregister<IModuleBootstrap>();
            ServiceLocator.Register<IModuleBootstrap>(this);
        }

        private void OnDisable()
        {
            ServiceLocator.Unregister<IModuleBootstrap>();
            ServiceLocator.Cleared -= OnServiceLocatorCleared;
        }

        private void OnDestroy()
        {
            // PersistentSingleton (DontDestroyOnLoad). Hub Return-to-Hub → Teardown destroys this object;
            // without reopening the gate, the next launch's Initialize no-ops → black screen.
            Interlocked.Exchange(ref _initializeGate, 0);
            HubLaunchRequested = false;
        }

        protected override void Awake()
        {
            LogBoot("stage=awake");
            FirePlatformRuntime.TryInitialize(portingSettings);
            if (!HubLaunchRequested && !StandalonePlatform.HasHubOverride)
                StandalonePlatform.ApplyIfNoHubOverride();
            base.Awake();
        }

        private static void OnServiceLocatorCleared()
        {
            Interlocked.Exchange(ref _initializeGate, 0);
        }

        private void Start()
        {
            StartCoroutine(StandaloneEntryAfterServices());
        }

        /// <summary>
        /// Direct Play has no Hub <see cref="Initialize"/>. Wait for FireServiceInstaller, then load
        /// OfficeFireModule_Login in Single mode. Hub Initialize keeps the scene-group loader.
        /// </summary>
        private IEnumerator StandaloneEntryAfterServices()
        {
            if (_suppressStandaloneFireLogin)
            {
                LogBoot(WaitingForServicesBody());
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (HubLaunchRequested)
                {
                    LogBoot(WaitingForServicesBody());
                    yield break;
                }

                if (ServicesReady())
                    break;

                yield return null;
            }

            LogBoot(WaitingForServicesBody());

            if (HubLaunchRequested || _suppressStandaloneFireLogin)
                yield break;

            if (!ServicesReady())
            {
                LogBoot(
                    "stage=standalone-entry-load-complete\n" +
                    "success=false\n" +
                    "error=" + MissingServicesError());
                yield break;
            }

            LogBoot("stage=services-ready");
            yield return null;

            if (HubLaunchRequested)
                yield break;

            if (Interlocked.CompareExchange(ref _initializeGate, 1, 0) != 0)
                yield break;

            if (!StandalonePlatform.HasHubOverride)
                StandalonePlatform.ApplyIfNoHubOverride();

            string key = string.IsNullOrWhiteSpace(loginSceneGroupName)
                ? "OfficeFireModule_Login"
                : loginSceneGroupName.Trim();
            LogBoot(
                "stage=standalone-entry-load-begin\n" +
                "key=" + key);

            bool addressableKeyFound = false;
            string addressableError = null;
            AsyncOperationHandle<IList<IResourceLocation>> locations = default;
            bool locationsHandleOpen = false;
            try
            {
                locations = Addressables.LoadResourceLocationsAsync(key);
                locationsHandleOpen = true;
            }
            catch (System.Exception exception)
            {
                addressableError = exception.Message;
            }

            if (locationsHandleOpen)
            {
                while (!locations.IsDone)
                    yield return null;

                addressableKeyFound =
                    locations.Status == AsyncOperationStatus.Succeeded
                    && locations.Result != null
                    && locations.Result.Count > 0;
                if (!addressableKeyFound)
                {
                    addressableError = locations.OperationException != null
                        ? locations.OperationException.Message
                        : "Addressables has no location for " + key;
                }

                if (locations.IsValid())
                    Addressables.Release(locations);
            }

            if (addressableKeyFound)
            {
                AsyncOperationHandle<SceneInstance> handle;
                try
                {
                    handle = Addressables.LoadSceneAsync(key, LoadSceneMode.Single);
                }
                catch (System.Exception exception)
                {
                    LoadLoginThroughSceneManager(key, exception.Message);
                    yield break;
                }

                handle.Completed += completed =>
                {
                    if (completed.Status == AsyncOperationStatus.Succeeded)
                    {
                        LogBoot(
                            "stage=standalone-entry-load-complete\n" +
                            "success=true\n" +
                            "error=none");
                        return;
                    }

                    if (completed.IsValid())
                        Addressables.Release(completed);

                    LoadLoginThroughSceneManager(key, DescribeLoadError(completed.OperationException));
                };
                yield break;
            }

            LoadLoginThroughSceneManager(key, addressableError);
        }

        private static void LoadLoginThroughSceneManager(string sceneName, string addressableError)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                LogBoot(
                    "stage=standalone-entry-load-complete\n" +
                    "success=false\n" +
                    "error=" + (addressableError ?? "no location") +
                    "; SceneManager could not load " + sceneName);
                return;
            }

            operation.completed += _ =>
            {
                bool loaded = SceneManager.GetSceneByName(sceneName).isLoaded;
                LogBoot(
                    "stage=standalone-entry-load-complete\n" +
                    "success=" + (loaded ? "true" : "false") + "\n" +
                    "error=" + (loaded ? "none" : (addressableError ?? "SceneManager load failed")));
            };
        }

        private static string WaitingForServicesBody()
        {
            return
                "stage=waiting-for-services\n" +
                "sceneLoaderAvailable=" + (SceneLoaderAvailable() ? "true" : "false") + "\n" +
                "playerServiceAvailable=" + (PlayerServiceAvailable() ? "true" : "false") + "\n" +
                "hubLaunch=" + (HubLaunchRequested ? "true" : "false") + "\n" +
                "resolvedMode=" + StandalonePlatform.Describe(
                    StandalonePlatform.HasHubOverride,
                    StandalonePlatform.HasHubOverride && StandalonePlatform.IsVrActive);
        }

        private static bool ServicesReady()
        {
            return SceneLoaderAvailable() && PlayerServiceAvailable() && InputManagerAvailable();
        }

        private static string MissingServicesError()
        {
            if (!SceneLoaderAvailable())
                return "ISceneLoaderService was not registered";
            if (!PlayerServiceAvailable())
                return "IPlayerService was not registered";
            return "InputManager was not registered";
        }

        private static bool SceneLoaderAvailable()
        {
            return ServiceLocator.TryGet<ISceneLoaderService>(out ISceneLoaderService loader) && loader != null;
        }

        private static bool PlayerServiceAvailable()
        {
            return ServiceLocator.TryGet<IPlayerService>(out IPlayerService player) && player != null;
        }

        private static bool InputManagerAvailable()
        {
            return ServiceLocator.TryGet<InputManager>(out InputManager input) && input != null;
        }

        private static void LogBoot(string body)
        {
            Debug.Log("[FireBoot]\n" + body);
        }

        private static string DescribeLoadError(System.Exception exception)
        {
            if (exception == null)
                return "Addressables load failed";
            return string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message;
        }

        public async Task Initialize(ModuleLaunchContext context)
        {
            MarkHubLaunchRequested();
            if (Interlocked.CompareExchange(ref _initializeGate, 1, 0) != 0)
                return;

            if (!await WaitForLiveSceneLoaderAsync(10f))
            {
                Debug.LogError("[OfficeFireBootstrapper] ISceneLoaderService is missing. Ensure FireServiceInstaller ran in Office_Boot.");
                Interlocked.Exchange(ref _initializeGate, 0);
                return;
            }

            if (!ServiceLocator.TryGet<ISceneLoaderService>(out var sceneLoader) || sceneLoader == null)
            {
                Debug.LogError("[OfficeFireBootstrapper] ISceneLoaderService is missing.");
                Interlocked.Exchange(ref _initializeGate, 0);
                return;
            }

            if (ShouldSkipHubSceneLoad(context))
            {
                Debug.Log(
                    "[OfficeFireBootstrapper] Hub launch: entry scene already loaded. Skipping login scene group load.");
                return;
            }

            await sceneLoader.LoadScene(loginSceneGroupName);
        }

        private bool ShouldSkipHubSceneLoad(ModuleLaunchContext context)
        {
            if (context?.TargetModule == null)
                return false;
            if (string.IsNullOrWhiteSpace(context.TargetModule.CatalogUrl))
                return false;
            if (string.IsNullOrWhiteSpace(context.TargetModule.EntrySceneKey))
                return false;

            var entryKey = context.TargetModule.EntrySceneKey.Trim();
            if (IsBootstrapEntryKey(entryKey))
                return false;

            if (string.Equals(entryKey, loginSceneGroupName, System.StringComparison.OrdinalIgnoreCase))
                return true;

            return true;
        }

        private static bool IsBootstrapEntryKey(string entryKey)
        {
            return entryKey.Equals("Office_Boot", System.StringComparison.OrdinalIgnoreCase)
                   || entryKey.Equals("Office Boot", System.StringComparison.OrdinalIgnoreCase)
                   || entryKey.IndexOf("Bootstrap", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static async UniTask<bool> WaitForLiveSceneLoaderAsync(float timeoutSeconds, CancellationToken cancellationToken = default)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (ServiceLocator.TryGet<ISceneLoaderService>(out var loader) && loader != null)
                    return true;

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            return ServiceLocator.TryGet<ISceneLoaderService>(out var finalLoader) && finalLoader != null;
        }
    }
}
