using System;
using UnityEngine;
using Woi.UI.Popups;

namespace Woi.UI.Announcements
{
    public interface IAnnouncementService
    {
        event Action<AnnouncementDefinition> OnAnnouncementStarted;
        event Action<AnnouncementDefinition> OnAnnouncementFinished;
        event Action<AnnouncementDefinition> OnAnnouncementInterrupted;
        event Action<AnnouncementDefinition> OnAnnouncementIgnored;

        void Play(AnnouncementDefinition definition);

        /// <summary>Plays the resolved EN or TR <see cref="AnnouncementDefinition"/> from the bundle.</summary>
        void Play(LocalizedAnnouncementDefinition bundle);

        void StopCurrentAnnouncement();
    }
}
