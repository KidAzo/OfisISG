using UnityEngine;
using Woi.UI.Popups.Localization;

namespace Woi.UI.Announcements
{
    /// <summary>
    /// References two <see cref="AnnouncementDefinition"/> assets (English / Turkish). At runtime the matching
    /// one is chosen from <see cref="ILocalizationService.CurrentLanguage"/>; use with <see cref="IAnnouncementService.Play(LocalizedAnnouncementDefinition)"/> or <see cref="ResolveForCurrentLanguage"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizedAnnouncement", menuName = "Woi/UI/Localized Announcement (EN + TR)", order = 2)]
    public sealed class LocalizedAnnouncementDefinition : ScriptableObject
    {
        [Tooltip("Played when Current Language is en (or when Turkish asset is missing).")]
        public AnnouncementDefinition english;

        [Tooltip("Played when Current Language is tr.")]
        public AnnouncementDefinition turkish;

        /// <summary>Resolved announcement for the active UI language.</summary>
        public AnnouncementDefinition ResolveForCurrentLanguage() =>
            LocalizedLanguageAssetResolver.Pick(english, turkish);
    }
}
