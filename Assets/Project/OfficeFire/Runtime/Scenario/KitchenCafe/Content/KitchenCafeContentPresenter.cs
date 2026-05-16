using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Resolves kitchen/cafe scenario content from <see cref="KitchenCafeScenarioContentDatabase"/>
    /// using <see cref="OfficeFireLanguageResolver"/> so this assembly does not reference localization SDK types.
    /// </summary>
    public sealed class KitchenCafeContentPresenter : MonoBehaviour
    {
        [Header("Database")]
        [SerializeField]
        private KitchenCafeScenarioContentDatabase database;

        [Header("Language")]
        [Tooltip("Optional; if unset, a resolver is added on this GameObject when needed.")]
        [SerializeField]
        private OfficeFireLanguageResolver languageResolver;

        [Header("Audio Output")]
        [SerializeField]
        private AudioSource voiceAudioSource;

        private OfficeFireLanguageResolver ResolveLanguage()
        {
            if (languageResolver == null)
            {
                languageResolver = GetComponent<OfficeFireLanguageResolver>();
            }

            if (languageResolver == null)
            {
                languageResolver = gameObject.AddComponent<OfficeFireLanguageResolver>();
            }

            return languageResolver;
        }

        public void PlayContentCue(KitchenCafeContentCueId cueId)
        {
            if (cueId == KitchenCafeContentCueId.None)
            {
                return;
            }

            if (database == null)
            {
                Debug.LogWarning("[KitchenCafeContentPresenter] KitchenCafeScenarioContentDatabase is missing.", this);
                return;
            }

            if (!database.TryGetCue(cueId, out KitchenCafeContentCueEntry cue))
            {
                return;
            }

            if (cue.ClosePreviousPopup)
            {
                CloseCurrentPopupPlaceholder();
            }

            if (cue.StopPreviousVoice && voiceAudioSource != null && voiceAudioSource.isPlaying)
            {
                voiceAudioSource.Stop();
            }

            string title = string.Empty;
            string body = string.Empty;
            bool hasPopupText = false;
            if (cue.PopupId != KitchenCafePopupId.None)
            {
                hasPopupText = TryResolvePopupText(cue.PopupId, out title, out body);
            }

            AudioClip clip = null;
            if (cue.VoiceId != KitchenCafeVoiceId.None)
            {
                TryResolveVoiceClip(cue.VoiceId, out clip);
            }

            float duration;
            switch (cue.PopupDurationMode)
            {
                case PopupDurationMode.UseVoiceClipLength:
                    duration = clip != null ? clip.length : Mathf.Max(0f, cue.CustomPopupDuration);
                    break;
                case PopupDurationMode.CustomDuration:
                    duration = Mathf.Max(0f, cue.CustomPopupDuration);
                    break;
                default:
                    duration = -1f;
                    break;
            }

            if (hasPopupText)
            {
                ShowPopupPlaceholder(title, body, duration);
            }

            if (voiceAudioSource != null && clip != null)
            {
                voiceAudioSource.clip = clip;
                voiceAudioSource.Play();
            }

            Debug.Log(
                $"[Kitchen Content Cue] Playing cue: {cueId}, popup: {cue.PopupId}, voice: {cue.VoiceId}, duration: {duration}",
                this);
        }

        public void ShowPopup(KitchenCafePopupId popupId)
        {
            if (popupId == KitchenCafePopupId.None)
            {
                return;
            }

            if (database == null)
            {
                Debug.LogWarning("[KitchenCafeContentPresenter] KitchenCafeScenarioContentDatabase is missing.", this);
                return;
            }

            if (!TryResolvePopupText(popupId, out string title, out string body))
            {
                return;
            }

            Debug.Log($"[Kitchen Popup] {title}\n{body}", this);
            // TODO: Forward title/body to the existing project popup system here.
        }

        public void PlayVoice(KitchenCafeVoiceId voiceId)
        {
            if (voiceId == KitchenCafeVoiceId.None)
            {
                return;
            }

            if (database == null)
            {
                Debug.LogWarning("[KitchenCafeContentPresenter] KitchenCafeScenarioContentDatabase is missing.", this);
                return;
            }

            if (voiceAudioSource == null)
            {
                Debug.LogWarning("[KitchenCafeContentPresenter] voiceAudioSource is missing.", this);
                return;
            }

            if (!TryResolveVoiceClip(voiceId, out AudioClip clip) || clip == null)
            {
                return;
            }

            if (voiceAudioSource.isPlaying)
            {
                voiceAudioSource.Stop();
            }

            voiceAudioSource.clip = clip;
            voiceAudioSource.Play();
            Debug.Log($"[Kitchen Voice] Playing: {voiceId}", this);
        }

        private void CloseCurrentPopupPlaceholder()
        {
            // TODO: Close current popup through existing popup system.
        }

        private void ShowPopupPlaceholder(string title, string body, float duration)
        {
            // TODO: Show popup through existing popup system with title, body, duration.
            Debug.Log($"[Kitchen Popup] {title}\n{body}\nDuration: {duration}", this);
        }

        private bool TryResolvePopupText(KitchenCafePopupId popupId, out string title, out string body)
        {
            title = string.Empty;
            body = string.Empty;
            if (database == null)
            {
                return false;
            }

            if (IsEnglish())
            {
                return database.TryGetPopupEnglish(popupId, out title, out body);
            }

            if (IsTurkish())
            {
                return database.TryGetPopupTurkish(popupId, out title, out body);
            }

            return database.TryGetPopupTurkish(popupId, out title, out body);
        }

        private bool TryResolveVoiceClip(KitchenCafeVoiceId voiceId, out AudioClip clip)
        {
            clip = null;
            if (database == null)
            {
                return false;
            }

            if (IsEnglish())
            {
                return database.TryGetVoiceClipEnglish(voiceId, out clip);
            }

            if (IsTurkish())
            {
                return database.TryGetVoiceClipTurkish(voiceId, out clip);
            }

            return database.TryGetVoiceClipTurkish(voiceId, out clip);
        }

        private bool IsTurkish()
        {
            return ResolveLanguage().IsTurkish();
        }

        private bool IsEnglish()
        {
            return ResolveLanguage().IsEnglish();
        }
    }
}
