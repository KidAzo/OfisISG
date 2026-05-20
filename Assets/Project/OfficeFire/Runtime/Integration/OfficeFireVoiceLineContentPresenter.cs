using UnityEngine;
using WOI.Modules.SDK;
using Woi.UI.Announcements;
using Woi.UI.Popups;
using WoiUtils.AudioSystem;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Plays popup text + Woi Audio voice from <see cref="OfficeFireVoiceLineContentDatabase"/> for Archive / Server scenarios.
    /// Wire <see cref="OfficeFireScenarioController.OnAnnouncementRequested"/> to <see cref="PlayVoiceLine"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Voice Line Content Presenter")]
    public sealed class OfficeFireVoiceLineContentPresenter : MonoBehaviour
    {
        [SerializeField]
        private OfficeFireVoiceLineContentDatabase database;

        [SerializeField]
        private OfficeFireLanguageResolver languageResolver;

        [Header("Woi Audio")]
        [Tooltip("Optional. If empty, resolved from scene or added on this GameObject.")]
        [SerializeField]
        private WoiAnnouncementAudioAdapter announcementAudioAdapter;

        [Header("Popup")]
        [Tooltip("Optional. If empty, resolved from ServiceLocator / scene PopupService.")]
        [SerializeField]
        private PopupService popupService;

        [SerializeField]
        private PopupType popupType = PopupType.Training;

        [SerializeField]
        [Min(0.5f)]
        private float defaultPopupDurationSeconds = 5f;

        [SerializeField]
        private bool replaceCurrentPopup = true;

        public void PlayVoiceLine(OfficeFireVoiceLineId voiceLineId)
        {
            if (voiceLineId == OfficeFireVoiceLineId.None)
            {
                return;
            }

            if (database == null)
            {
                Debug.LogWarning("[OfficeFireVoiceLineContentPresenter] database is not assigned.", this);
                return;
            }

            database.TryGetLocalizedSound(voiceLineId, out LocalizedSoundDefinition localizedSound);
            float popupDuration = OfficeFireAnnouncementAudioPlayback.EstimateDuration(localizedSound);
            if (popupDuration <= 0f)
            {
                popupDuration = defaultPopupDurationSeconds;
            }

            if (TryResolvePopupTexts(voiceLineId, out string titleTr, out string bodyTr, out string titleEn, out string bodyEn))
            {
                ShowAnnouncementPopup(titleTr, bodyTr, titleEn, bodyEn, popupDuration);
            }

            if (localizedSound == null)
            {
                return;
            }

            OfficeFireAnnouncementAudioPlayback.Play(announcementAudioAdapter, localizedSound);
            Debug.Log($"[OfficeFire Voice] {voiceLineId}", this);
        }

        private void ShowAnnouncementPopup(
            string titleTr,
            string bodyTr,
            string titleEn,
            string bodyEn,
            float durationSeconds)
        {
            ResolvePopupService();
            if (popupService == null)
            {
                Debug.LogWarning("[OfficeFireVoiceLineContentPresenter] PopupService not found — popup skipped.", this);
                return;
            }

            float duration = Mathf.Max(0.5f, durationSeconds);

            if (replaceCurrentPopup && popupService.IsVisible)
            {
                popupService.Hide();
            }

            popupService.ShowLocalizedText(
                titleTr ?? string.Empty,
                bodyTr ?? string.Empty,
                titleEn ?? string.Empty,
                bodyEn ?? string.Empty,
                popupType,
                duration);
        }

        private void ResolvePopupService()
        {
            if (popupService != null)
            {
                return;
            }

            if (ServiceLocator.TryGet<PopupService>(out PopupService concrete) && concrete != null)
            {
                popupService = concrete;
                return;
            }

            if (ServiceLocator.TryGet<IPopupService>(out IPopupService service) && service is PopupService resolved)
            {
                popupService = resolved;
                return;
            }

            popupService = FindFirstObjectByType<PopupService>();
        }

        private bool TryResolvePopupTexts(
            OfficeFireVoiceLineId id,
            out string titleTr,
            out string bodyTr,
            out string titleEn,
            out string bodyEn)
        {
            titleTr = string.Empty;
            bodyTr = string.Empty;
            titleEn = string.Empty;
            bodyEn = string.Empty;

            if (database == null)
            {
                return false;
            }

            bool hasTurkish = database.TryGetPopupTurkish(id, out titleTr, out bodyTr);
            bool hasEnglish = database.TryGetPopupEnglish(id, out titleEn, out bodyEn);
            return hasTurkish || hasEnglish;
        }
    }
}
