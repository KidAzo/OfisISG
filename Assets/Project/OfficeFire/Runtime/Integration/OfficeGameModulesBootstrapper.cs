using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using Woi.Settings;
using WOI.Modules.SDK;
using WOI.Modules.SDK.Contracts;
using WOI.Modules.SDK.Data;
using WOI.Module.Fire.DI;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Loads office game modules through <see cref="ISceneLoaderService"/> from <see cref="ServiceLocator"/>.
    /// Implements <see cref="IModuleBootstrap"/> so the Hub can drive the first scene load from <c>Office_Boot</c>.
    /// When <see cref="loadAfterFireInstallerReady"/> is enabled, auto-load subscribes to the static
    /// <see cref="FireServiceInstaller.OnServicesReady"/> event (fired from the Fire module after managers are registered,
    /// including <see cref="SceneLoader"/>). This is not a ServiceLocator API — it is defined on <see cref="FireServiceInstaller"/>.
    /// Does not call Unity <c>SceneManager</c> directly.
    /// </summary>
    [Preserve]
    public sealed class OfficeGameModulesBootstrapper : MonoBehaviour, IModuleBootstrap
    {
        public const string WasteLoginSceneGroup = "WasteLogin";
        public const string WasteCollectorSceneGroup = "WasteCollector";
        [Header("Scene")]
        [Tooltip("FireTraining loads FireModule_Office. WasteCollector: PC → WasteLogin, VR → FireModule_Office directly.")]
        [SerializeField]
        private OfficeGameModule gameModule = OfficeGameModule.FireTraining;

        [SerializeField]
        private bool loadOnStart = true;

        [Header("Timing (Fire module)")]
        [Tooltip(
            "When enabled with Load On Start: subscribes to the static event FireServiceInstaller.OnServicesReady " +
            "(raised after FireServiceInstaller registers SceneLoader on ServiceLocator), then calls LoadDesiredScene once. " +
            "Disable in scenes that do not run FireServiceInstaller.")]
        [SerializeField]
        private bool loadAfterFireInstallerReady = true;

        [SerializeField]
        private float loadDelay;

        [Header("Safety")]
        [SerializeField]
        private bool preventDuplicateLoad = true;

        [SerializeField]
        private bool keepBootstrapperAlive = true;

        private bool isLoading;
        private bool startupLoadIssued;

        public bool IsLoading => isLoading;

        public OfficeGameModule CurrentGameModule => gameModule;

        private void Awake()
        {
            if (keepBootstrapperAlive)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (ServiceLocator.IsRegistered<OfficeGameModulesBootstrapper>())
            {
                ServiceLocator.Unregister<OfficeGameModulesBootstrapper>();
            }

            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<IModuleBootstrap>();
            ServiceLocator.Unregister<OfficeGameModulesBootstrapper>();
        }

        private void OnEnable()
        {
            ServiceLocator.Unregister<IModuleBootstrap>();
            ServiceLocator.Register<IModuleBootstrap>(this);

            if (!loadOnStart || !loadAfterFireInstallerReady)
            {
                return;
            }

            FireServiceInstaller.OnServicesReady += OnFireServicesReady;
            if (IsSceneLoaderRegistered())
            {
                TryIssueStartupLoad("Scene loader already on ServiceLocator (installer finished before subscription)");
            }
        }

        private void OnDisable()
        {
            ServiceLocator.Unregister<IModuleBootstrap>();

            if (loadOnStart && loadAfterFireInstallerReady)
            {
                FireServiceInstaller.OnServicesReady -= OnFireServicesReady;
            }
        }

        public Task Initialize(ModuleLaunchContext context)
        {
            Debug.Log(
                $"[OfficeGameModulesBootstrapper] IModuleBootstrap.Initialize moduleId={context?.ModuleId} " +
                $"entryKey={context?.TargetModule?.EntrySceneKey}");

            TryIssueStartupLoad("IModuleBootstrap.Initialize");
            return Task.CompletedTask;
        }

        private void Start()
        {
            if (!loadOnStart)
            {
                return;
            }

            if (!loadAfterFireInstallerReady)
            {
                TryIssueStartupLoad("Start()");
            }
        }

        private void OnFireServicesReady()
        {
            TryIssueStartupLoad("FireServiceInstaller.OnServicesReady");
        }

        private void TryIssueStartupLoad(string reason)
        {
            if (startupLoadIssued)
            {
                return;
            }

            startupLoadIssued = true;
            Debug.Log($"[OfficeGameModulesBootstrapper] Auto-load from {reason}.", this);
            LoadDesiredScene();
        }

        private static bool IsSceneLoaderRegistered()
        {
            if (ServiceLocator.TryGet<ISceneLoaderService>(out ISceneLoaderService s) && s != null)
            {
                return true;
            }

            return ServiceLocator.TryGet<SceneLoader>(out SceneLoader c) && c != null;
        }

        /// <summary>
        /// Loads the scene group for the configured <see cref="gameModule"/>.
        /// </summary>
        public void LoadDesiredScene()
        {
            LoadScene(ResolveSceneGroupName(gameModule));
        }

        /// <summary>
        /// Loads the scene group for the given game module.
        /// </summary>
        public void LoadScene(OfficeGameModule module)
        {
            LoadScene(ResolveSceneGroupName(module));
        }

        private static string ResolveSceneGroupName(OfficeGameModule module)
        {
            return module switch
            {
                OfficeGameModule.FireTraining => "OfficeFireModule_Login",
                OfficeGameModule.WasteCollector => ResolveWasteCollectorEntryScene(),
                _ => "OfficeFireModule_Login",
            };
        }

        private static string ResolveWasteCollectorEntryScene()
        {
            if (FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR)
                return WasteCollectorSceneGroup;

            return WasteLoginSceneGroup;
        }

        /// <summary>
        /// Loads the waste collection login scene group.
        /// </summary>
        public void LoadWasteLogin()
        {
            LoadScene(WasteLoginSceneGroup);
        }

        /// <summary>
        /// Loads the waste collection gameplay scene group (after login).
        /// </summary>
        public void LoadWasteCollectorGameplay()
        {
            LoadScene(WasteCollectorSceneGroup);
        }

        /// <summary>
        /// Loads the given scene group name via the registered scene loader service.
        /// </summary>
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[OfficeGameModulesBootstrapper] LoadScene ignored: scene name is null or empty.");
                return;
            }

            if (preventDuplicateLoad && isLoading)
            {
                Debug.LogWarning(
                    $"[OfficeGameModulesBootstrapper] LoadScene('{sceneName}') ignored: a load is already in progress.");
                return;
            }

            StartCoroutine(LoadRoutine(sceneName.Trim()));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            isLoading = true;

            if (loadDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(loadDelay);
            }

            if (!ServiceLocator.TryGet<ISceneLoaderService>(out ISceneLoaderService loader) || loader == null)
            {
                if (ServiceLocator.TryGet<SceneLoader>(out SceneLoader concreteLoader) && concreteLoader != null)
                {
                    loader = concreteLoader;
                }
            }

            if (loader == null)
            {
                Debug.LogError(
                    "[OfficeGameModulesBootstrapper] No scene loader in ServiceLocator. " +
                    "Register ISceneLoaderService or SceneLoader (e.g. FireServiceInstaller or OfficeFireSceneLoaderServiceBinder) before loading.",
                    this);
                isLoading = false;
                yield break;
            }

            Debug.Log(
                $"[OfficeGameModulesBootstrapper] Requesting load for scene group '{sceneName}' via ServiceLocator (scene loader).",
                this);

            Task loadTask;
            try
            {
                loadTask = loader.LoadScene(sceneName);
            }
            catch (System.Exception ex)
            {
                Debug.LogError(
                    $"[OfficeGameModulesBootstrapper] LoadScene('{sceneName}') threw: {ex.Message}",
                    this);
                Debug.LogException(ex, this);
                isLoading = false;
                yield break;
            }

            if (loadTask == null)
            {
                Debug.LogWarning(
                    $"[OfficeGameModulesBootstrapper] LoadScene('{sceneName}') returned null; treating as completed.",
                    this);
                isLoading = false;
                yield break;
            }

            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            if (loadTask.IsFaulted)
            {
                Debug.LogError(
                    $"[OfficeGameModulesBootstrapper] Load task faulted for scene group '{sceneName}'.",
                    this);
                if (loadTask.Exception != null)
                {
                    Debug.LogException(loadTask.Exception.GetBaseException(), this);
                }

                isLoading = false;
                yield break;
            }

            Debug.Log(
                $"[OfficeGameModulesBootstrapper] SceneLoader completed load request for scene group '{sceneName}'.",
                this);
            isLoading = false;
        }
    }

    public enum OfficeGameModule
    {
        FireTraining,
        WasteCollector,
    }
}
