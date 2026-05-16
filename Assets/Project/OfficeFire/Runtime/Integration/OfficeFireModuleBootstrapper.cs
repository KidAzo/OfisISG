using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Woi.Settings;
using WOI.Modules.SDK;
using WOI.Module.Fire.DI;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Requests loads through <see cref="ISceneLoaderService"/> from <see cref="ServiceLocator"/>.
    /// When <see cref="loadAfterFireInstallerReady"/> is enabled, auto-load subscribes to the static
    /// <see cref="FireServiceInstaller.OnServicesReady"/> event (fired from the Fire module after managers are registered,
    /// including <see cref="SceneLoader"/>). This is not a ServiceLocator API — it is defined on <see cref="FireServiceInstaller"/>.
    /// Does not call Unity <c>SceneManager</c> directly.
    /// </summary>
    public sealed class OfficeFireModuleBootstrapper : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("Must match a SceneGroup GroupName configured on the project's SceneLoader (same string passed to SceneLoader.LoadScene).")]
        [SerializeField]
        private string desiredSceneName;

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

        private void Awake()
        {
            if (keepBootstrapperAlive)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            if (!loadOnStart || !loadAfterFireInstallerReady)
            {
                return;
            }

            // FireServiceInstaller.OnServicesReady — static Action invoked after ServiceLocator.Register for SceneLoader (see com.woi.module.fire).
            FireServiceInstaller.OnServicesReady += OnFireServicesReady;
            if (IsSceneLoaderRegistered())
            {
                TryIssueStartupLoad("Scene loader already on ServiceLocator (installer finished before subscription)");
            }
        }

        private void OnDisable()
        {
            if (loadOnStart && loadAfterFireInstallerReady)
            {
                FireServiceInstaller.OnServicesReady -= OnFireServicesReady;
            }
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
            Debug.Log($"[OfficeFireModuleBootstrapper] Auto-load from {reason}.", this);
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
        /// Loads <see cref="desiredSceneName"/> via the registered scene loader service.
        /// </summary>
        public void LoadDesiredScene()
        {
            LoadScene(desiredSceneName);
        }

        /// <summary>
        /// Loads the given scene group name via the registered scene loader service.
        /// </summary>
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[OfficeFireModuleBootstrapper] LoadScene ignored: scene name is null or empty.");
                return;
            }

            if (preventDuplicateLoad && isLoading)
            {
                Debug.LogWarning(
                    $"[OfficeFireModuleBootstrapper] LoadScene('{sceneName}') ignored: a load is already in progress.");
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
                    "[OfficeFireModuleBootstrapper] No scene loader in ServiceLocator. " +
                    "Register ISceneLoaderService or SceneLoader (e.g. FireServiceInstaller or OfficeFireSceneLoaderServiceBinder) before loading.",
                    this);
                isLoading = false;
                yield break;
            }

            Debug.Log(
                $"[OfficeFireModuleBootstrapper] Requesting load for scene group '{sceneName}' via ServiceLocator (scene loader).",
                this);

            Task loadTask;
            try
            {
                loadTask = loader.LoadScene(sceneName);
            }
            catch (System.Exception ex)
            {
                Debug.LogError(
                    $"[OfficeFireModuleBootstrapper] LoadScene('{sceneName}') threw: {ex.Message}",
                    this);
                Debug.LogException(ex, this);
                isLoading = false;
                yield break;
            }

            if (loadTask == null)
            {
                Debug.LogWarning(
                    $"[OfficeFireModuleBootstrapper] LoadScene('{sceneName}') returned null; treating as completed.",
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
                    $"[OfficeFireModuleBootstrapper] Load task faulted for scene group '{sceneName}'.",
                    this);
                if (loadTask.Exception != null)
                {
                    Debug.LogException(loadTask.Exception.GetBaseException(), this);
                }

                isLoading = false;
                yield break;
            }

            Debug.Log(
                $"[OfficeFireModuleBootstrapper] SceneLoader completed load request for scene group '{sceneName}'.",
                this);
            isLoading = false;
        }
    }
}
