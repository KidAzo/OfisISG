using WOI.Modules.SDK;
using Woi.UI.Announcements;
using Woi.UI.Popups;
using Woi.UI.Popups.Localization;

namespace Woi.UI.Bootstrap
{
    /// <summary>
    /// Typed accessors for <see cref="ServiceLocator"/>.
    /// Populate the locator via <see cref="UiMessagingServiceInstaller"/> and/or
    /// <see cref="PopupService"/> / <see cref="AnnouncementService"/> / <see cref="LocalizationService"/> on-Awake registration.
    /// </summary>
    public static class UiMessagingLocator
    {
        public static bool TryPopups(out IPopupService service) =>
            ServiceLocator.TryGet(out service);

        public static bool TryAnnouncements(out IAnnouncementService service) =>
            ServiceLocator.TryGet(out service);

        public static bool TryLocalization(out ILocalizationService service) =>
            ServiceLocator.TryGet(out service);

        public static IPopupService PopupsOrNull() =>
            TryPopups(out IPopupService s) ? s : null;

        public static IAnnouncementService AnnouncementsOrNull() =>
            TryAnnouncements(out IAnnouncementService s) ? s : null;

        public static ILocalizationService LocalizationOrNull() =>
            TryLocalization(out ILocalizationService s) ? s : null;
    }
}
