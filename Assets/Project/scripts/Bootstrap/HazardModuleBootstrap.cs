using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using Woi.Player;
using Woi.Settings;
using WOI.Modules.SDK;
using WOI.Modules.SDK.Contracts;
using WOI.Modules.SDK.Data;

namespace Woi.Settings
{
    /// <summary>
    /// Hub entry for the office-hazard module. Lives only in <c>Hazard_Boot</c>.
    /// Registers <see cref="IModuleBootstrap"/> when the slot is empty. A different live bootstrap is left
    /// registered and logged. Unregisters on disable only when this instance is still the registered bootstrap.
    /// Does not use DontDestroyOnLoad.
    /// </summary>
    [Preserve]
    public sealed class HazardModuleBootstrap : MonoBehaviour, IModuleBootstrap
    {
        public const string CanonicalModuleId = "office-hazard";
        public const string LoginSceneGroup = "HazardHunt_Login";
        public const string OfficeSceneGroup = "HazardHunt_Office";
        const string LoginSceneName = "LoginScreen";

        bool _loadStarted;

        void Awake()
        {
            LogBoot("stage=awake");
        }

        void OnEnable()
        {
            HazardHubLaunch.ApplyEditorStandaloneFallback();

            if (ServiceLocator.TryGet<IModuleBootstrap>(out IModuleBootstrap current))
            {
                if (ReferenceEquals(current, this))
                    return;

                Debug.LogError(
                    "[HazardModuleBootstrap] Another IModuleBootstrap is already registered (" +
                    current.GetType().Name +
                    "). Hazard_Boot is expected to be the only module bootstrap in this scene. " +
                    "This instance will not unregister or replace it.",
                    this);
                return;
            }

            ServiceLocator.Register<IModuleBootstrap>(this);
        }

        void Start()
        {
            if (_loadStarted || HazardHubLaunch.IsHubLaunch)
            {
                LogBoot(WaitingForServicesBody());
                return;
            }

            StartCoroutine(StandaloneLoginAfterServices());
        }

        /// <summary>
        /// Direct Play of Hazard_Boot has no Hub call to <see cref="Initialize"/>.
        /// Waits until FireServiceInstaller has registered SceneLoader and IPlayerService,
        /// then loads HazardHunt_Login.
        /// A Hub <see cref="Initialize"/> during this wait does not enter the load.
        /// </summary>
        IEnumerator StandaloneLoginAfterServices()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (_loadStarted || HazardHubLaunch.IsHubLaunch)
                {
                    LogBoot(WaitingForServicesBody());
                    yield break;
                }

                if (SceneLoaderAvailable())
                    break;

                yield return null;
            }

            LogBoot(WaitingForServicesBody());

            if (_loadStarted || HazardHubLaunch.IsHubLaunch)
                yield break;

            if (!SceneLoaderAvailable())
            {
                LogBoot(
                    "stage=standalone-login-load-complete\n" +
                    "success=false\n" +
                    "error=ISceneLoaderService was not registered");
                yield break;
            }

            LogBoot("stage=services-ready");
            yield return null;

            if (_loadStarted || HazardHubLaunch.IsHubLaunch)
                yield break;

            _loadStarted = true;
            LogBoot(
                "stage=standalone-login-load-begin\n" +
                "key=" + LoginSceneGroup);

            // SceneLoader.LoadScene is additive and never unloads scene index 0.
            // Play from Hazard_Boot makes that scene index 0, so the group load cannot leave it.
            bool addressableKeyFound = false;
            string addressableError = null;
            AsyncOperationHandle<IList<IResourceLocation>> locations = default;
            bool locationsHandleOpen = false;
            try
            {
                locations = Addressables.LoadResourceLocationsAsync(LoginSceneGroup);
                locationsHandleOpen = true;
            }
            catch (Exception exception)
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
                        : "Addressables has no location for " + LoginSceneGroup;
                }

                if (locations.IsValid())
                    Addressables.Release(locations);
            }

            if (addressableKeyFound)
            {
                AsyncOperationHandle<SceneInstance> handle;
                try
                {
                    handle = Addressables.LoadSceneAsync(LoginSceneGroup, LoadSceneMode.Single);
                }
                catch (Exception exception)
                {
                    LoadLoginSceneThroughSceneManager(exception.Message);
                    yield break;
                }

                handle.Completed += completed =>
                {
                    if (completed.Status == AsyncOperationStatus.Succeeded)
                    {
                        LogBoot(
                            "stage=standalone-login-load-complete\n" +
                            "success=true\n" +
                            "error=none");
                        return;
                    }

                    if (completed.IsValid())
                        Addressables.Release(completed);

                    LoadLoginSceneThroughSceneManager(DescribeLoadError(completed.OperationException));
                };
                yield break;
            }

            LoadLoginSceneThroughSceneManager(addressableError);
        }

        static void LoadLoginSceneThroughSceneManager(string addressableError)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(LoginSceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                LogBoot(
                    "stage=standalone-login-load-complete\n" +
                    "success=false\n" +
                    "error=" + (addressableError ?? "no location") +
                    "; SceneManager could not load " + LoginSceneName);
                return;
            }

            operation.completed += _ =>
            {
                bool loaded = SceneManager.GetSceneByName(LoginSceneName).isLoaded;
                LogBoot(
                    "stage=standalone-login-load-complete\n" +
                    "success=" + (loaded ? "true" : "false") + "\n" +
                    "error=" + (loaded ? "none" : (addressableError ?? "SceneManager load failed")));
            };
        }

        static string WaitingForServicesBody()
        {
            return
                "stage=waiting-for-services\n" +
                "sceneLoaderAvailable=" + (SceneLoaderAvailable() ? "true" : "false") + "\n" +
                "playerServiceAvailable=" + (PlayerServiceAvailable() ? "true" : "false") + "\n" +
                "hubLaunch=" + (HazardHubLaunch.IsHubLaunch ? "true" : "false");
        }

        static bool SceneLoaderAvailable()
        {
            return ServiceLocator.TryGet<ISceneLoaderService>(out ISceneLoaderService loader) && loader != null;
        }

        static bool PlayerServiceAvailable()
        {
            return ServiceLocator.TryGet<IPlayerService>(out IPlayerService player) && player != null;
        }

        static void LogBoot(string body)
        {
            Debug.Log("[HazardBoot]\n" + body);
        }

        static string DescribeLoadError(Exception exception)
        {
            if (exception == null)
                return "Addressables load failed";
            return string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message;
        }

        void OnDisable()
        {
            if (ServiceLocator.TryGet<IModuleBootstrap>(out IModuleBootstrap current) && ReferenceEquals(current, this))
                ServiceLocator.Unregister<IModuleBootstrap>();
        }

        public async Task Initialize(ModuleLaunchContext context)
        {
            if (_loadStarted)
                return;

            if (!TryReadHazardLaunch(context, out string moduleId, out string moduleVersion))
            {
                Debug.LogError(
                    "[HazardModuleBootstrap] Refusing launch. Expected module id '" + CanonicalModuleId + "'. " +
                    "context.ModuleId='" + (context?.ModuleId ?? string.Empty) + "' " +
                    "TargetModule.Id='" + (context?.TargetModule?.Id ?? string.Empty) + "'.",
                    this);
                return;
            }

            _loadStarted = true;
            HazardHubLaunch.Set(moduleId, moduleVersion, ReadPlatformId(context));

            if (!await WaitForLiveSceneLoaderAsync(10f))
            {
                _loadStarted = false;
                Debug.LogError(
                    "[HazardModuleBootstrap] ISceneLoaderService is missing. " +
                    "Hazard_Boot includes FireServiceInstaller, which registers SceneLoader.",
                    this);
                return;
            }

            if (!ServiceLocator.TryGet<ISceneLoaderService>(out ISceneLoaderService sceneLoader) || sceneLoader == null)
            {
                _loadStarted = false;
                Debug.LogError("[HazardModuleBootstrap] ISceneLoaderService is missing.", this);
                return;
            }

            HazardHubLaunch.ApplyPlatformToLoadedPortingAssets();

            Debug.Log(
                "[HazardModuleBootstrap] Loading scene group '" + LoginSceneGroup + "' " +
                "moduleId=" + moduleId + " version=" + moduleVersion +
                " platform=" + HazardHubLaunch.PlatformId + ".",
                this);

            await sceneLoader.LoadScene(LoginSceneGroup);
        }

        static string ReadPlatformId(ModuleLaunchContext context)
        {
            if (context == null)
                return string.Empty;

            PropertyInfo property = context.GetType().GetProperty("PlatformId");
            if (property == null || property.PropertyType != typeof(string))
                return string.Empty;

            return property.GetValue(context) as string ?? string.Empty;
        }

        static bool TryReadHazardLaunch(ModuleLaunchContext context, out string moduleId, out string moduleVersion)
        {
            moduleId = null;
            moduleVersion = null;
            if (context == null)
                return false;

            string contextId = context.ModuleId;
            string definitionId = context.TargetModule != null ? context.TargetModule.Id : null;
            bool contextSet = !string.IsNullOrWhiteSpace(contextId);
            bool definitionSet = !string.IsNullOrWhiteSpace(definitionId);
            bool contextMatches = string.Equals(contextId, CanonicalModuleId, StringComparison.Ordinal);
            bool definitionMatches = string.Equals(definitionId, CanonicalModuleId, StringComparison.Ordinal);

            if (contextSet && !contextMatches)
                return false;
            if (definitionSet && !definitionMatches)
                return false;
            if (!contextMatches && !definitionMatches)
                return false;

            moduleId = CanonicalModuleId;
            moduleVersion = context.TargetModule != null && context.TargetModule.Version != null
                ? context.TargetModule.Version
                : string.Empty;
            return true;
        }

        static async UniTask<bool> WaitForLiveSceneLoaderAsync(float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (ServiceLocator.TryGet<ISceneLoaderService>(out ISceneLoaderService loader) && loader != null)
                    return true;

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            return ServiceLocator.TryGet<ISceneLoaderService>(out ISceneLoaderService finalLoader) && finalLoader != null;
        }
    }

    /// <summary>
    /// Module id and version captured from <see cref="ModuleLaunchContext"/> for the current Hub launch.
    /// Not a DontDestroyOnLoad object. Cleared at Play Mode start, then set again by the next accepted Hub launch.
    /// </summary>
    public static class HazardHubLaunch
    {
        public static string ModuleId { get; private set; } = string.Empty;
        public static string ModuleVersion { get; private set; } = string.Empty;
        public static string PlatformId { get; private set; } = string.Empty;
        public static bool HasLaunch { get; private set; }
        public static bool IsHubLaunch => HasLaunch;

        /// <summary>
        /// Play Mode can keep static Hub state when domain reload is disabled.
        /// This runs before scenes load. Hub Initialize in the same session calls <see cref="Set"/> after it.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPlayModeStatics()
        {
            ModuleId = string.Empty;
            ModuleVersion = string.Empty;
            PlatformId = string.Empty;
            HasLaunch = false;
            StandalonePlatform.ClearHubOverride();
        }

        public static bool IsVr =>
            string.Equals(PlatformId, "vr", StringComparison.OrdinalIgnoreCase)
            || string.Equals(PlatformId, "xr", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Hub platform id, otherwise the shared standalone selector (Quest still wins over the selector).
        /// </summary>
        public static bool UseVrPresentation => StandalonePlatform.UseVr(IsHubLaunch, IsVr);

        /// <summary>
        /// Applies the shared selector when Hub has not launched this module.
        /// Hub <see cref="Set"/> replaces it. PC still stops the Editor OpenXR loader; VR starts it.
        /// </summary>
        public static void ApplyEditorStandaloneFallback()
        {
            StandalonePlatform.ApplyIfNoHubOverride();
        }

        internal static void Set(string moduleId, string moduleVersion, string platformId)
        {
            ModuleId = moduleId ?? string.Empty;
            ModuleVersion = moduleVersion ?? string.Empty;
            PlatformId = string.IsNullOrWhiteSpace(platformId) ? "pc" : platformId.Trim();
            HasLaunch = true;
            StandalonePlatform.NotifyHubLaunch(IsVr);
        }

        public static void ApplyPlatformToLoadedPortingAssets()
        {
            if (!IsHubLaunch)
                return;

            StandalonePlatform.ApplyToLoadedPortingAssets(IsVr ? AppMode.XR : AppMode.PC);
        }
    }
}
