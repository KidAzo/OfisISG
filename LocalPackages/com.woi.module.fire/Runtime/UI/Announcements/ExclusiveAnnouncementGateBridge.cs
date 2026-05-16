using Obvious.Soap;
using UnityEngine;
using UnityEngine.Serialization;

namespace Woi.UI.Announcements
{
    /// <summary>
    /// Inspector-callable hooks for <see cref="ExclusiveAnnouncementPlaybackGate"/>.
    /// Typical FireWarehouse wiring: LevelController gameplay-started → <see cref="Hold"/> then AudioTrigger.Play;
    /// AudioTrigger onPlaybackFinished → <see cref="Release"/> after Queue All / single clip ends.
    /// Optional: Soap event raised after the gate exits (for other listeners).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/UI/Exclusive Announcement Gate Bridge")]
    public sealed class ExclusiveAnnouncementGateBridge : MonoBehaviour
    {
        [Header("Optional Soap event")]
        [Tooltip("When set, Release() calls Raise() after ExclusiveAnnouncementPlaybackGate.Exit().")]
        [FormerlySerializedAs("raiseWhenReleasedForHoverUnlock")]
        [SerializeField]
        private ScriptableEventNoParam raiseWhenReleased;

        private void OnDisable()
        {
            // Scene unload / disable mid-intro: avoid leaving the gate latched if Release never ran.
            ExclusiveAnnouncementPlaybackGate.Exit();
        }

        public void Hold() => ExclusiveAnnouncementPlaybackGate.Enter();

        public void Release()
        {
            ExclusiveAnnouncementPlaybackGate.Exit();
            raiseWhenReleased?.Raise();
        }
    }
}
