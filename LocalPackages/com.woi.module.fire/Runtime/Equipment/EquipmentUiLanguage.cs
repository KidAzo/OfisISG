using WOI.Modules.SDK;
using Woi.UI.Popups.Localization;

namespace Woi.Equipment
{
    /// <summary>
    /// HUD / pickup için aktif UI dil kodu (<c>tr</c>, <c>en</c>, …).
    /// </summary>
    internal static class EquipmentUiLanguage
    {
        public static string CurrentCode()
        {
            if (ServiceLocator.TryGet<ILocalizationService>(out ILocalizationService loc) && loc != null)
            {
                string c = loc.CurrentLanguage;
                if (!string.IsNullOrWhiteSpace(c))
                    return c.Trim().ToLowerInvariant();
            }

            if (LocalizationService.Instance != null)
            {
                string c = LocalizationService.Instance.CurrentLanguage;
                if (!string.IsNullOrWhiteSpace(c))
                    return c.Trim().ToLowerInvariant();
            }

            return LocalizationService.Turkish;
        }
    }
}
