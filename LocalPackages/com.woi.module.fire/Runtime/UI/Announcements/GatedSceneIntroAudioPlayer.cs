using System.Collections;
using UnityEngine;
using WoiUtils.AudioSystem;

namespace Woi.UI.Announcements
{
    /// <summary>
    /// Plays localized or single <see cref="SoundDefinition"/> like <see cref="AudioTrigger"/> (manual <c>Play()</c>),
    /// and holds <see cref="ExclusiveAnnouncementPlaybackGate"/> until that sound fully completes (including Queue All).
    /// Use from UnityEvents (e.g. <see cref="Woi.Training.LevelController"/> gameplay-started) for scene entry VO.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/UI/Gated Scene Intro Audio Player")]
    public sealed class GatedSceneIntroAudioPlayer : MonoBehaviour
    {
        [Header("Target (same as Audio Trigger)")]
        [SerializeField]
        private LocalizedSoundDefinition localizedSound;

        [SerializeField]
        private SoundDefinition sound;

        [Header("Multipliers")]
        [SerializeField]
        [Range(0f, 1.5f)]
        private float volumeMul = 1f;

        [SerializeField]
        private float pitchMul = 1f;

        private AudioSystem _audioSystem;
        private AudioVoice _voice;
        private Coroutine _completionRoutine;
        private bool _gateHeld;

        /// <summary>Manual fire from UnityEvent — bypasses sound cooldown like <see cref="AudioTrigger.Play"/>.</summary>
        public void Play() => TryPlay();

        private void OnDisable()
        {
            CancelPlayback();
        }

        private void ResolveAudioSystem()
        {
            if (_audioSystem != null)
                return;

            if (AudioSystem.TryGetFromServiceLocator(out _audioSystem) && _audioSystem != null)
                return;

            _audioSystem = FindFirstObjectByType<AudioSystem>();
        }

        private void TryPlay()
        {
            if (AudioSystem.IsShuttingDown)
                return;

            SoundDefinition playSound = localizedSound != null ? localizedSound.ResolveForCurrentLanguage() : sound;
            if (playSound == null)
            {
                Debug.LogWarning(
                    "[GatedSceneIntroAudioPlayer] No sound — assign Localized Sound or Sound Definition.",
                    this);
                return;
            }

            ResolveAudioSystem();
            if (_audioSystem == null)
            {
                Debug.LogWarning("[GatedSceneIntroAudioPlayer] No AudioSystem — intro skipped.", this);
                return;
            }

            CancelPlayback();

            ExclusiveAnnouncementPlaybackGate.Enter();
            _gateHeld = true;

            var ctx = PlayContext.DebugNoCooldown();
            ctx.volumeMul = volumeMul > 0f ? volumeMul : 1f;
            ctx.pitchMul = pitchMul > 0f ? pitchMul : 1f;

            _voice = _audioSystem.Play(playSound, ctx);

            if (_voice != null)
            {
                _voice.OnCompleted += OnVoiceCompleted;
            }
            else
            {
                _completionRoutine = StartCoroutine(CoWaitCompletion(playSound));
            }
        }

        private void OnVoiceCompleted(int _)
        {
            if (_voice != null)
            {
                _voice.OnCompleted -= OnVoiceCompleted;
                _voice = null;
            }

            ReleaseGate();
        }

        private IEnumerator CoWaitCompletion(SoundDefinition playSound)
        {
            _completionRoutine = null;

            if (playSound != null
                && playSound.selectionMode == ClipSelectionMode.QueueAll
                && _audioSystem != null)
            {
                float boot = 0f;
                const float bootTimeout = 5f;
                while (!_audioSystem.IsQueueRunnerActive(playSound) && boot < bootTimeout)
                {
                    boot += Time.unscaledDeltaTime;
                    yield return null;
                }

                while (_audioSystem.IsQueueRunnerActive(playSound))
                    yield return null;

                ReleaseGate();
                yield break;
            }

            float wait = WoiAnnouncementAudioAdapter.EstimatePlaybackDuration(playSound);
            if (wait > 0f)
                yield return new WaitForSecondsRealtime(wait);

            ReleaseGate();
        }

        private void CancelPlayback()
        {
            if (_completionRoutine != null)
            {
                StopCoroutine(_completionRoutine);
                _completionRoutine = null;
            }

            if (_voice != null)
            {
                _voice.OnCompleted -= OnVoiceCompleted;
                _voice.Stop();
                _voice = null;
            }

            ReleaseGate();
        }

        private void ReleaseGate()
        {
            if (!_gateHeld)
                return;

            _gateHeld = false;
            ExclusiveAnnouncementPlaybackGate.Exit();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (volumeMul < 0f)
                volumeMul = 0f;
            if (pitchMul <= 0f)
                pitchMul = 1f;
        }
#endif
    }
}
