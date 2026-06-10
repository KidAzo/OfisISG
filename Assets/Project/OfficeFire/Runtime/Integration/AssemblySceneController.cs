using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Woi.Settings;
using WOI.Modules.SDK;
using Woi.UI.Announcements;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Assembly finale: fade-only scene load, teleport, announcements, result screen.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Assembly Scene Controller")]
    public sealed class AssemblySceneController : MonoBehaviour
    {
        private const string TransitionRunnerName = "OfficeFireAssemblySceneTransition";
        private const string FadeOverlayName = "OfficeFireAssemblyFadeOverlay";

        private static bool _beginWhenSceneLoads;

        [Header("Scene Load")]
        [Tooltip("SceneLoader SceneGroup GroupName for the assembly scene.")]
        [SerializeField]
        private string assemblySceneGroupName = "OutDoor";

        [SerializeField]
        private bool beginWhenSceneLoads = true;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Fade to black before the scene group loads (Archive → OutDoor).")]
        private float fadeInDurationSeconds = 0.45f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Fade from black after OutDoor loads — increase this to slow the scene reveal.")]
        private float fadeOutDurationSeconds = 0.45f;

        public float FadeInDurationSeconds => fadeInDurationSeconds;
        public float FadeOutDurationSeconds => fadeOutDurationSeconds;

        [Header("Player")]
        [SerializeField]
        private Transform playerRoot;

        [SerializeField]
        private Transform xrOriginRoot;

        [SerializeField]
        private Transform assemblyPoint;

        [Header("Announcements")]
        [SerializeField]
        private OfficeFireVoiceLineContentPresenter[] voiceLinePresenters;

        [SerializeField]
        private OfficeFireVoiceLineId[] announcements =
        {
            OfficeFireVoiceLineId.ReachAssemblyArea,
            OfficeFireVoiceLineId.ScenarioCompleted,
        };

        [Header("Result Screen")]
        [SerializeField]
        private GameObject resultScreenRoot;

        [SerializeField]
        private OfficeFireResultScreenController resultScreenController;

        [SerializeField]
        private UnityEvent onResultScreenRequested;

        [SerializeField]
        [Min(0f)]
        private float delayBeforeResultScreenSeconds = 5.5f;

        [Header("Debug")]
        [SerializeField]
        private bool beginOnStartForTesting;

        private Coroutine _sequence;

        public static void LoadAssemblyScene(string sceneGroupName = "OutDoor")
        {
            LoadAssemblyScene(sceneGroupName, 0.45f, 0.45f);
        }

        public static void LoadAssemblyScene(string sceneGroupName, float fadeInSeconds, float fadeOutSeconds)
        {
            if (string.IsNullOrWhiteSpace(sceneGroupName))
            {
                Debug.LogError("[AssemblySceneController] LoadAssemblyScene ignored: scene group name is empty.");
                return;
            }

            _beginWhenSceneLoads = true;

            GameObject runner = new GameObject(TransitionRunnerName);
            DontDestroyOnLoad(runner);
            AssemblySceneTransitionRunner transition = runner.AddComponent<AssemblySceneTransitionRunner>();
            transition.StartLoad(sceneGroupName.Trim(), fadeInSeconds, fadeOutSeconds);
        }

        private void Start()
        {
            if (beginOnStartForTesting || (beginWhenSceneLoads && _beginWhenSceneLoads))
            {
                _beginWhenSceneLoads = false;
                Begin();
            }
        }

        public void Begin()
        {
            if (_sequence != null)
            {
                StopCoroutine(_sequence);
            }

            if (resultScreenController != null)
            {
                resultScreenController.HideScreen();
            }
            else if (resultScreenRoot != null)
            {
                resultScreenRoot.SetActive(false);
            }

            _sequence = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            TeleportPlayer();

            OfficeFireVoiceLineContentPresenter voiceLinePresenter = ResolveVoiceLinePresenter();
            if (voiceLinePresenter != null && announcements != null)
            {
                for (int i = 0; i < announcements.Length; i++)
                {
                    OfficeFireVoiceLineId voiceLineId = announcements[i];
                    if (voiceLineId == OfficeFireVoiceLineId.None)
                    {
                        continue;
                    }

                    voiceLinePresenter.PlayVoiceLine(voiceLineId);
                    yield return WaitForAnnouncementFinished();
                }
            }

            if (delayBeforeResultScreenSeconds > 0f)
            {
                yield return new WaitForSeconds(delayBeforeResultScreenSeconds);
            }

            ShowResultScreen();
            _sequence = null;
        }

        private OfficeFireVoiceLineContentPresenter ResolveVoiceLinePresenter()
        {
            if (voiceLinePresenters == null || voiceLinePresenters.Length == 0)
            {
                return null;
            }

            int index = 0;
            if (OfficeFireScenarioReportHolder.TryPeek(out OfficeFireScenarioReport report))
            {
                index = GetVoiceLinePresenterIndex(report.scenarioId);
            }

            if (index < 0 || index >= voiceLinePresenters.Length)
            {
                Debug.LogWarning(
                    $"[AssemblySceneController] Voice line presenter index {index} is out of range " +
                    $"(length {voiceLinePresenters.Length}). Falling back to 0.",
                    this);
                index = 0;
            }

            return voiceLinePresenters[index];
        }

        private static int GetVoiceLinePresenterIndex(OfficeFireScenarioId scenarioId)
        {
            switch (scenarioId)
            {
                case OfficeFireScenarioId.KitchenCafe:
                    return 0;
                case OfficeFireScenarioId.ArchiveRoom:
                    return 1;
                case OfficeFireScenarioId.ServerRoom:
                    return 2;
                default:
                    return 0;
            }
        }

        private void ShowResultScreen()
        {
            OfficeFireResultScreenController screen = ResolveResultScreenController();
            if (screen != null)
            {
                if (OfficeFireScenarioReportHolder.TryConsume(out OfficeFireScenarioReport report))
                {
                    screen.Present(report);
                }
                else
                {
                    screen.Present(new OfficeFireScenarioReport());
                }
            }
            else if (resultScreenRoot != null)
            {
                resultScreenRoot.SetActive(true);
            }

            onResultScreenRequested?.Invoke();
        }

        private OfficeFireResultScreenController ResolveResultScreenController()
        {
            if (resultScreenController != null)
            {
                return resultScreenController;
            }

            if (resultScreenRoot != null &&
                resultScreenRoot.TryGetComponent(out OfficeFireResultScreenController onRoot))
            {
                return onRoot;
            }

            return FindFirstObjectByType<OfficeFireResultScreenController>(FindObjectsInactive.Include);
        }

        private void TeleportPlayer()
        {
            if (assemblyPoint == null)
            {
                Debug.LogWarning("[AssemblySceneController] Assembly point is not assigned.", this);
                return;
            }

            Transform root = ResolvePlayerRoot();
            if (root == null)
            {
                Debug.LogWarning("[AssemblySceneController] Player root is not assigned.", this);
                return;
            }

            CharacterController controller = root.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = root.GetComponentInChildren<CharacterController>();
            }

            if (controller != null)
            {
                controller.enabled = false;
            }

            root.SetPositionAndRotation(assemblyPoint.position, assemblyPoint.rotation);

            Rigidbody body = root.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = root.GetComponentInChildren<Rigidbody>();
            }

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            if (controller != null)
            {
                controller.enabled = true;
            }

            EnsureGameplayCamera(root);
        }

        private Transform ResolvePlayerRoot()
        {
            if (xrOriginRoot != null)
            {
                return xrOriginRoot;
            }

            if (playerRoot != null)
            {
                return playerRoot;
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            return taggedPlayer != null ? taggedPlayer.transform : null;
        }

        private static void EnsureGameplayCamera(Transform playerRoot)
        {
            Camera playerCamera = null;
            if (playerRoot != null)
            {
                playerCamera = playerRoot.GetComponentInChildren<Camera>(true);
            }

            if (playerCamera == null)
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                if (taggedPlayer != null)
                {
                    playerCamera = taggedPlayer.GetComponentInChildren<Camera>(true);
                }
            }

            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null)
                {
                    continue;
                }

                if (camera.gameObject.name == FadeOverlayName)
                {
                    continue;
                }

                bool isPlayerCamera = playerCamera != null && camera == playerCamera;
                if (isPlayerCamera)
                {
                    if (!camera.gameObject.activeInHierarchy)
                    {
                        camera.gameObject.SetActive(true);
                    }

                    camera.enabled = true;
                    camera.tag = "MainCamera";
                    continue;
                }

                if (camera.CompareTag("MainCamera"))
                {
                    camera.enabled = false;
                }
            }

            if (playerCamera == null)
            {
                Debug.LogWarning(
                    "[AssemblySceneController] No player camera found after scene load. " +
                    "Assign Player Root or ensure a Player-tagged object has an enabled Camera.");
            }
        }

        private static IEnumerator WaitForAnnouncementFinished()
        {
            WoiAnnouncementAudioAdapter adapter = OfficeFireAnnouncementAudioPlayback.ResolveAdapter(null);
            if (adapter == null)
            {
                yield return new WaitForSeconds(5f);
                yield break;
            }

            bool finished = false;
            void OnFinished() => finished = true;

            adapter.OnAnnouncementAudioFinished += OnFinished;
            yield return new WaitUntil(() => finished);
            adapter.OnAnnouncementAudioFinished -= OnFinished;
        }

        private sealed class AssemblySceneTransitionRunner : MonoBehaviour
        {
            public void StartLoad(string sceneGroupName, float fadeInSeconds, float fadeOutSeconds)
            {
                StartCoroutine(LoadRoutine(sceneGroupName, fadeInSeconds, fadeOutSeconds));
            }

            private IEnumerator LoadRoutine(string sceneGroupName, float fadeInSeconds, float fadeOutSeconds)
            {
                if (!TryResolveLoader(out ISceneLoaderService loader))
                {
                    Debug.LogError(
                        "[AssemblySceneController] ISceneLoaderService not found. Ensure FireServiceInstaller / SceneLoader is registered.",
                        this);
                    _beginWhenSceneLoads = false;
                    Destroy(gameObject);
                    yield break;
                }

                CanvasGroup fadeOverlay = SceneFadeOverlay.GetOrCreate(FadeOverlayName);
                fadeOverlay.gameObject.SetActive(true);
                SceneFadeOverlay.SetTransitionCameraActive(fadeOverlay, true);

                yield return SceneFadeOverlay.Fade(fadeOverlay, 0f, 1f, fadeInSeconds);

                Task loadTask = loader.LoadScene(sceneGroupName, SceneLoadPresentation.Silent);
                while (!loadTask.IsCompleted)
                {
                    yield return null;
                }

                if (loadTask.IsFaulted && loadTask.Exception != null)
                {
                    Debug.LogException(loadTask.Exception.GetBaseException(), this);
                    _beginWhenSceneLoads = false;
                    yield return SceneFadeOverlay.Fade(fadeOverlay, fadeOverlay.alpha, 0f, fadeOutSeconds);
                    SceneFadeOverlay.SetTransitionCameraActive(fadeOverlay, false);
                    fadeOverlay.gameObject.SetActive(false);
                    Destroy(gameObject);
                    yield break;
                }

                EnsureGameplayCamera(null);

                float revealFadeSeconds = ResolveFadeFromBlackSeconds(fadeOutSeconds);
                yield return SceneFadeOverlay.Fade(fadeOverlay, 1f, 0f, revealFadeSeconds);
                SceneFadeOverlay.SetTransitionCameraActive(fadeOverlay, false);
                fadeOverlay.gameObject.SetActive(false);
                Destroy(gameObject);
            }

            private static bool TryResolveLoader(out ISceneLoaderService loader)
            {
                if (ServiceLocator.TryGet(out loader) && loader != null)
                {
                    return true;
                }

                if (ServiceLocator.TryGet(out SceneLoader concreteLoader) && concreteLoader != null)
                {
                    loader = concreteLoader;
                    return true;
                }

                loader = null;
                return false;
            }

            private static float ResolveFadeFromBlackSeconds(float fallbackSeconds)
            {
                AssemblySceneController controller = FindFirstObjectByType<AssemblySceneController>();
                if (controller == null)
                {
                    return fallbackSeconds;
                }

                return controller.FadeOutDurationSeconds;
            }
        }

        private static class SceneFadeOverlay
        {
            private const string TransitionCameraName = "TransitionCamera";

            public static CanvasGroup GetOrCreate(string overlayName)
            {
                GameObject existing = GameObject.Find(overlayName);
                if (existing != null && existing.TryGetComponent(out CanvasGroup existingGroup))
                {
                    EnsureTransitionCamera(existing);
                    return existingGroup;
                }

                GameObject root = new GameObject(overlayName);
                Object.DontDestroyOnLoad(root);

                EnsureTransitionCamera(root);

                Canvas canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;
                canvas.pixelPerfect = false;

                root.AddComponent<CanvasScaler>();
                root.AddComponent<GraphicRaycaster>();

                GameObject panel = new GameObject("BlackPanel");
                panel.transform.SetParent(root.transform, false);

                RectTransform rect = panel.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                Image image = panel.AddComponent<Image>();
                image.color = Color.black;
                image.raycastTarget = true;

                CanvasGroup group = root.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
                return group;
            }

            public static void SetTransitionCameraActive(CanvasGroup group, bool active)
            {
                if (group == null)
                {
                    return;
                }

                Transform transitionCameraTransform = group.transform.Find(TransitionCameraName);
                if (transitionCameraTransform != null &&
                    transitionCameraTransform.TryGetComponent(out Camera transitionCamera))
                {
                    transitionCamera.enabled = active;
                }
            }

            private static void EnsureTransitionCamera(GameObject root)
            {
                Transform existing = root.transform.Find(TransitionCameraName);
                if (existing != null)
                {
                    return;
                }

                GameObject cameraObject = new GameObject(TransitionCameraName);
                cameraObject.transform.SetParent(root.transform, false);

                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 0;
                camera.depth = 100f;
                camera.useOcclusionCulling = false;
                camera.enabled = false;
            }

            public static IEnumerator Fade(CanvasGroup group, float from, float to, float durationSeconds)
            {
                if (group == null)
                {
                    yield break;
                }

                group.alpha = from;
                ApplyFadeState(group, from);

                if (durationSeconds <= 0f)
                {
                    group.alpha = to;
                    ApplyFadeState(group, to);
                    yield break;
                }

                float elapsed = 0f;
                while (elapsed < durationSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / durationSeconds));
                    group.alpha = alpha;
                    ApplyFadeState(group, alpha);
                    yield return null;
                }

                group.alpha = to;
                ApplyFadeState(group, to);
            }

            private static void ApplyFadeState(CanvasGroup group, float alpha)
            {
                bool solid = alpha >= 0.99f;
                group.blocksRaycasts = solid;
                group.interactable = solid;
            }
        }
    }
}
