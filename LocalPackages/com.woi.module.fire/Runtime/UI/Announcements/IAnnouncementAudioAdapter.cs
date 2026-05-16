using System;
using WoiUtils.AudioSystem;

namespace Woi.UI.Announcements
{
    /// <summary>Abstract announcement playback — implemented by <see cref="WoiAnnouncementAudioAdapter"/>.</summary>
    public interface IAnnouncementAudioAdapter
    {
        bool IsAnnouncementPlaying { get; }

        event Action OnAnnouncementAudioFinished;

        void PlayAnnouncement(SoundDefinition sound);
        void StopCurrentAnnouncement();
    }
}
