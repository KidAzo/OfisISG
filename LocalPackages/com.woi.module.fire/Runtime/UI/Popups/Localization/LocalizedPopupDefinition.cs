using UnityEngine;
using Woi.UI.Popups;

namespace Woi.UI.Popups.Localization
{
    /// <summary>
    /// References two <see cref="PopupDefinition"/> assets (English / Turkish). At runtime the one matching
    /// <see cref="ILocalizationService.CurrentLanguage"/> is used; missing side falls back to the other.
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizedPopup", menuName = "Woi/UI/Localized Popup (EN + TR)", order = 2)]
    public sealed class LocalizedPopupDefinition : ScriptableObject
    {
        [Tooltip("Shown when Current Language is en (or non-tr fallback).")]
        public PopupDefinition english;

        [Tooltip("Shown when Current Language is tr.")]
        public PopupDefinition turkish;

        /// <summary>Resolved definition for the active UI language.</summary>
        public PopupDefinition ResolveForCurrentLanguage() =>
            LocalizedLanguageAssetResolver.Pick(english, turkish);
    }
}