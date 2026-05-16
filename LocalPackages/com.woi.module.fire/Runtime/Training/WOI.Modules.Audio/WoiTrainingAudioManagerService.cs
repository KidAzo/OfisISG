using System;
using System.Collections;
using UnityEngine;
using WOI.Modules.SDK;
using WoiUtils.AudioSystem;

namespace WOI.Modules.Audio
{
    /// <summary>
    /// Default <see cref="IAudioManagerService"/> backed by <see cref="AudioSystem"/> on the service locator.
    /// </summary>
    public sealed class WoiTrainingAudioManagerService : IAudioManagerService
    {
        public static readonly WoiTrainingAudioManagerService Instance = new();

        private WoiTrainingAudioManagerService()
        {
        }

        public float GetEstimatedDurationSeconds(SoundDefinition sound)
        {
            if (sound == null || sound.clips == null || sound.clips.Count == 0)
                return 0f;

            float sum = 0f;
            for (int i = 0; i < sound.clips.Count; i++)
            {
                ClipEntry e = sound.clips[i];
                if (e?.clip != null)
                    sum += Mathf.Max(0f, e.delay) + e.clip.length;
            }

            return sum;
        }

        public void PlayWhenFinished(MonoBehaviour coroutineHost, SoundDefinition sound, Action onComplete)
        {
            if (coroutineHost == null)
            {
                Debug.LogWarning("[WoiTrainingAudioManagerService] coroutineHost is null — cannot play.", coroutineHost);
                onComplete?.Invoke();
                return;
            }

            if (sound == null)
            {
                onComplete?.Invoke();
                return;
            }

            coroutineHost.StartCoroutine(CoPlay(coroutineHost, sound, onComplete));
        }

        private static IEnumerator CoPlay(MonoBehaviour host, SoundDefinition sound, Action onComplete)
        {
            if (!AudioSystem.TryGetFromServiceLocator(out AudioSystem audioSystem) || audioSystem == null)
                audioSystem = UnityEngine.Object.FindFirstObjectByType<AudioSystem>();

            if (audioSystem == null)
            {
                Debug.LogWarning("[WoiTrainingAudioManagerService] No AudioSystem — playback skipped.", host);
                onComplete?.Invoke();
                yield break;
            }

            var ctx = PlayContext.DebugNoCooldown();
            if (sound.loop)
            {
                Debug.LogWarning(
                    "[WoiTrainingAudioManagerService] Looping SoundDefinition — completion uses estimated duration only.",
                    host);
                audioSystem.Play(sound, ctx);
                float loopWaitSeconds = Instance.GetEstimatedDurationSeconds(sound);
                if (loopWaitSeconds > 0f)
                    yield return new WaitForSecondsRealtime(loopWaitSeconds);
                onComplete?.Invoke();
                yield break;
            }

            AudioVoice voice = audioSystem.Play(sound, ctx);

            if (voice != null)
            {
                bool done = false;
                void Handler(int _)
                {
                    done = true;
                    if (voice != null)
                        voice.OnCompleted -= Handler;
                }

                voice.OnCompleted += Handler;
                while (!done)
                    yield return null;

                if (voice != null)
                    voice.OnCompleted -= Handler;

                onComplete?.Invoke();
                yield break;
            }

            if (sound.selectionMode == ClipSelectionMode.QueueAll)
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

                onComplete?.Invoke();
                yield break;
            }

            float fallbackWaitSeconds = Instance.GetEstimatedDurationSeconds(sound);
            if (fallbackWaitSeconds > 0f)
                yield return new WaitForSecondsRealtime(fallbackWaitSeconds);

            onComplete?.Invoke();
        }
    }

    internal static class WoiTrainingAudioManagerServiceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            if (ServiceLocator.IsRegistered<IAudioManagerService>())
                return;

            ServiceLocator.Register<IAudioManagerService>(WoiTrainingAudioManagerService.Instance);
        }
    }
}
