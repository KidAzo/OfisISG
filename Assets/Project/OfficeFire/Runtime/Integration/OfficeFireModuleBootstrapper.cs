using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Woi.Settings;
using WOI.Modules.SDK;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Requests gameplay (or other) loads through your <see cref="SceneLoader"/> (assigned or from <see cref="ServiceLocator"/>)
    /// as <see cref="ISceneLoaderService"/> — does not call Unity <c>SceneManager</c> directly.
    /// </summary>
    public sealed class OfficeFireModuleBootstrapper : MonoBehaviour
    {
        [Header("Scene Loader")]
        [Tooltip("Assign your SceneLoader here to use it directly. If empty, ISceneLoaderService is resolved from ServiceLocator.")]
        [SerializeField]
        private SceneLoader sceneLoader;

        [Header("Scene")]
        [Tooltip("Must match a SceneGroup GroupName configured on the project's SceneLoader (same string passed to SceneLoader.LoadScene).")]
        [SerializeField]
        private string desiredSceneName;

        [SerializeField]
        private bool loadOnStart = true;

        [SerializeField]
        private float loadDelay;

        [Header("Safety")]
        [SerializeField]
        private bool preventDuplicateLoad = true;

        [SerializeField]
        private bool keepBootstrapperAlive = true;

        private bool isLoading;

        public bool IsLoading => isLoading;

        private void Awake()
        {
            if (sceneLoader == null)
            {
                sceneLoader = GetComponent<SceneLoader>();
            }

            if (keepBootstrapperAlive)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
            if (loadOnStart)
            {
                LoadDesiredScene();
            }
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

            ISceneLoaderService loader = sceneLoader;
            if (loader == null)
            {
                if (!ServiceLocator.TryGet<ISceneLoaderService>(out ISceneLoaderService fromLocator) || fromLocator == null)
                {
                    Debug.LogError(
                        "[OfficeFireModuleBootstrapper] No SceneLoader assigned and ISceneLoaderService is not registered in ServiceLocator. " +
                        "Assign SceneLoader on this component, put SceneLoader on the same GameObject, or register ISceneLoaderService (e.g. OfficeFireSceneLoaderServiceBinder).",
                        this);
                    isLoading = false;
                    yield break;
                }

                loader = fromLocator;
            }

            string loaderSource = sceneLoader != null ? "assigned SceneLoader" : "ServiceLocator (ISceneLoaderService)";
            Debug.Log(
                $"[OfficeFireModuleBootstrapper] Requesting load for scene group '{sceneName}' via {loaderSource}.",
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
