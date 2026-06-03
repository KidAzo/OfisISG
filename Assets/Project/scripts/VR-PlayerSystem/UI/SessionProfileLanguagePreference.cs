using Woi.Events.Data;
using Woi.UI.Popups.Localization;
using Woi.WasteCollectionMode;
using WOI.Modules.SDK;

namespace Woi.DataHandler
{
    /// <summary>
    /// Keeps the language chosen on the session overlay and pushes it to all runtime localization hosts.
    /// </summary>
    public static class SessionProfileLanguagePreference
    {
        public static bool HasUserChoice => SessionLanguageState.HasUserChoice;

        public static void RecordUserChoice(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return;

            SessionLanguageState.RecordUserChoice(languageCode);
            ApplyToGame(languageCode);
            SyncWasteLoginSessionLanguage(languageCode);
        }

        public static string ResolveForOverlay()
        {
            if (SessionLanguageState.HasUserChoice)
                return SessionLanguageState.LanguageCode;

            return ResolveFromGameOrDefault();
        }

        public static void ReapplyToGame()
        {
            if (SessionLanguageState.HasUserChoice)
                ApplyToGame(SessionLanguageState.LanguageCode);
            else
                ApplyToGame(ResolveFromGameOrDefault());
        }

        private static void SyncWasteLoginSessionLanguage(string languageCode)
        {
            if (!WasteLoginSession.IsSet)
                return;

            WasteLoginSession.Set(WasteLoginSession.UserName, WasteLoginSession.UserId, languageCode);
        }

        private static string ResolveFromGameOrDefault()
        {
            if (SessionLanguageState.HasUserChoice)
                return SessionLanguageState.LanguageCode;

            if (ServiceLocator.TryGet(out ILocalizationService localization) && localization != null
                && !string.IsNullOrEmpty(localization.CurrentLanguage))
            {
                return localization.CurrentLanguage.Trim().ToLowerInvariant();
            }

            if (LocalizationService.Instance != null
                && !string.IsNullOrEmpty(LocalizationService.Instance.CurrentLanguage))
            {
                return LocalizationService.Instance.CurrentLanguage.Trim().ToLowerInvariant();
            }

            return LocalizationService.Turkish;
        }

        public static void ApplyToGame(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return;

            languageCode = languageCode.Trim().ToLowerInvariant();

            LocalizationService[] services = UnityEngine.Object.FindObjectsByType<LocalizationService>(
                UnityEngine.FindObjectsInactive.Include,
                UnityEngine.FindObjectsSortMode.None);

            for (int i = 0; i < services.Length; i++)
            {
                LocalizationService service = services[i];
                if (service != null)
                    service.SetLanguage(languageCode);
            }

            if (ServiceLocator.TryGet(out ILocalizationService localization) && localization != null)
                localization.SetLanguage(languageCode);
        }
    }
}
