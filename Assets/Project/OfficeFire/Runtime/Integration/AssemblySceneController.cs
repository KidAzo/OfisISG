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
        private float fadeInDurationSeconds = 0.45f;

        [SerializeField]
        [Min(0f)]
        private float fadeOutDurationSeconds = 0.45f;

        [Header("Player")]
        [SerializeField]
        private Transform playerRoot;

        [SerializeField]
        private Transform xrOriginRoot;

        [SerializeField]
        private Transform assemblyPoint;

        [Header("Announcements")]
        [SerializeField]
        private OfficeFireVoiceLineContentPresenter voiceLinePresenter;

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
        private UnityEvent onResultScreenRequested;

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

            if (resultScreenRoot != null)
            {
                resultScreenRoot.SetActive(false);
            }

            _sequence = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            TeleportPlayer();

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

            if (resultScreenRoot != null)
            {
                resultScreenRoot.SetActive(true);
            }

            onResultScreenRequested?.Invoke();
            _sequence = null;
        }

        private void TeleportPlayer()
        {
            if (assemblyPoint == null)
            {
                Debug.LogWarning("[AssemblySceneController] Assembly point is not assigned.", this);
                return;
            }

            Transform root = xrOriginRoot != null ? xrOriginRoot : playerRoot;
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
                    fadeOverlay.gameObject.SetActive(false);
                    Destroy(gameObject);
                    yield break;
                }

                yield return SceneFadeOverlay.Fade(fadeOverlay, 1f, 0f, fadeOutSeconds);
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
        }

        private static class SceneFadeOverlay
        {
            public static CanvasGroup GetOrCreate(string overlayName)
            {
                GameObject existing = GameObject.Find(overlayName);
                if (existing != null && existing.TryGetComponent(out CanvasGroup existingGroup))
                {
                    return existingGroup;
                }

                GameObject root = new GameObject(overlayName);
                DontDestroyOnLoad(root);

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
