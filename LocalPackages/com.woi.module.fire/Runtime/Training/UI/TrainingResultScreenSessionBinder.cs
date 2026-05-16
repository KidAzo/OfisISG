using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using Woi.Events.Data;
using Woi.Game.Training;


namespace Woi.Game.Training.UI
{
    /// <summary>
    /// Keeps the results UI hidden while a training session runs, then shows it with the final report.
    /// Place this on an <b>always-active</b> GameObject (e.g. next to <see cref="ExtinguisherSessionRecorder"/>).
    /// The results <see cref="GameObject"/> may stay disabled in the scene until the first session ends.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainingResultScreenSessionBinder : MonoBehaviour
    {
        [SerializeField] private ExtinguisherSessionRecorder _recorder;

        [Tooltip("PC / slate sonuç kökü: UIDocument + TrainingResultScreenController (oturum boyunca kapalı tutulur).")]
        [SerializeField] private GameObject _resultsScreenRoot;

        [Tooltip("VR sonuç kökü (ör. ResultScreenVR). Boşsa yok sayılır. PC ile aynı SessionReport burada da Present edilir.")]
        [SerializeField] private GameObject _vrResultsScreenRoot;

        [Tooltip("Optional; if empty, taken from _resultsScreenRoot.")]
        [SerializeField] private TrainingResultScreenController _controller;

        [Tooltip("Optional; if empty, taken from _vrResultsScreenRoot.")]
        [SerializeField] private TrainingResultScreenController _vrController;

        [Tooltip("Wait one frame after enabling UI so UIDocument root is ready before Present.")]
        [SerializeField] private bool _presentOnNextFrame = true;

        [Tooltip("Show and unlock the hardware cursor when the results screen opens (e.g. after FPS-style locked cursor during play).")]
        [SerializeField] private bool _showCursorWhenResultsOpen = true;

        [Tooltip("PC: sol alttaki klavye / tuş HUD (ControlsHUDController). Boşsa sahnede aranır.")]
        [SerializeField] private ControlsHUDController _pcControlsHud;

        private void OnEnable()
        {
            if (_recorder == null)
            {
                Debug.LogWarning($"[{nameof(TrainingResultScreenSessionBinder)}] Recorder not assigned on {gameObject.name}.", this);
                return;
            }

            _recorder.OnSessionStarted += OnTrainingSessionStarted;
            _recorder.OnSessionEnded += OnSessionEnded;

            if (_recorder.IsSessionActive)
                OnTrainingSessionStarted();
        }

        private void OnDisable()
        {
            if (_recorder != null)
            {
                _recorder.OnSessionStarted -= OnTrainingSessionStarted;
                _recorder.OnSessionEnded -= OnSessionEnded;
            }
        }

        void OnTrainingSessionStarted()
        {
            HideResults();
            SetPcControlsHudVisible(true);
        }

        /// <summary>
        /// Hides the UI. Can be triggered directly by an Obvious Soap EventListener.
        /// </summary>
        public void HideResults()
        {
            if (_resultsScreenRoot != null)
                _resultsScreenRoot.SetActive(false);
            if (_vrResultsScreenRoot != null)
                _vrResultsScreenRoot.SetActive(false);
        }

        /// <summary>
        /// Shows the UI with empty data. Can be triggered directly by an Obvious Soap EventListener.
        /// </summary>
        public void ShowResultsEmpty()
        {
            if (_resultsScreenRoot != null)
                _resultsScreenRoot.SetActive(true);
            if (_vrResultsScreenRoot != null)
                _vrResultsScreenRoot.SetActive(true);

            if (_showCursorWhenResultsOpen)
                ShowCursorForUi();
        }

        private void OnSessionEnded(SessionReport report)
        {
            SetPcControlsHudVisible(false);
            StartCoroutine(SessionEndPresentationFlow(report));
        }

        void SetPcControlsHudVisible(bool visible)
        {
            if (!FirePlatformRuntime.IsPC)
                return;

            ControlsHUDController hud = _pcControlsHud;
            if (hud == null)
                hud = FindAnyObjectByType<ControlsHUDController>(FindObjectsInactive.Include);

            if (hud != null)
                hud.SetHudVisible(visible);
        }

        IEnumerator SessionEndPresentationFlow(SessionReport report)
        {
            TryRecordLeaderboardFromReport(report);

            ShowResultsEmpty();

            TrainingResultScreenController primary = ResolvePrimaryController();
            TrainingResultScreenController vrExtra = ResolveVrController();
            if (vrExtra != null && vrExtra == primary)
                vrExtra = null;

            if (primary == null && vrExtra == null)
            {
                Debug.LogWarning(
                    $"[{nameof(TrainingResultScreenSessionBinder)}] No {nameof(TrainingResultScreenController)}: assign _controller / _resultsScreenRoot and/or _vrController / _vrResultsScreenRoot.",
                    this);
                yield break;
            }

            if (_presentOnNextFrame)
                yield return null;

            if (_showCursorWhenResultsOpen)
                ShowCursorForUi();

            if (report == null)
            {
                Debug.LogWarning(
                    $"[{nameof(TrainingResultScreenSessionBinder)}] Session ended with a null report — results UI is visible but not filled. Check {nameof(ExtinguisherSessionRecorder)} end path (active session / EndSession).",
                    this);
                yield break;
            }

            if (primary != null)
                yield return WaitUntilDocumentRoot(primary);
            if (vrExtra != null)
                yield return WaitUntilDocumentRoot(vrExtra);

            if (primary != null)
                PresentOnController(primary, report);
            PresentOnController(vrExtra, report);
        }

        static IEnumerator WaitUntilDocumentRoot(TrainingResultScreenController controller)
        {
            if (controller == null)
                yield break;

            const int maxFrames = 120;
            for (int i = 0; i < maxFrames; i++)
            {
                UIDocument doc = controller.GetComponent<UIDocument>();
                if (doc != null && doc.rootVisualElement != null)
                    yield break;

                yield return null;
            }

            Debug.LogWarning(
                $"[{nameof(TrainingResultScreenSessionBinder)}] {nameof(UIDocument)} rootVisualElement did not appear in {maxFrames} frames on '{controller.gameObject.name}'.",
                controller);
        }

        static void PresentOnController(TrainingResultScreenController controller, SessionReport report)
        {
            if (controller == null || report == null)
                return;

            controller.Present(report);
            controller.SetTraineeName(ResolveTraineeName(report));
        }

        /// <summary>
        /// Priority: GameSessionData.UserName (static, set at login) → report TraineeId → empty.
        /// </summary>
        private static string ResolveTraineeName(SessionReport report)
        {
            if (GameSessionData.IsSet && !string.IsNullOrEmpty(GameSessionData.UserName))
                return GameSessionData.UserName;

            if (report?.Client != null && !string.IsNullOrEmpty(report.Client.TraineeId))
                return report.Client.TraineeId;

            return string.Empty;
        }

        private static void ShowCursorForUi()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        TrainingResultScreenController ResolvePrimaryController()
        {
            if (_controller != null)
                return _controller;
            return _resultsScreenRoot != null
                ? _resultsScreenRoot.GetComponent<TrainingResultScreenController>()
                : null;
        }

        TrainingResultScreenController ResolveVrController()
        {
            if (_vrController != null)
                return _vrController;
            return _vrResultsScreenRoot != null
                ? _vrResultsScreenRoot.GetComponent<TrainingResultScreenController>()
                : null;
        }

        static void TryRecordLeaderboardFromReport(SessionReport report)
        {
            if (report?.Client == null)
                return;

            int pct = Mathf.Clamp(Mathf.RoundToInt(report.Client.FinalScore * 100f), 0, 100);

            string displayName = string.Empty;
            string userId = string.Empty;
            if (GameSessionData.IsSet)
            {
                displayName = GameSessionData.UserName ?? string.Empty;
                userId = GameSessionData.UserId ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(report.Client.TraineeId))
                displayName = report.Client.TraineeId.Trim();

            TrainingLeaderboardStore.TryRecordScore(displayName, userId, pct);
        }
    }
}
