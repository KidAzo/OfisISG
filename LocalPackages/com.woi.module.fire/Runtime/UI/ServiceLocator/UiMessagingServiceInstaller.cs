using UnityEngine;
using WOI.Modules.SDK;
using Woi.UI.Announcements;
using Woi.UI.Popups;
using Woi.UI.Popups.Localization;

namespace Woi.UI.Bootstrap
{
    /// <summary>
    /// Runs in <see cref="Start"/> (order -5100) so registration happens after all <c>Awake</c> methods. Resolve services from <see cref="ServiceLocator"/> in <b>Start</b> or later, not in <c>Awake</c>.
    /// Add to your bootstrap / persistent UI root (often DontDestroyOnLoad) alongside your UI Document + services.
    ///
    /// Execution order is after <c>FireServiceInstaller</c> (Addressables) and before default <c>Start</c> on gameplay scripts.
    ///
    /// Usage from gameplay:
    /// <code>
    /// if (ServiceLocator.TryGet&lt;IPopupService&gt;(out var popups))
    ///     popups.Show(definition);
    /// if (ServiceLocator.TryGet&lt;IAnnouncementService&gt;(out var ann))
    ///     ann.Play(definition);
    /// if (ServiceLocator.TryGet&lt;ILocalizationService&gt;(out var loc))
    ///     loc.SetLanguage("tr");
    /// </code>
    /// </summary>
    [DefaultExecutionOrder(-5100)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/UI/Bootstrap/UI Messaging Service Installer")]
    public sealed class UiMessagingServiceInstaller : MonoBehaviour
    {
        [SerializeField] private PopupService popupService;
        [SerializeField] private AnnouncementService announcementService;
        [SerializeField] private LocalizationService localizationService;

        private void Start()
        {
            ReplaceRegistration<IPopupService, PopupService>(ResolvePopup());
            ReplaceRegistration<IAnnouncementService, AnnouncementService>(ResolveAnnouncement());
            ReplaceRegistration<ILocalizationService, LocalizationService>(ResolveLocalization());
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ILocalizationService>();
            ServiceLocator.Unregister<LocalizationService>();
            ServiceLocator.Unregister<IAnnouncementService>();
            ServiceLocator.Unregister<AnnouncementService>();
            ServiceLocator.Unregister<IPopupService>();
            ServiceLocator.Unregister<PopupService>();
        }

        private LocalizationService ResolveLocalization()
        {
            if (localizationService != null)
                return localizationService;

            localizationService = FindFirstObjectByType<LocalizationService>();
            return localizationService;
        }

        private PopupService ResolvePopup()
        {
            if (popupService != null)
                return popupService;

            popupService = FindFirstObjectByType<PopupService>();
            if (popupService == null)
                Debug.LogWarning("[UiMessagingServiceInstaller] No PopupService found — assign or add one with UIDocument.");

            return popupService;
        }

        private AnnouncementService ResolveAnnouncement()
        {
            if (announcementService != null)
                return announcementService;

            announcementService = FindFirstObjectByType<AnnouncementService>();
            return announcementService;
        }

        private static void ReplaceRegistration<TInterface, TConcrete>(TConcrete instance)
            where TInterface : class
            where TConcrete : class, TInterface
        {
            ServiceLocator.Unregister<TInterface>();
            ServiceLocator.Unregister<TConcrete>();

            if (instance == null)
                return;

            ServiceLocator.Register<TInterface>(instance);
            ServiceLocator.Register<TConcrete>(instance);
        }
    }
}
