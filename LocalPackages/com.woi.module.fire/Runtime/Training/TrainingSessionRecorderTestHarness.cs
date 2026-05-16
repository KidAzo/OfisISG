using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Woi.Game.Training
{
    /// <summary>
    /// Play-mode helper to exercise <see cref="ExtinguisherSessionRecorder"/> without wiring game flow.
    /// The recorder can sit on the player with <see cref="Woi.Equipment.PlayerExtinguisherEquipment"/> assigned so it follows swaps.
    /// </summary>
    [AddComponentMenu("Woi/Training/Session Recorder Test Harness")]
    public sealed class TrainingSessionRecorderTestHarness : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private ExtinguisherSessionRecorder _recorder;

        [Header("Session ids")]
        [SerializeField] private string _scenarioIdForTest = "debug_scenario";

        [Header("Hotkeys & Events")]
        [SerializeField] private bool _hotkeysEnabled = true;
        [SerializeField] private KeyCode _beginSessionKey = KeyCode.F5;
        [SerializeField] private KeyCode _endSessionKey   = KeyCode.F6;
        [SerializeField] private KeyCode _partialReportKey = KeyCode.F7;

        [Header("Logging")]
        [Tooltip("If true, logs company debrief (SessionReport.ToString()) when a session ends. " +
                 "Disable _logReportOnEnd on ExtinguisherSessionRecorder to avoid duplicate dumps.")]
        [SerializeField] private bool _logFullReportOnEnd = true;
        [Tooltip("Logs GetPartialReport() to the console when you press the partial-report key.")]
        [SerializeField] private bool _logPartialReportToConsole = true;

        private void OnEnable()
        {
             _recorder.OnSessionEnded += HandleSessionEnded;
             _recorder.OnSessionStarted += HandleSessionStarted;
        }

        private void OnDisable()
        {
             _recorder.OnSessionEnded -= HandleSessionEnded;
             _recorder.OnSessionStarted -= HandleSessionStarted;
        }

        private void Update()
        {
            if (!_hotkeysEnabled || _recorder == null) return;

            if (WasKeyPressedThisFrame(_beginSessionKey))
                TestBeginSession();

            if (WasKeyPressedThisFrame(_endSessionKey))
                TestEndSession();

            if (WasKeyPressedThisFrame(_partialReportKey))
                TestPartialReport();
        }

        private bool WasKeyPressedThisFrame(KeyCode code)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            return code switch
            {
                KeyCode.F1  => kb.f1Key.wasPressedThisFrame,
                KeyCode.F2  => kb.f2Key.wasPressedThisFrame,
                KeyCode.F3  => kb.f3Key.wasPressedThisFrame,
                KeyCode.F4  => kb.f4Key.wasPressedThisFrame,
                KeyCode.F5  => kb.f5Key.wasPressedThisFrame,
                KeyCode.F6  => kb.f6Key.wasPressedThisFrame,
                KeyCode.F7  => kb.f7Key.wasPressedThisFrame,
                KeyCode.F8  => kb.f8Key.wasPressedThisFrame,
                KeyCode.F9  => kb.f9Key.wasPressedThisFrame,
                KeyCode.F10 => kb.f10Key.wasPressedThisFrame,
                KeyCode.F11 => kb.f11Key.wasPressedThisFrame,
                KeyCode.F12 => kb.f12Key.wasPressedThisFrame,
                _           => false,
            };
#else
            return Input.GetKeyDown(code);
#endif
        }

        private void HandleSessionStarted()
        {
            // Removed for Main Recorder
        }

        private void HandleSessionEnded(SessionReport report)
        {
            if (report == null) return;

            if (_logFullReportOnEnd)
                Debug.Log($"[SessionRecordTest] OnSessionEnded\n{report}", this);
            else
                Debug.Log(
                    $"[SessionRecordTest] OnSessionEnded | id={report.Client.SessionId} | " +
                    $"duration={report.Client.SessionDurationSeconds:F1}s | final={report.Client.FinalScore:F3} | " +
                    $"hits={report.Technical.HitTicks}/{report.Technical.TotalEvalTicks} | " +
                    $"sweep perf={report.Sweep.SweepPerformed} rule={report.Sweep.SweepRulePassed} " +
                    $"span={report.Sweep.SweepCoverageWidth:F2}m streak={report.Sweep.SweepDurationSeconds:F2}s",
                    this);
        }

        /// <summary>Wire to a UI Button — starts recording (same as F5).</summary>
        [ContextMenu("Test / Begin Session")]
        public void TestBeginSession()
        {
            if (_recorder == null)
            {
                Debug.LogWarning("[SessionRecordTest] No ExtinguisherSessionRecorder assigned.", this);
                return;
            }

            _recorder.BeginSession(_scenarioIdForTest);

            Debug.Log(
                $"[SessionRecordTest] BeginSession(\"{_scenarioIdForTest}\") | active={_recorder.IsSessionActive}",
                this);
        }

        /// <summary>Wire to a UI Button — ends recording and raises OnSessionEnded (same as F6).</summary>
        [ContextMenu("Test / End Session")]
        public void TestEndSession()
        {
            if (_recorder == null)
            {
                Debug.LogWarning("[SessionRecordTest] No ExtinguisherSessionRecorder assigned.", this);
                return;
            }

            SessionReport report = _recorder.EndSession();
            if (report == null)
                Debug.Log("[SessionRecordTest] EndSession returned null (no active session).", this);
        }

        /// <summary>Wire to a UI Button — snapshot without ending (same as F7).</summary>
        [ContextMenu("Test / Partial Report")]
        public void TestPartialReport()
        {
            if (_recorder == null)
            {
                Debug.LogWarning("[SessionRecordTest] No ExtinguisherSessionRecorder assigned.", this);
                return;
            }

            SessionReport partial = _recorder.GetPartialReport();
            if (partial == null)
            {
                Debug.Log("[SessionRecordTest] GetPartialReport → null (no active session).", this);
                return;
            }

            if (_logPartialReportToConsole)
                Debug.Log($"[SessionRecordTest] PARTIAL (session still open)\n{partial}", this);
        }
    }
}
