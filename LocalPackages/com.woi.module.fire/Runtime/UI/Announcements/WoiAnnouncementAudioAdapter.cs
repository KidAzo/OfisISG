using System;
using UnityEngine;
using WoiUtils.AudioSystem;

namespace Woi.UI.Announcements
{
    /// <summary>
    /// Optional adapter over Woi Audio — keeps <see cref="PopupService"/> free of audio types.
    /// Uses <see cref="AudioSystem.Play"/> and <see cref="AudioVoice.OnCompleted"/> when available;
    /// for <see cref="ClipSelectionMode.QueueAll"/>, waits until <see cref="AudioSystem.IsQueueRunnerActive"/> is false.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/UI/Woi Announcement Audio Adapter")]
    public sealed class WoiAnnouncementAudioAdapter : MonoBehaviour, IAnnouncementAudioAdapter
    {
        [SerializeField] private AudioSystem audioSystem;

        private AudioVoice _voice;
        private SoundDefinition _lastSound;
        private Coroutine _fallbackRoutine;
        private bool _completionRaised;

        public bool IsAnnouncementPlaying =>
            _voice != null && _voice.IsPlaying();

        public event Action OnAnnouncementAudioFinished;

        private void Start()
        {
            ResolveAudioSystem();
        }

        private void ResolveAudioSystem()
        {
            if (audioSystem != null)
                return;

            if (AudioSystem.TryGetFromServiceLocator(out var sys))
                audioSystem = sys;

            if (audioSystem == null)
                audioSystem = FindFirstObjectByType<AudioSystem>();
        }

        public void PlayAnnouncement(SoundDefinition sound)
        {
            ResolveAudioSystem();

            Debug.Log($"[AnnouncementAudioAdapter] Play: {sound?.name ?? "(null)"}");

            _completionRaised = false;
            StopCurrentAnnouncementInternal();

            if (sound == null || audioSystem == null)
            {
                RaiseFinishedOnce();
                return;
            }

            _lastSound = sound;

            _voice = audioSystem.Play(sound);

            if (_voice != null)
            {
                _voice.OnCompleted += OnVoiceCompleted;
            }
            else
            {
                if (_fallbackRoutine != null)
                    StopCoroutine(_fallbackRoutine);

                _fallbackRoutine = StartCoroutine(CoFallbackFinished(sound));
            }
        }

        public void StopCurrentAnnouncement()
        {
            StopCurrentAnnouncementInternal();
        }

        private void StopCurrentAnnouncementInternal()
        {
            if (_fallbackRoutine != null)
            {
                StopCoroutine(_fallbackRoutine);
                _fallbackRoutine = null;
            }

            if (_voice != null)
            {
                _voice.OnCompleted -= OnVoiceCompleted;
                _voice.Stop();
                _voice = null;
            }
            else if (_lastSound != null && audioSystem != null)
            {
                audioSystem.StopAllInstances(_lastSound);
                audioSystem.ClearQueue(_lastSound);
            }

            _lastSound = null;
        }

        private void OnVoiceCompleted(int _)
        {
            if (_voice != null)
                _voice.OnCompleted -= OnVoiceCompleted;

            _voice = null;
            _lastSound = null;
            RaiseFinishedOnce();
        }

        private System.Collections.IEnumerator CoFallbackFinished(SoundDefinition sound)
        {
            _fallbackRoutine = null;

            // Queue All: Play() returned null — wait for the real queue runner to finish (not clip-length sum).
            if (sound != null && sound.selectionMode == ClipSelectionMode.QueueAll && audioSystem != null)
            {
                float boot = 0f;
                const float bootTimeout = 5f;
                while (!audioSystem.IsQueueRunnerActive(sound) && boot < bootTimeout)
                {
                    boot += Time.unscaledDeltaTime;
                    yield return null;
                }

                while (audioSystem.IsQueueRunnerActive(sound))
                    yield return null;

                RaiseFinishedOnce();
                yield break;
            }

            float wait = EstimatePlaybackDuration(sound);
            if (wait > 0f)
                yield return new WaitForSecondsRealtime(wait);

            RaiseFinishedOnce();
        }

        /// <summary>
        /// Best-effort clip-length sum for scheduling (matches fallback completion timing).
        /// </summary>
        public static float EstimatePlaybackDuration(SoundDefinition sound)
        {
            if (sound == null || sound.clips == null || sound.clips.Count == 0)
                return 0f;

            float sum = 0f;
            for (int i = 0; i < sound.clips.Count; i++)
            {
                ClipEntry e = sound.clips[i];
                if (e?.clip != null)
                    sum += e.clip.length;
            }

            return sum;
        }

        private void RaiseFinishedOnce()
        {
            if (_completionRaised)
                return;

            _completionRaised = true;
            OnAnnouncementAudioFinished?.Invoke();
        }

        private void OnDisable()
        {
            StopCurrentAnnouncementInternal();
        }
    }
}
