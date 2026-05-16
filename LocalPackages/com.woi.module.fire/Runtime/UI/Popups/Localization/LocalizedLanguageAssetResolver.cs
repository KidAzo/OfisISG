using System;
using WOI.Modules.SDK;

namespace Woi.UI.Popups.Localization
{
    /// <summary>
    /// Chooses EN vs TR ScriptableObject references using <see cref="ILocalizationService.CurrentLanguage"/>.
    /// </summary>
    public static class LocalizedLanguageAssetResolver
    {
        public static T Pick<T>(T english, T turkish) where T : class
        {
            string code = ResolveLanguageCode();

            if (IsTurkish(code))
            {
                if (turkish != null)
                    return turkish;
                return english;
            }

            if (english != null)
                return english;
            return turkish;
        }

        private static string ResolveLanguageCode()
        {
            if (ServiceLocator.TryGet<ILocalizationService>(out var loc) && loc != null && !string.IsNullOrEmpty(loc.CurrentLanguage))
                return loc.CurrentLanguage.Trim().ToLowerInvariant();

            if (LocalizationService.Instance != null && !string.IsNullOrEmpty(LocalizationService.Instance.CurrentLanguage))
                return LocalizationService.Instance.CurrentLanguage.Trim().ToLowerInvariant();

            return LocalizationService.Turkish;
        }

        /// <summary>Active UI language code from ServiceLocator / LocalizationService (e.g. en, tr, de).</summary>
        public static string GetCurrentLanguageCode() => ResolveLanguageCode();

        private static bool IsTurkish(string code) =>
            string.Equals(code, LocalizationService.Turkish, StringComparison.OrdinalIgnoreCase);
    }
}
