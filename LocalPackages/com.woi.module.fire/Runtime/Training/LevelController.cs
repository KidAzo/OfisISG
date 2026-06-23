using Obvious.Soap;
using UnityEngine;
using Woi.Game.Training;
using Woi.Settings;
using WOI.Modules.Audio;
using WOI.Modules.SDK;
using UnityEngine.Events;
using WoiUtils.AudioSystem;


namespace Woi.Training
{
    /// <summary>
    /// Controls the high-level flow of the training level.
    /// Starts the session on scene load and ends it when the player presses the
    /// gameplay-finished input (TAB by default).
    /// </summary>
    public class LevelController : MonoBehaviour
    {
        [Header("Session")]
        [SerializeField] private ExtinguisherSessionRecorder _recorder;
        [SerializeField] private string _initialScenarioId = "scenario_01";

        [Header("Level start voice (optional)")]
        [Tooltip("Localized EN/TR SoundDefinition bundle. Played after _onGameplayStartedEvent; when playback ends, _gameplayStartedLevelVoiceFinished is raised (Soap).")]
        [SerializeField]
        private LocalizedSoundDefinition _gameplayStartedLevelVoice;

        [Tooltip("Soap event raised when _gameplayStartedLevelVoice has fully finished (including Queue All).")]
        [SerializeField]
        private ScriptableEventNoParam _gameplayStartedLevelVoiceFinished;

        [Tooltip("Optional: e.g. Assets/.../LevelNarrationFinishedForHover.asset. Raised when level start voice ends, same moment as above. If it is the same reference as _gameplayStartedLevelVoiceFinished, it is raised only once.")]
        [SerializeField]
        private ScriptableEventNoParam _levelNarrationFinishedForHover;

        [Header("Input Events")]
        [Tooltip("Assign the 'onGameplayFinishedInput' ScriptableEvent SO here. " +
                 "When it is raised (TAB key) EndSession is called automatically.")]
        [SerializeField] private ScriptableEventNoParam _gameplayFinishedEvent;

        [Header("Navigation")]
        [Tooltip("Scene group name to load when returning to the login screen. " +
                 "Must match a GroupName in the SceneLoader's Scene Groups array " +
                 "(the same bootstrap scene that contains GameInitializer).")]
        [SerializeField] private string _loginSceneGroupName = "Login";

        [Tooltip("When true, ReturnToLogin is called automatically after the session ends. " +
                 "Disable when you want a results screen shown first and the player clicks a button.")]
        [SerializeField] private bool _returnToLoginAfterSession = false;

        [SerializeField] private UnityEvent _onGameplayStartedEvent;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_gameplayFinishedEvent != null)
                _gameplayFinishedEvent.OnRaised += HandleGameplayFinished;
        }

        private void OnDisable()
        {
            if (_gameplayFinishedEvent != null)
                _gameplayFinishedEvent.OnRaised -= HandleGameplayFinished;
        }

        private void Start()
        {
            TrainingGameplayInputGate.ResetForSceneEntry();

            if (_recorder != null)
            {
                _recorder.BeginSession(_initialScenarioId);
            }
            else
            {
                Debug.LogWarning("[LevelController] ExtinguisherSessionRecorder is not assigned — cannot start session.", this);
            }

            _onGameplayStartedEvent.Invoke();

            TryPlayLevelStartVoiceAndRaiseSoapWhenDone();
        }

        private void TryPlayLevelStartVoiceAndRaiseSoapWhenDone()
        {
            if (_gameplayStartedLevelVoice == null)
            {
                RaiseLevelStartNarrationFinishedSoap();
                return;
            }

            SoundDefinition voice = _gameplayStartedLevelVoice.ResolveForCurrentLanguage();
            if (voice == null)
            {
                Debug.LogWarning(
                    "[LevelController] Localized level start voice resolved to null — assign EN/TR on the Localized Sound asset.",
                    this);
                RaiseLevelStartNarrationFinishedSoap();
                return;
            }

            if (!ServiceLocator.TryGet<IAudioManagerService>(out IAudioManagerService audio) || audio == null)
            {
                Debug.LogWarning(
                    "[LevelController] IAudioManagerService not registered — level start voice skipped. " +
                    "WoiTrainingAudioManagerService registers at subsystem startup.",
                    this);
                RaiseLevelStartNarrationFinishedSoap();
                return;
            }

            float estimatedSeconds = audio.GetEstimatedDurationSeconds(voice);
            if (estimatedSeconds > 0f)
                Debug.Log($"[LevelController] Level start voice estimated duration: {estimatedSeconds:F2}s", this);

            audio.PlayWhenFinished(this, voice, RaiseLevelStartNarrationFinishedSoap);
        }

        private void RaiseLevelStartNarrationFinishedSoap()
        {
            if (_gameplayStartedLevelVoiceFinished != null)
                _gameplayStartedLevelVoiceFinished.Raise();

            if (_levelNarrationFinishedForHover != null &&
                _levelNarrationFinishedForHover != _gameplayStartedLevelVoiceFinished)
                _levelNarrationFinishedForHover.Raise();
        }

        // ── Input handler ─────────────────────────────────────────────────────

        private void HandleGameplayFinished()
        {
            if (_recorder == null)
            {
                Debug.LogWarning("[LevelController] ExtinguisherSessionRecorder not assigned — cannot end session.", this);
                return;
            }

            if (!_recorder.IsSessionActive)
            {
                Debug.LogWarning("[LevelController] GameplayFinished pressed but no active session.", this);
                return;
            }

            Debug.Log("[LevelController] GameplayFinished received — ending session.", this);
            _recorder.EndSession();

            if (_returnToLoginAfterSession)
                ReturnToLogin();
        }

        /// <summary>
        /// VR çıkış paneli EVET: <see cref="_gameplayFinishedEvent"/> ile aynı — oturum biter, sonuç ekranı binder ile açılır.
        /// (XR <c>FinishedGame</c> girdisi sağ grip ile çıkış paneliyle çakıştığı için orada <c>onGameplayFinishedInput</c> tetiklenmez.)
        /// </summary>
        public void RequestEndSessionFromExitPanel() => HandleGameplayFinished();

        // ── Navigation ────────────────────────────────────────────────────────

        /// <summary>
        /// Loads the login scene group via the project's <see cref="ISceneLoaderService"/>.
        /// Call this from a UI button on the results screen, or enable
        /// <see cref="_returnToLoginAfterSession"/> to trigger it automatically.
        /// </summary>
        public async void ReturnToLogin()
        {
            if (!ServiceLocator.TryGet<ISceneLoaderService>(out ISceneLoaderService loader))
            {
                Debug.LogError(
                    "[LevelController] ISceneLoaderService not found in ServiceLocator. " +
                    "Make sure the bootstrap scene with SceneLoader / GameInitializer is loaded.", this);
                return;
            }

            if (string.IsNullOrEmpty(_loginSceneGroupName))
            {
                Debug.LogError(
                    "[LevelController] Login Scene Group Name is empty. " +
                    "Set it to the GroupName of your login scene group in the Inspector.", this);
                return;
            }

            Debug.Log($"[LevelController] Returning to login scene group: '{_loginSceneGroupName}'", this);
            await loader.LoadScene(_loginSceneGroupName);
        }
    }
}
