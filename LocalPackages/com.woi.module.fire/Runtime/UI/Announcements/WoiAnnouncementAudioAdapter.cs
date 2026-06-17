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
        private int _playGeneration;
        private int _activeVoiceGeneration;

        public bool IsAnnouncementPlaying
        {
            get
            {
                if (_voice != null && _voice.IsPlaying())
                {
                    return true;
                }

                if (_fallbackRoutine != null)
                {
                    return true;
                }

                if (audioSystem != null && _lastSound != null && audioSystem.IsQueueRunnerActive(_lastSound))
                {
                    return true;
                }

                return false;
            }
        }

        public event Action OnAnnouncementAudioFinished;

        private void OnEnable()
        {
            audioSystem = null;
            ResolveAudioSystem();
        }

        private void Start()
        {
            ResolveAudioSystem();
        }

        private void ResolveAudioSystem()
        {
            if (audioSystem != null)
            {
                return;
            }

            if (AudioSystem.TryGetFromServiceLocator(out AudioSystem registered) && registered != null)
            {
                audioSystem = registered;
                return;
            }

            audioSystem = FindFirstObjectByType<AudioSystem>();
        }

        private void EnsureAdapterActive()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        private bool TryStartFallbackRoutine(SoundDefinition sound, int generation)
        {
            EnsureAdapterActive();
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning(
                    "[AnnouncementAudioAdapter] Cannot start fallback coroutine — adapter GameObject is inactive.",
                    this);
                RaiseFinishedOnce(generation);
                return false;
            }

            _fallbackRoutine = StartCoroutine(CoFallbackFinished(sound, generation));
            return true;
        }

        public void PlayAnnouncement(SoundDefinition sound)
        {
            if (AudioSystem.IsShuttingDown)
            {
                audioSystem = null;
            }

            ResolveAudioSystem();
            EnsureAdapterActive();

            int generation = ++_playGeneration;

            Debug.Log($"[AnnouncementAudioAdapter] Play: {sound?.name ?? "(null)"}");

            _completionRaised = false;
            StopCurrentAnnouncementInternal();

            if (sound == null || audioSystem == null)
            {
                RaiseFinishedOnce(generation);
                return;
            }

            _lastSound = sound;

            _voice = audioSystem.Play(sound);

            if (_voice != null)
            {
                _activeVoiceGeneration = generation;
                _voice.OnCompleted += OnVoiceCompleted;
            }
            else
            {
                if (_fallbackRoutine != null)
                    StopCoroutine(_fallbackRoutine);

                if (!TryStartFallbackRoutine(sound, generation))
                {
                    return;
                }
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
            RaiseFinishedOnce(_activeVoiceGeneration);
        }

        private System.Collections.IEnumerator CoFallbackFinished(SoundDefinition sound, int generation)
        {
            _fallbackRoutine = null;

            if (generation != _playGeneration)
            {
                yield break;
            }

            // Queue All: Play() returned null — wait for the real queue runner to finish (not clip-length sum).
            if (sound != null && sound.selectionMode == ClipSelectionMode.QueueAll && audioSystem != null)
            {
                float boot = 0f;
                const float bootTimeout = 5f;
                while (!audioSystem.IsQueueRunnerActive(sound) && boot < bootTimeout)
                {
                    if (generation != _playGeneration)
                    {
                        yield break;
                    }

                    boot += Time.unscaledDeltaTime;
                    yield return null;
                }

                while (audioSystem.IsQueueRunnerActive(sound))
                {
                    if (generation != _playGeneration)
                    {
                        yield break;
                    }

                    yield return null;
                }

                RaiseFinishedOnce(generation);
                yield break;
            }

            float wait = EstimatePlaybackDuration(sound);
            if (wait > 0f)
            {
                float elapsed = 0f;
                while (elapsed < wait)
                {
                    if (generation != _playGeneration)
                    {
                        yield break;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            RaiseFinishedOnce(generation);
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

        private void RaiseFinishedOnce(int generation)
        {
            if (generation != _playGeneration)
            {
                return;
            }

            if (_completionRaised)
            {
                return;
            }

            _completionRaised = true;
            _lastSound = null;
            OnAnnouncementAudioFinished?.Invoke();
        }

        private void OnDisable()
        {
            StopCurrentAnnouncementInternal();
        }
    }
}
