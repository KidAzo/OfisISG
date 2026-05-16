using UnityEngine;
using Woi.UI.Popups.Localization;
using WOI.Modules.SDK;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Bridges the global localization host (same as popups/HUD) into Office Fire content code
    /// so scenario assemblies do not reference SDK types directly.
    /// </summary>
    public sealed class OfficeFireLanguageResolver : MonoBehaviour
    {
        [Tooltip("Optional explicit host; otherwise ServiceLocator / LocalizationService.Instance is used.")]
        [SerializeField]
        private LocalizationService localizationService;

        /// <summary>
        /// Allows callers (e.g. content presenters) to forward an inspector-assigned host at runtime.
        /// </summary>
        public void AssignHost(LocalizationService service)
        {
            localizationService = service;
        }

        public bool IsTurkish()
        {
            string code = ResolveLanguageCode();
            if (string.IsNullOrEmpty(code))
            {
                return true;
            }

            return code == LocalizationService.Turkish || code == "turkish";
        }

        public bool IsEnglish()
        {
            string code = ResolveLanguageCode();
            return code == LocalizationService.English || code == "english";
        }

        public string ResolveLanguageCode()
        {
            if (localizationService != null && !string.IsNullOrEmpty(localizationService.CurrentLanguage))
            {
                return localizationService.CurrentLanguage.Trim().ToLowerInvariant();
            }

            if (ServiceLocator.TryGet<ILocalizationService>(out ILocalizationService loc) && loc != null &&
                !string.IsNullOrEmpty(loc.CurrentLanguage))
            {
                return loc.CurrentLanguage.Trim().ToLowerInvariant();
            }

            if (LocalizationService.Instance != null &&
                !string.IsNullOrEmpty(LocalizationService.Instance.CurrentLanguage))
            {
                return LocalizationService.Instance.CurrentLanguage.Trim().ToLowerInvariant();
            }

            Debug.LogWarning(
                "[OfficeFireLanguageResolver] LocalizationService / ILocalizationService not found — falling back to Turkish (tr).",
                this);
            return LocalizationService.Turkish;
        }
    }
}
