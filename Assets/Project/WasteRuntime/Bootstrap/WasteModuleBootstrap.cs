using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
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

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Hub entry for the office-waste module. Lives only in Waste_Boot.
    /// Direct Play with no Hub Initialize waits for FireServiceInstaller, then loads WasteLogin.
    /// Does not start Office Fire login and does not use OfficeFireBootstrapper.
    /// </summary>
    [Preserve]
    public sealed class WasteModuleBootstrap : MonoBehaviour, IModuleBootstrap
    {
        public const string CanonicalModuleId = "office-waste";
        public const string LoginSceneGroup = "WasteLogin";
        public const string OfficeSceneGroup = "Waste_Office";
        const string LoginSceneName = "WasteLogin";

        bool _loadStarted;

        void Awake()
        {
            LogBoot("stage=awake");
        }

        void OnEnable()
        {
            StandalonePlatform.ApplyIfNoHubOverride();

            if (ServiceLocator.TryGet<IModuleBootstrap>(out IModuleBootstrap current))
            {
                if (ReferenceEquals(current, this))
                    return;

                Debug.LogError(
                    "[WasteModuleBootstrap] Another IModuleBootstrap is already registered (" +
                    current.GetType().Name +
                    "). Waste_Boot is expected to be the only module bootstrap in this scene. " +
                    "This instance will not unregister or replace it.",
                    this);
                return;
            }

            ServiceLocator.Register<IModuleBootstrap>(this);
        }

        void Start()
        {
            if (_loadStarted || WasteHubLaunch.IsHubLaunch)
            {
                LogBoot(WaitingForServicesBody());
                return;
            }

            StartCoroutine(StandaloneLoginAfterServices());
        }

        /// <summary>
        /// Direct Play of Waste_Boot has no Hub call to <see cref="Initialize"/>.
        /// Waits until FireServiceInstaller has registered SceneLoader and IPlayerService,
        /// then loads WasteLogin in Single mode so Waste_Boot is not left at scene index 0.
        /// A Hub <see cref="Initialize"/> during this wait does not enter the load.
        /// </summary>
        IEnumerator StandaloneLoginAfterServices()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (_loadStarted || WasteHubLaunch.IsHubLaunch)
                {
                    LogBoot(WaitingForServicesBody());
                    yield break;
                }

                if (SceneLoaderAvailable())
                    break;

                yield return null;
            }

            LogBoot(WaitingForServicesBody());

            if (_loadStarted || WasteHubLaunch.IsHubLaunch)
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

            if (_loadStarted || WasteHubLaunch.IsHubLaunch)
                yield break;

            _loadStarted = true;
            LogBoot(
                "stage=standalone-login-load-begin\n" +
                "key=" + LoginSceneGroup);

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
                "hubLaunch=" + (WasteHubLaunch.IsHubLaunch ? "true" : "false") + "\n" +
                "resolvedMode=" + StandalonePlatform.Describe(WasteHubLaunch.IsHubLaunch, WasteHubLaunch.IsVr);
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
            Debug.Log("[WasteBoot]\n" + body);
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

            if (!TryReadWasteLaunch(context, out string moduleId, out string moduleVersion))
            {
                Debug.LogError(
                    "[WasteModuleBootstrap] Refusing launch. Expected module id '" + CanonicalModuleId + "'. " +
                    "context.ModuleId='" + (context?.ModuleId ?? string.Empty) + "' " +
                    "TargetModule.Id='" + (context?.TargetModule?.Id ?? string.Empty) + "'.",
                    this);
                return;
            }

            _loadStarted = true;
            WasteHubLaunch.Set(moduleId, moduleVersion, ReadPlatformId(context));

            if (!await WaitForLiveSceneLoaderAsync(10f))
            {
                _loadStarted = false;
                Debug.LogError(
                    "[WasteModuleBootstrap] ISceneLoaderService is missing. " +
                    "Waste_Boot includes FireServiceInstaller, which registers SceneLoader.",
                    this);
                return;
            }

            if (!ServiceLocator.TryGet<ISceneLoaderService>(out ISceneLoaderService sceneLoader) || sceneLoader == null)
            {
                _loadStarted = false;
                Debug.LogError("[WasteModuleBootstrap] ISceneLoaderService is missing.", this);
                return;
            }

            WasteHubLaunch.ApplyPlatformToLoadedPortingAssets();

            Debug.Log(
                "[WasteModuleBootstrap] Loading scene group '" + LoginSceneGroup + "' " +
                "moduleId=" + moduleId + " version=" + moduleVersion +
                " platform=" + WasteHubLaunch.PlatformId + ". " +
                "Waste_Office group '" + OfficeSceneGroup + "' is registered in Phase 2B.",
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

        static bool TryReadWasteLaunch(ModuleLaunchContext context, out string moduleId, out string moduleVersion)
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

        static async Task<bool> WaitForLiveSceneLoaderAsync(float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (ServiceLocator.TryGet<ISceneLoaderService>(out ISceneLoaderService loader) && loader != null)
                    return true;

                await Task.Delay(16);
            }

            return ServiceLocator.TryGet<ISceneLoaderService>(out ISceneLoaderService finalLoader) && finalLoader != null;
        }
    }

    /// <summary>
    /// Hub launch flag and platform for the current Waste module session.
    /// Does not copy Hub account identity into the Waste participant login.
    /// </summary>
    public static class WasteHubLaunch
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
