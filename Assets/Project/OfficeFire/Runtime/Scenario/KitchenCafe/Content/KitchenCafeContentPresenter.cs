using UnityEngine;
using Woi.UI.Announcements;
using WoiUtils.AudioSystem;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Resolves kitchen/cafe scenario content from <see cref="KitchenCafeScenarioContentDatabase"/>
    /// using Woi Audio + popup placeholders.
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

        [Header("Woi Audio")]
        [Tooltip("Optional. If empty, resolved from scene or added on this GameObject.")]
        [SerializeField]
        private WoiAnnouncementAudioAdapter announcementAudioAdapter;

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

            if (cue.StopPreviousVoice)
            {
                OfficeFireAnnouncementAudioPlayback.Stop(announcementAudioAdapter);
            }

            string title = string.Empty;
            string body = string.Empty;
            bool hasPopupText = false;
            if (cue.PopupId != KitchenCafePopupId.None)
            {
                hasPopupText = TryResolvePopupText(cue.PopupId, out title, out body);
            }

            database.TryGetLocalizedSound(cue.VoiceId, out LocalizedSoundDefinition localizedSound);
            float duration;
            switch (cue.PopupDurationMode)
            {
                case PopupDurationMode.UseVoiceClipLength:
                    duration = OfficeFireAnnouncementAudioPlayback.EstimateDuration(localizedSound);
                    if (duration <= 0f)
                    {
                        duration = Mathf.Max(0f, cue.CustomPopupDuration);
                    }

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

            if (localizedSound != null)
            {
                OfficeFireAnnouncementAudioPlayback.Play(announcementAudioAdapter, localizedSound);
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

            if (!database.TryGetLocalizedSound(voiceId, out LocalizedSoundDefinition localizedSound))
            {
                return;
            }

            OfficeFireAnnouncementAudioPlayback.Stop(announcementAudioAdapter);
            OfficeFireAnnouncementAudioPlayback.Play(announcementAudioAdapter, localizedSound);
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
