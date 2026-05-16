using System.Collections.Generic;

namespace Woi.UI.Popups.Localization
{
    /// <summary>
    /// Abstraction for <see cref="LocalizationService"/> — resolve via <c>ServiceLocator</c>.
    /// </summary>
    public interface ILocalizationService
    {
        string CurrentLanguage { get; }

        IReadOnlyList<string> SupportedLanguageCodes { get; }

        void SetLanguage(string languageCode);

        string GetText(LocalizedText localized);

        /// <summary>Prefer list slot <paramref name="entryIndex"/> when pairing queued clips to title/message lines.</summary>
        string GetText(LocalizedText localized, int entryIndex);

        /// <summary>
        /// Resolves title and message from the same <see cref="PopupLocalizedLine"/> (current language → English → first non-empty row).
        /// </summary>
        void GetPopupVariantText(PopupContentVariant variant, out string title, out string message);
    }
}
