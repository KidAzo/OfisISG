using System;
using System.Collections;
using System.Collections.Generic;
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

        private OfficeFireLanguageResolver _languageResolver;

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

        [Tooltip("When queue is off: hide/replace the visible popup before showing the next one.")]
        [SerializeField]
        private bool replaceCurrentPopup = true;

        [Header("Queue")]
        [Tooltip("When enabled, PlayVoiceLine calls are played one after another (audio + popup must finish).")]
        [SerializeField]
        private bool queueAnnouncements = true;

        private readonly Queue<OfficeFireVoiceLineId> _pendingVoiceLines = new Queue<OfficeFireVoiceLineId>();

        private int _playSession;
        private bool _isProcessingQueue;
        private bool _awaitingAudio;
        private bool _awaitingPopup;

        private Action _audioFinishedHandler;
        private Action _popupHiddenHandler;

        /// <summary>Enqueues (or plays immediately when queue is disabled).</summary>
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

            if (!queueAnnouncements)
            {
                StopCurrentAnnouncementInternal();
                PlayVoiceLineNow(voiceLineId);
                return;
            }

            _pendingVoiceLines.Enqueue(voiceLineId);

            if (!_isProcessingQueue)
            {
                ProcessNextQueuedVoiceLine();
            }
        }

        /// <summary>Stops the current announcement, clears the queue, and plays immediately.</summary>
        public void PlayVoiceLineImmediate(OfficeFireVoiceLineId voiceLineId)
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

            _pendingVoiceLines.Clear();
            StopCurrentAnnouncementInternal();
            PlayVoiceLineNow(voiceLineId);
        }

        public void ClearAnnouncementQueue()
        {
            _pendingVoiceLines.Clear();
        }

        /// <summary>
        /// Clears stale queue/state and plays one finale line (OutDoor assembly).
        /// </summary>
        public void PlayAssemblyVoiceLine(OfficeFireVoiceLineId voiceLineId)
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

            _pendingVoiceLines.Clear();
            _isProcessingQueue = false;
            popupService = null;
            announcementAudioAdapter = OfficeFireAnnouncementAudioPlayback.EnsureAdapter(gameObject, null);

            if (announcementAudioAdapter != null && !announcementAudioAdapter.gameObject.activeSelf)
            {
                announcementAudioAdapter.gameObject.SetActive(true);
            }

            if (IsAnnouncementAudioPlaying())
            {
                StopCurrentAnnouncementInternal();
            }

            PlayVoiceLineNow(voiceLineId);
        }

        public IEnumerator WaitForCurrentVoiceLineAudio()
        {
            WoiAnnouncementAudioAdapter adapter =
                OfficeFireAnnouncementAudioPlayback.ResolveAdapter(announcementAudioAdapter);
            if (adapter == null)
            {
                Debug.LogWarning(
                    "[OfficeFireVoiceLineContentPresenter] No live announcement audio adapter — waiting fallback.",
                    this);
                yield return new WaitForSeconds(5f);
                yield break;
            }

            const float startupTimeoutSeconds = 3f;
            float startupElapsed = 0f;
            while (!adapter.IsAnnouncementPlaying && startupElapsed < startupTimeoutSeconds)
            {
                startupElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!adapter.IsAnnouncementPlaying)
            {
                yield break;
            }

            bool finished = false;
            void OnFinished() => finished = true;
            adapter.OnAnnouncementAudioFinished += OnFinished;

            const float maxWaitSeconds = 120f;
            float elapsed = 0f;
            while (!finished && elapsed < maxWaitSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            adapter.OnAnnouncementAudioFinished -= OnFinished;
        }

        private bool IsAnnouncementAudioPlaying()
        {
            WoiAnnouncementAudioAdapter adapter =
                OfficeFireAnnouncementAudioPlayback.ResolveAdapter(announcementAudioAdapter);
            return adapter != null && adapter.IsAnnouncementPlaying;
        }

        private void ProcessNextQueuedVoiceLine()
        {
            if (_pendingVoiceLines.Count == 0)
            {
                _isProcessingQueue = false;
                return;
            }

            _isProcessingQueue = true;
            PlayVoiceLineNow(_pendingVoiceLines.Dequeue());
        }

        private void PlayVoiceLineNow(OfficeFireVoiceLineId voiceLineId)
        {
            _playSession++;
            int session = _playSession;

            ResolveLanguageResolver();

            database.TryGetLocalizedSound(voiceLineId, out LocalizedSoundDefinition localizedSound);
            float popupDuration = OfficeFireAnnouncementAudioPlayback.EstimateDuration(localizedSound);
            if (popupDuration <= 0f)
            {
                popupDuration = defaultPopupDurationSeconds;
            }

            bool hasPopup = TryResolvePopupTexts(
                voiceLineId,
                out string titleTr,
                out string bodyTr,
                out string titleEn,
                out string bodyEn);

            if (hasPopup)
            {
                ShowAnnouncementPopup(titleTr, bodyTr, titleEn, bodyEn, popupDuration, replacePopup: !queueAnnouncements);
            }

            bool hasAudio = localizedSound != null;
            if (hasAudio)
            {
                announcementAudioAdapter = OfficeFireAnnouncementAudioPlayback.EnsureAdapter(
                    gameObject,
                    announcementAudioAdapter);
                OfficeFireAnnouncementAudioPlayback.Play(announcementAudioAdapter, localizedSound);
                Debug.Log($"[OfficeFire Voice] {voiceLineId}", this);
            }

            BeginCompletionTracking(session, hasAudio, hasPopup);
        }

        private void BeginCompletionTracking(int session, bool trackAudio, bool trackPopup)
        {
            ClearCompletionHandlers();

            _awaitingAudio = trackAudio;
            _awaitingPopup = trackPopup;

            if (!_awaitingAudio && !_awaitingPopup)
            {
                OnCurrentVoiceLineFinished(session);
                return;
            }

            if (_awaitingAudio)
            {
                WoiAnnouncementAudioAdapter adapter = OfficeFireAnnouncementAudioPlayback.ResolveAdapter(announcementAudioAdapter);
                if (adapter == null)
                {
                    _awaitingAudio = false;
                }
                else
                {
                    _audioFinishedHandler = () =>
                    {
                        if (session != _playSession)
                        {
                            return;
                        }

                        _awaitingAudio = false;
                        TryCompleteVoiceLine(session);
                    };

                    adapter.OnAnnouncementAudioFinished += _audioFinishedHandler;
                }
            }

            if (_awaitingPopup)
            {
                ResolvePopupService();
                if (popupService == null)
                {
                    _awaitingPopup = false;
                }
                else
                {
                    _popupHiddenHandler = () =>
                    {
                        if (session != _playSession)
                        {
                            return;
                        }

                        _awaitingPopup = false;
                        TryCompleteVoiceLine(session);
                    };

                    popupService.OnPopupHidden += _popupHiddenHandler;
                }
            }

            TryCompleteVoiceLine(session);
        }

        private void TryCompleteVoiceLine(int session)
        {
            if (session != _playSession)
            {
                return;
            }

            if (_awaitingAudio || _awaitingPopup)
            {
                return;
            }

            OnCurrentVoiceLineFinished(session);
        }

        private void OnCurrentVoiceLineFinished(int session)
        {
            if (session != _playSession)
            {
                return;
            }

            ClearCompletionHandlers();

            if (!queueAnnouncements)
            {
                _isProcessingQueue = false;
                return;
            }

            ProcessNextQueuedVoiceLine();
        }

        private void StopCurrentAnnouncementInternal()
        {
            _playSession++;
            ClearCompletionHandlers();
            OfficeFireAnnouncementAudioPlayback.Stop(announcementAudioAdapter);

            ResolvePopupService();
            if (popupService != null && popupService.IsVisible)
            {
                popupService.Hide();
            }
        }

        private void ClearCompletionHandlers()
        {
            if (_audioFinishedHandler != null)
            {
                WoiAnnouncementAudioAdapter adapter = OfficeFireAnnouncementAudioPlayback.ResolveAdapter(announcementAudioAdapter);
                if (adapter != null)
                {
                    adapter.OnAnnouncementAudioFinished -= _audioFinishedHandler;
                }

                _audioFinishedHandler = null;
            }

            if (_popupHiddenHandler != null)
            {
                ResolvePopupService();
                if (popupService != null)
                {
                    popupService.OnPopupHidden -= _popupHiddenHandler;
                }

                _popupHiddenHandler = null;
            }

            _awaitingAudio = false;
            _awaitingPopup = false;
        }

        private void ShowAnnouncementPopup(
            string titleTr,
            string bodyTr,
            string titleEn,
            string bodyEn,
            float durationSeconds,
            bool replacePopup)
        {
            ResolvePopupService();
            if (popupService == null || !popupService.isActiveAndEnabled)
            {
                Debug.LogWarning("[OfficeFireVoiceLineContentPresenter] PopupService not found — popup skipped.", this);
                return;
            }

            float duration = Mathf.Max(0.5f, durationSeconds);

            if (replacePopup && popupService.IsVisible)
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
            if (popupService != null && popupService.isActiveAndEnabled)
            {
                return;
            }

            popupService = null;

            if (ServiceLocator.TryGet<PopupService>(out PopupService concrete) && concrete != null)
            {
                popupService = concrete;
            }
            else if (ServiceLocator.TryGet<IPopupService>(out IPopupService service) && service is PopupService resolved)
            {
                popupService = resolved;
            }
            else
            {
                popupService = FindFirstObjectByType<PopupService>(FindObjectsInactive.Include);
            }

            if (popupService != null && !popupService.gameObject.activeSelf)
            {
                popupService.gameObject.SetActive(true);
            }
        }

        private void ResolveLanguageResolver()
        {
            if (_languageResolver != null)
            {
                return;
            }

            if (ServiceLocator.TryGet(out OfficeFireLanguageResolver registered) && registered != null)
            {
                _languageResolver = registered;
                return;
            }

            _languageResolver = FindFirstObjectByType<OfficeFireLanguageResolver>();
            if (_languageResolver == null)
            {
                _languageResolver = gameObject.AddComponent<OfficeFireLanguageResolver>();
            }
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

        private void OnDisable()
        {
            _pendingVoiceLines.Clear();
            StopCurrentAnnouncementInternal();
            _isProcessingQueue = false;
        }
    }
}
