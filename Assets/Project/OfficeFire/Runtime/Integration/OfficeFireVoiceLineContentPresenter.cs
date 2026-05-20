using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Plays popup text + voice from <see cref="OfficeFireVoiceLineContentDatabase"/> for Archive / Server scenarios.
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

        [SerializeField]
        private AudioSource voiceAudioSource;

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

            if (TryResolvePopupText(voiceLineId, out string title, out string body))
            {
                Debug.Log($"[OfficeFire Popup] {title}\n{body}", this);
            }

            if (voiceAudioSource == null)
            {
                Debug.LogWarning("[OfficeFireVoiceLineContentPresenter] voiceAudioSource is not assigned.", this);
                return;
            }

            if (!TryResolveVoiceClip(voiceLineId, out AudioClip clip) || clip == null)
            {
                return;
            }

            if (voiceAudioSource.isPlaying)
            {
                voiceAudioSource.Stop();
            }

            voiceAudioSource.clip = clip;
            voiceAudioSource.Play();
            Debug.Log($"[OfficeFire Voice] {voiceLineId}", this);
        }

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

        private bool TryResolvePopupText(OfficeFireVoiceLineId id, out string title, out string body)
        {
            title = string.Empty;
            body = string.Empty;
            if (database == null)
            {
                return false;
            }

            if (ResolveLanguage().IsEnglish())
            {
                return database.TryGetPopupEnglish(id, out title, out body);
            }

            return database.TryGetPopupTurkish(id, out title, out body);
        }

        private bool TryResolveVoiceClip(OfficeFireVoiceLineId id, out AudioClip clip)
        {
            clip = null;
            if (database == null)
            {
                return false;
            }

            if (ResolveLanguage().IsEnglish())
            {
                return database.TryGetVoiceClipEnglish(id, out clip);
            }

            return database.TryGetVoiceClipTurkish(id, out clip);
        }
    }
}
