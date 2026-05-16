using UnityEngine;
using Woi.UI.Popups;
using WoiUtils.AudioSystem;

namespace Woi.UI.Announcements
{
    [CreateAssetMenu(fileName = "AnnouncementDefinition", menuName = "Woi/UI/Announcement Definition", order = 1)]
    public sealed class AnnouncementDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id;

        [Header("Audio (Woi Audio)")]
        public bool playAudio;
        public SoundDefinition sound;

        [Header("Popup")]
        public bool showPopup;
        public PopupDefinition popupDefinition;

        [Tooltip("When off (default), the announcement popup does not use a full-screen input blocker — gameplay and other UI (e.g. fire type selection) stay interactive. When on, pointer events are captured like a modal (same as Popup Definition → Block Input).")]
        public bool popupBlocksInput = false;

        [Tooltip("Optional: assign one popup per clip index when Sound uses Queue All (clip 0 → element 0, etc.). Null slots use Popup Definition. Popup duration follows each clip length.")]
        public PopupDefinition[] popupPerClip;

        [Header("Interrupt & coordination")]
        [Tooltip("When true, may interrupt an announcement of the same priority.")]
        public bool interruptCurrentAnnouncement = true;

        public bool stopPreviousAnnouncementAudio = true;
        [Tooltip("When true, uses PopupService.Replace (immediate swap). When false, uses Show so the popup can queue behind the current one if PopupService overflow is Queue Next.")]
        public bool replaceCurrentPopup = true;

        [Tooltip("When audio ends, hide the popup if it is still visible.")]
        public bool closePopupWhenAudioEnds;

        [Tooltip("When audio + popup are both enabled and Popup Duration Override is 0, popup auto-close uses estimated clip length (non-looping sounds). Requires Auto Close on the Popup Definition.")]
        public bool syncPopupDurationWithSound = true;

        [Tooltip("If greater than zero, popup uses this many seconds instead of the sound-length estimate.")]
        public float popupDurationOverride;

        [Tooltip("Skip priority comparison — use sparingly (e.g. admin messages).")]
        public bool bypassPriorityGate;

        [Tooltip(
            "While this announcement is active (audio and/or popup until fully finished), other Play() calls are ignored. " +
            "Does not affect sounds played only via AudioSystem / AudioTrigger — use Gated Scene Intro Audio Player for those.")]
        public bool exclusiveAnnouncementPlayback;

        [Header("Priority")]
        public AnnouncementPriority priority = AnnouncementPriority.Normal;
    }
}
