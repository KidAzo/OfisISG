using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using Woi.Settings;
using WOI.Modules.SDK;
using WOI.Modules.SDK.Contracts;
using WOI.Modules.SDK.Data;

namespace Woi.Settings
{
    /// <summary>
    /// Hub entry for the office-hazard module. Lives only in <c>Hazard_Boot</c>.
    /// Registers <see cref="IModuleBootstrap"/> on enable and unregisters on disable when this instance
    /// is still the registered bootstrap. Does not use DontDestroyOnLoad.
    /// </summary>
    [Preserve]
    public sealed class HazardModuleBootstrap : MonoBehaviour, IModuleBootstrap
    {
        public const string CanonicalModuleId = "office-hazard";
        public const string OfficeSceneGroup = "HazardHunt_Office";

        bool _loadStarted;

        void OnEnable()
        {
            if (ServiceLocator.TryGet<IModuleBootstrap>(out IModuleBootstrap current) && !ReferenceEquals(current, this))
                ServiceLocator.Unregister<IModuleBootstrap>();

            if (!ServiceLocator.IsRegistered<IModuleBootstrap>())
                ServiceLocator.Register<IModuleBootstrap>(this);
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
            HazardHubLaunch.Set(moduleId, moduleVersion);

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

            Debug.Log(
                "[HazardModuleBootstrap] Loading scene group '" + OfficeSceneGroup + "' " +
                "moduleId=" + moduleId + " version=" + moduleVersion + ".",
                this);

            await sceneLoader.LoadScene(OfficeSceneGroup);
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
    /// Not a DontDestroyOnLoad object. Cleared only by the next accepted launch.
    /// </summary>
    public static class HazardHubLaunch
    {
        public static string ModuleId { get; private set; } = string.Empty;
        public static string ModuleVersion { get; private set; } = string.Empty;
        public static bool HasLaunch { get; private set; }

        internal static void Set(string moduleId, string moduleVersion)
        {
            ModuleId = moduleId ?? string.Empty;
            ModuleVersion = moduleVersion ?? string.Empty;
            HasLaunch = true;
        }
    }
}
