using System;
using System.Collections;
using UnityEngine;
using WoiUtils.AudioSystem;
using WOI.Modules.SDK;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Central audio feedback for the waste flow. Plays a per-waste sound on selection and a
    /// correct/wrong sound after bin classification. Resolved via <see cref="ServiceLocator"/>.
    /// Assign a RandomWeighted SoundDefinition to <see cref="correctSound"/> so one of several
    /// "correct" variants is picked automatically.
    /// </summary>
    [DisallowMultipleComponent]
    public class WasteAudioFeedback : MonoBehaviour
    {
        [Header("Classification Result")]
        [Tooltip("Played when the chosen bin is correct. Use a RandomWeighted SoundDefinition to vary clips.")]
        [SerializeField] private LocalizedWasteSound correctSound;
        [Tooltip("Played when the chosen bin is wrong.")]
        [SerializeField] private LocalizedWasteSound wrongSound;

        private AudioSystem audioSystem;
        private AudioVoice activeFlowVoice;
        private Coroutine activeFlowCoroutine;
        private int flowGeneration;

        /// <summary>
        /// Stops the current waste voice chain (doğru/yanlış + explanation). Call when the player collects
        /// another item while feedback is still playing — not between chained classification → explanation.
        /// </summary>
        public void StopActiveFeedback()
        {
            flowGeneration++;

            if (activeFlowCoroutine != null)
            {
                StopCoroutine(activeFlowCoroutine);
                activeFlowCoroutine = null;
            }

            if (activeFlowVoice != null)
            {
                activeFlowVoice.Stop();
                activeFlowVoice = null;
            }

            EnsureAudioSystem();
            if (audioSystem == null)
                return;

            StopLocalizedInstances(correctSound);
            StopLocalizedInstances(wrongSound);
        }

        private void Awake()
        {
            if (!ServiceLocator.IsRegistered<WasteAudioFeedback>())
                ServiceLocator.Register(this);
        }

        private void Start()
        {
            EnsureAudioSystem();
            if (audioSystem == null)
                Debug.LogWarning("[WasteAudioFeedback] AudioSystem not found on ServiceLocator or in loaded scenes.", this);
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet(out WasteAudioFeedback registered) && ReferenceEquals(registered, this))
                ServiceLocator.Unregister<WasteAudioFeedback>();
        }

        /// <summary>Plays the waste-specific selection sound at the given world position.</summary>
        public void PlayWasteSelected(WasteDefinition definition, Vector3 position)
        {
            if (definition == null)
            {
                Debug.LogWarning("[WasteAudioFeedback] PlayWasteSelected: WasteDefinition is NULL.", this);
                return;
            }

            StopActiveFeedback();

            Debug.Log($"[WasteAudioFeedback] PlayWasteSelected for '{definition.Name}', SelectSound={(definition.SelectSound != null ? definition.SelectSound.name : "NULL")}.", this);
            PlayLocalized(definition.SelectSound, position, true);
        }

        /// <summary>Plays the correct or wrong feedback sound after a bin is chosen.</summary>
        public void PlayClassificationResult(bool isCorrect)
        {
            PlayLocalized(isCorrect ? correctSound : wrongSound, Vector3.zero, false);
        }

        /// <summary>Plays the correct/wrong feedback sound and invokes <paramref name="onComplete"/> when it finishes.</summary>
        public void PlayClassificationResult(bool isCorrect, Action onComplete)
        {
            PlayLocalizedThen(isCorrect ? correctSound : wrongSound, onComplete);
        }

        /// <summary>Plays the waste-specific explanation voice and invokes <paramref name="onComplete"/> when it finishes.</summary>
        public void PlayWasteExplanation(WasteDefinition definition, Action onComplete)
        {
            if (definition == null)
            {
                onComplete?.Invoke();
                return;
            }

            PlayLocalizedThen(definition.ExplanationSound, onComplete);
        }

        private void PlayLocalizedThen(LocalizedWasteSound localized, Action onComplete)
        {
            if (activeFlowCoroutine != null)
            {
                StopCoroutine(activeFlowCoroutine);
                activeFlowCoroutine = null;
            }

            if (activeFlowVoice != null)
            {
                activeFlowVoice.Stop();
                activeFlowVoice = null;
            }

            SoundDefinition sound = localized != null ? localized.Resolve() : null;
            EnsureAudioSystem();

            if (sound == null || audioSystem == null)
            {
                Debug.LogWarning($"[WasteAudioFeedback] PlayLocalizedThen skipped: localized={(localized != null ? localized.name : "NULL")}, sound={(sound != null ? sound.name : "NULL")}, audioSystem={(audioSystem != null ? "ok" : "NULL")}.", this);
                onComplete?.Invoke();
                return;
            }

            int generation = flowGeneration;
            Debug.Log($"[WasteAudioFeedback] PlayLocalizedThen playing '{sound.name}'.", this);
            AudioVoice voice = audioSystem.Play(sound);
            if (voice != null)
            {
                activeFlowVoice = voice;
                int voiceGeneration = voice.Generation;
                Action<int> handler = null;
                handler = completedGeneration =>
                {
                    if (completedGeneration != voiceGeneration || generation != flowGeneration)
                        return;

                    voice.OnCompleted -= handler;
                    if (ReferenceEquals(activeFlowVoice, voice))
                        activeFlowVoice = null;
                    onComplete?.Invoke();
                };
                voice.OnCompleted += handler;
                return;
            }

            // Play() returned no voice (queue/delay/cooldown) — fall back to a clip-length wait.
            if (isActiveAndEnabled)
                activeFlowCoroutine = StartCoroutine(WaitThenInvoke(EstimateDuration(sound), generation, onComplete));
            else
                onComplete?.Invoke();
        }

        private static float EstimateDuration(SoundDefinition sound)
        {
            if (sound == null || sound.clips == null || sound.clips.Count == 0)
                return 0f;

            float longest = 0f;
            for (int i = 0; i < sound.clips.Count; i++)
            {
                ClipEntry entry = sound.clips[i];
                if (entry != null && entry.clip != null)
                    longest = Mathf.Max(longest, Mathf.Max(0f, entry.delay) + entry.clip.length);
            }

            return longest;
        }

        private IEnumerator WaitThenInvoke(float seconds, int generation, Action onComplete)
        {
            if (seconds > 0f)
                yield return new WaitForSecondsRealtime(seconds);

            activeFlowCoroutine = null;
            if (generation != flowGeneration)
                yield break;

            onComplete?.Invoke();
        }

        private void StopLocalizedInstances(LocalizedWasteSound localized)
        {
            if (localized == null)
                return;

            EnsureAudioSystem();
            if (audioSystem == null)
                return;

            localized.StopAllInstances(audioSystem);
        }

        private void PlayLocalized(LocalizedWasteSound localized, Vector3 position, bool positional)
        {
            if (localized == null)
            {
                Debug.LogWarning("[WasteAudioFeedback] PlayLocalized: LocalizedWasteSound is NULL (not assigned).", this);
                return;
            }

            EnsureAudioSystem();
            if (audioSystem == null)
            {
                Debug.LogWarning("[WasteAudioFeedback] PlayLocalized: AudioSystem is NULL.", this);
                return;
            }

            SoundDefinition sound = localized.Resolve();
            if (sound == null)
            {
                Debug.LogWarning($"[WasteAudioFeedback] PlayLocalized: Resolve() returned NULL on '{localized.name}'.", this);
                return;
            }

            Debug.Log($"[WasteAudioFeedback] Playing '{sound.name}' (positional={positional}).", this);

            if (positional)
                audioSystem.Play(sound, PlayContext.At(position));
            else
                audioSystem.Play(sound);
        }

        private void EnsureAudioSystem()
        {
            if (audioSystem != null)
                return;

            if (AudioSystem.TryGetFromServiceLocator(out audioSystem) && audioSystem != null)
                return;

            audioSystem = FindFirstObjectByType<AudioSystem>();
        }
    }
}
