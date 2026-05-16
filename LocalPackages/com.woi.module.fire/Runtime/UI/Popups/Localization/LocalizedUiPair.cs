using System.Collections.Generic;
using WOI.Modules.SDK;

namespace Woi.UI.Popups.Localization
{
    /// <summary>
    /// İngilizce + Türkçe çiftlerini <see cref="ILocalizationService"/> / <see cref="LocalizationService"/> ile çözer.
    /// </summary>
    public static class LocalizedUiPair
    {
        /// <summary>
        /// Aktif dile göre metin döner; servis yoksa <paramref name="english"/> kullanılır.
        /// </summary>
        public static string Resolve(string english, string turkish)
        {
            var lt = new LocalizedText
            {
                entries = new List<LocalizedStringEntry>(2)
                {
                    new LocalizedStringEntry
                    {
                        languageCode = LocalizationService.English,
                        text = english ?? string.Empty,
                    },
                    new LocalizedStringEntry
                    {
                        languageCode = LocalizationService.Turkish,
                        text = turkish ?? string.Empty,
                    },
                },
            };

            if (ServiceLocator.TryGet<ILocalizationService>(out ILocalizationService loc) && loc != null)
                return loc.GetText(lt);

            if (LocalizationService.Instance != null)
                return LocalizationService.Instance.GetText(lt);

            return english ?? string.Empty;
        }
    }
}
