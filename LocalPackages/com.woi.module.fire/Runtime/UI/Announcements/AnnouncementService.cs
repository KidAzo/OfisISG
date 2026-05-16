/*
 * ANNOUNCEMENT SERVICE — SETUP
 * ----------------------------
 * 1. Scene needs: PopupService + UIDocument (popup UXML), optional LocalizationService.
 * 2. Add WoiAnnouncementAudioAdapter next to or referencing your AudioSystem (optional for popup-only).
 * 3. Add AnnouncementService; assign PopupService only if needed, otherwise it resolves IPopupService / PopupService from ServiceLocator (PopupService registers in Start at order -5000).
 * 4. Create AnnouncementDefinition assets (Create → Woi → UI → Announcement Definition).
 * 5. Optional: for SoundDefinition set to Queue All with multiple clips, set Popup Per Clip on the announcement so each clip gets its own popup and duration.
 * 6. Scene intro VO not using this service: add Gated Scene Intro Audio Player (holds ExclusiveAnnouncementPlaybackGate until audio ends).
 * 7. For announcement assets that must finish before any other Play(): enable Exclusive Announcement Playback on the Announcement Definition.
 */

using System;
using UnityEngine;
using WOI.Modules.SDK;
using Woi.UI.Popups;
using Woi.UI.Popups.Localization;
using WoiUtils.AudioSystem;

namespace Woi.UI.Announcements
{
    [DefaultExecutionOrder(-4990)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/UI/Announcement Service")]
    public sealed class AnnouncementService : MonoBehaviour, IAnnouncementService
    {
        [Tooltip("Optional. If empty, resolved from ServiceLocator (IPopupService / PopupService) after PopupService.Start, then FindFirstObjectByType.")]
        [SerializeField]
        private PopupService popupService;

        [SerializeField] private WoiAnnouncementAudioAdapter audioAdapter;

        [Header("Service locator")]
        [Tooltip("Registers IAnnouncementService / AnnouncementService on ServiceLocator in Start when not already registered.")]
        [SerializeField]
        private bool registerWithServiceLocator = true;

        private bool _registeredWithServiceLocator;

        private AnnouncementDefinition _active;

        private int _session;
        private bool _audioDone;
        private bool _popupDone;

        private Action _audioFinishedHandler;
        private Action _popupHiddenHandler;

        private Action<SoundDefinition, int> _queueClipIndexHandler;
        private AudioSystem _queueClipAudioSystem;

        public event Action<AnnouncementDefinition> OnAnnouncementStarted;
        public event Action<AnnouncementDefinition> OnAnnouncementFinished;
        public event Action<AnnouncementDefinition> OnAnnouncementInterrupted;
        public event Action<AnnouncementDefinition> OnAnnouncementIgnored;

        private void Start()
        {
            ResolvePopupService();
            TryRegisterWithServiceLocator();
        }

        private void ResolvePopupService()
        {
            if (popupService != null)
                return;

            if (ServiceLocator.TryGet<PopupService>(out var concrete) && concrete != null)
            {
                popupService = concrete;
                return;
            }

            if (ServiceLocator.TryGet<IPopupService>(out var svc) && svc is PopupService ps)
            {
                popupService = ps;
                return;
            }

            popupService = FindFirstObjectByType<PopupService>();
        }

        private void TryRegisterWithServiceLocator()
        {
            if (!registerWithServiceLocator)
                return;

            if (ServiceLocator.IsRegistered<IAnnouncementService>())
                return;

            ServiceLocator.Register<IAnnouncementService>(this);
            ServiceLocator.Register<AnnouncementService>(this);
            _registeredWithServiceLocator = true;
        }

        private void TryUnregisterWithServiceLocator()
        {
            if (!_registeredWithServiceLocator)
                return;

            ServiceLocator.Unregister<IAnnouncementService>();
            ServiceLocator.Unregister<AnnouncementService>();
            _registeredWithServiceLocator = false;
        }

        public void Play(AnnouncementDefinition definition)
        {
            ResolvePopupService();

            if (definition == null)
                return;

            PlayCore(definition);
        }

        /// <summary>Resolves EN/TR from <paramref name="bundle"/> using current language, then plays.</summary>
        public void Play(LocalizedAnnouncementDefinition bundle)
        {
            ResolvePopupService();

            if (bundle == null)
                return;

            AnnouncementDefinition resolved = bundle.ResolveForCurrentLanguage();
            if (resolved == null)
            {
                Debug.LogWarning("[AnnouncementService] LocalizedAnnouncementDefinition resolved to null (assign English and/or Turkish).", bundle);
                return;
            }

            PlayCore(resolved);
        }

        private void PlayCore(AnnouncementDefinition definition)
        {
            if (!CanAccept(definition))
            {
                Debug.Log($"[AnnouncementService] Ignored (priority): {definition.id}");
                OnAnnouncementIgnored?.Invoke(definition);
                return;
            }

            ClearHandlers();

            InterruptPrevious(definition);

            _session++;
            int session = _session;

            _active = definition;

            Debug.Log($"[AnnouncementService] Started: {definition.id}");

            OnAnnouncementStarted?.Invoke(definition);

            bool needsAudio = definition.playAudio && definition.sound != null && audioAdapter != null;
            bool needsPopup = definition.showPopup && HasPopupAssets(definition) && popupService != null;
            bool wantClipSync = ShouldUseClipSyncedPopups(definition) && needsAudio && needsPopup;
            bool clipPopupSync = false;

            _audioDone = !needsAudio;
            _popupDone = !needsPopup;

            if (needsAudio)
            {
                _audioFinishedHandler = () =>
                {
                    if (session != _session)
                        return;

                    if (audioAdapter != null && _audioFinishedHandler != null)
                        audioAdapter.OnAnnouncementAudioFinished -= _audioFinishedHandler;

                    _audioDone = true;

                    if (definition.closePopupWhenAudioEnds && popupService != null && popupService.IsVisible)
                        popupService.Hide();

                    TryFinalize(session, definition);
                };

                audioAdapter.OnAnnouncementAudioFinished += _audioFinishedHandler;

                if (wantClipSync && AudioSystem.TryGetFromServiceLocator(out var queueAudio))
                {
                    _queueClipIndexHandler = (sound, idx) => OnQueueClipIndexChanged(definition, session, sound, idx);
                    queueAudio.OnQueueIndexChanged += _queueClipIndexHandler;
                    _queueClipAudioSystem = queueAudio;
                    clipPopupSync = true;
                }
                else if (wantClipSync)
                {
                    Debug.LogWarning(
                        "[AnnouncementService] Queue All + popup sync requires AudioSystem on ServiceLocator (same as announcement audio). Clip-sync UI skipped.",
                        this);
                }

                audioAdapter.PlayAnnouncement(definition.sound);
            }

            if (clipPopupSync)
                _popupDone = true;

            if (needsPopup && clipPopupSync)
            {
                // Popups are shown from AudioSystem.OnQueueIndexChanged (each queued clip, scoped by SoundDefinition).
            }
            else if (needsPopup)
            {
                PopupDefinition popDef = ResolvePopupForClipIndex(definition, 0);
                if (popDef == null)
                {
                    _popupDone = true;
                }
                else
                {
                    _popupHiddenHandler = () =>
                    {
                        if (session != _session)
                            return;

                        if (popupService != null && _popupHiddenHandler != null)
                            popupService.OnPopupHidden -= _popupHiddenHandler;

                        _popupDone = true;
                        TryFinalize(session, definition);
                    };

                    popupService.OnPopupHidden += _popupHiddenHandler;

                    bool hasDurationOverride = false;
                    float popupDuration = -1f;

                    if (definition.popupDurationOverride > 0f)
                    {
                        popupDuration = definition.popupDurationOverride;
                        hasDurationOverride = true;
                    }
                    else if (needsAudio && definition.syncPopupDurationWithSound && definition.sound != null && !definition.sound.loop)
                    {
                        float est = WoiAnnouncementAudioAdapter.EstimatePlaybackDuration(definition.sound);
                        if (est > 0f)
                        {
                            popupDuration = est;
                            hasDurationOverride = true;
                        }
                    }

                    if (definition.replaceCurrentPopup)
                    {
                        if (hasDurationOverride)
                            popupService.Replace(popDef, popupDuration, definition.popupBlocksInput);
                        else
                            popupService.Replace(popDef, -1f, definition.popupBlocksInput);
                    }
                    else if (hasDurationOverride)
                    {
                        popupService.Show(popDef, popupDuration, definition.popupBlocksInput);
                    }
                    else
                    {
                        popupService.Show(popDef, -1f, definition.popupBlocksInput);
                    }
                }
            }

            TryFinalize(session, definition);
        }

        public void StopCurrentAnnouncement()
        {
            ResolvePopupService();

            ClearHandlers();

            if (audioAdapter != null)
                audioAdapter.StopCurrentAnnouncement();

            if (popupService != null)
                popupService.Hide();

            AnnouncementDefinition cleared = _active;
            _active = null;
            _session++;

            if (cleared != null)
                OnAnnouncementInterrupted?.Invoke(cleared);
        }

        private bool CanAccept(AnnouncementDefinition incoming)
        {
            if (incoming == null)
                return false;

            if (incoming.bypassPriorityGate)
                return true;

            if (ExclusiveAnnouncementPlaybackGate.IsBlocking)
                return false;

            if (_active != null && _active.exclusiveAnnouncementPlayback)
                return false;

            if (_active == null)
                return true;

            int n = (int)incoming.priority;
            int a = (int)_active.priority;

            if (n > a)
                return true;

            if (n == a && incoming.interruptCurrentAnnouncement)
                return true;

            return false;
        }

        private void InterruptPrevious(AnnouncementDefinition incoming)
        {
            if (_active == null)
                return;

            AnnouncementDefinition previous = _active;

            if (incoming.stopPreviousAnnouncementAudio && audioAdapter != null)
                audioAdapter.StopCurrentAnnouncement();

            if (popupService != null && incoming.replaceCurrentPopup)
                popupService.Hide();

            _active = null;

            Debug.Log($"[AnnouncementService] Interrupted: {previous.id}");
            OnAnnouncementInterrupted?.Invoke(previous);
        }

        private void ClearHandlers()
        {
            if (audioAdapter != null && _audioFinishedHandler != null)
            {
                audioAdapter.OnAnnouncementAudioFinished -= _audioFinishedHandler;
                _audioFinishedHandler = null;
            }

            if (popupService != null && _popupHiddenHandler != null)
            {
                popupService.OnPopupHidden -= _popupHiddenHandler;
                _popupHiddenHandler = null;
            }

            if (_queueClipAudioSystem != null && _queueClipIndexHandler != null)
            {
                _queueClipAudioSystem.OnQueueIndexChanged -= _queueClipIndexHandler;
                _queueClipAudioSystem = null;
                _queueClipIndexHandler = null;
            }
        }

        private static bool HasPopupAssets(AnnouncementDefinition d)
        {
            if (d == null)
                return false;
            if (d.popupDefinition != null)
                return true;
            if (d.popupPerClip == null)
                return false;
            foreach (var p in d.popupPerClip)
            {
                if (p != null)
                    return true;
            }
            return false;
        }

        private static bool ShouldUseClipSyncedPopups(AnnouncementDefinition d)
        {
            if (d.sound == null || d.sound.clips == null || d.sound.clips.Count < 2)
                return false;
            if (d.sound.selectionMode != ClipSelectionMode.QueueAll)
                return false;
            return HasPopupAssets(d);
        }

        private static PopupDefinition ResolvePopupForClipIndex(AnnouncementDefinition def, int clipIndex)
        {
            if (def.popupPerClip != null && clipIndex >= 0 && clipIndex < def.popupPerClip.Length && def.popupPerClip[clipIndex] != null)
                return def.popupPerClip[clipIndex];
            return def.popupDefinition;
        }

        private static float GetClipEntryDurationSeconds(SoundDefinition sound, int clipIndex)
        {
            if (sound?.clips == null || clipIndex < 0 || clipIndex >= sound.clips.Count)
                return 0f;
            ClipEntry e = sound.clips[clipIndex];
            if (e?.clip == null)
                return 0f;
            return Mathf.Max(0f, e.delay) + e.clip.length;
        }

        private void OnQueueClipIndexChanged(AnnouncementDefinition definition, int session, SoundDefinition sound, int clipIndex)
        {
            if (session != _session || definition != _active || popupService == null)
                return;

            if (sound == null || definition.sound != sound)
                return;

            PopupDefinition pop = ResolvePopupForClipIndex(definition, clipIndex);
            if (pop == null)
                return;

            bool resolvedFromPerClipSlot = definition.popupPerClip != null
                && clipIndex >= 0
                && clipIndex < definition.popupPerClip.Length
                && definition.popupPerClip[clipIndex] != null
                && ReferenceEquals(pop, definition.popupPerClip[clipIndex]);

            int contentEntryIndex = resolvedFromPerClipSlot ? -1 : clipIndex;

            float clipDur = GetClipEntryDurationSeconds(definition.sound, clipIndex);
            bool hasDur = clipDur > 0f && definition.sound != null && !definition.sound.loop;

            if (clipIndex > 0)
            {
                if (hasDur)
                    popupService.Replace(pop, clipDur, contentEntryIndex, definition.popupBlocksInput);
                else
                    popupService.Replace(pop, -1f, contentEntryIndex, definition.popupBlocksInput);
                return;
            }

            if (definition.replaceCurrentPopup)
            {
                if (hasDur)
                    popupService.Replace(pop, clipDur, contentEntryIndex, definition.popupBlocksInput);
                else
                    popupService.Replace(pop, -1f, contentEntryIndex, definition.popupBlocksInput);
            }
            else if (hasDur)
                popupService.Show(pop, clipDur, contentEntryIndex, definition.popupBlocksInput);
            else
                popupService.Show(pop, -1f, contentEntryIndex, definition.popupBlocksInput);
        }

        private void TryFinalize(int session, AnnouncementDefinition definition)
        {
            if (session != _session)
                return;

            if (!_audioDone || !_popupDone)
                return;

            if (_active != definition)
                return;

            _active = null;
            ClearHandlers();

            OnAnnouncementFinished?.Invoke(definition);
        }

        private void OnDestroy()
        {
            ClearHandlers();
            TryUnregisterWithServiceLocator();
        }
    }
}
