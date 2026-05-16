using System;
using UnityEngine;
using Woi.UI.Popups.Localization;
using WoiUtils.AudioSystem;

namespace Woi.UI.Announcements
{
    /// <summary>
    /// Per-language title + message + optional hover loop/one-shot audio for world-space hover cards (fire extinguisher rows, etc.).
    /// </summary>
    [Serializable]
    public struct HoverInfoLanguageSlot
    {
        [Tooltip("Short label shown as popup title (e.g. tube type name).")]
        public string title;

        [TextArea(2, 10)]
        [Tooltip("Body text for this language.")]
        public string message;

        [Tooltip("Started when hover begins; stopped when the pointer leaves (assign looping or long clips as needed).")]
        public SoundDefinition sound;
    }

    [CreateAssetMenu(fileName = "LocalizedHoverInfo", menuName = "Woi/UI/Localized Hover Info (EN+TR+Extra)", order = 3)]
    public sealed class LocalizedHoverInfoDefinition : ScriptableObject
    {
        public HoverInfoLanguageSlot english;
        public HoverInfoLanguageSlot turkish;
        public HoverInfoLanguageSlot extra;

        [Tooltip("ISO code for the Extra column — add the same code to LocalizationService → Supported Language Codes (e.g. de, fr, ar).")]
        public string extraLanguageCode = "de";

        public HoverInfoLanguageSlot ResolveForCurrentLanguage() =>
            ResolveForCode(LocalizedLanguageAssetResolver.GetCurrentLanguageCode());

        public HoverInfoLanguageSlot ResolveForCode(string languageCode)
        {
            languageCode = languageCode?.Trim().ToLowerInvariant() ?? string.Empty;

            if (IsTurkish(languageCode))
                return FirstWithContent(turkish, english, extra);

            string ex = string.IsNullOrWhiteSpace(extraLanguageCode)
                ? string.Empty
                : extraLanguageCode.Trim().ToLowerInvariant();

            if (!string.IsNullOrEmpty(ex) && string.Equals(languageCode, ex, StringComparison.Ordinal))
                return FirstWithContent(extra, english, turkish);

            return FirstWithContent(english, turkish, extra);
        }

        static bool IsTurkish(string code) =>
            string.Equals(code, LocalizationService.Turkish, StringComparison.OrdinalIgnoreCase);

        static HoverInfoLanguageSlot FirstWithContent(HoverInfoLanguageSlot a, HoverInfoLanguageSlot b, HoverInfoLanguageSlot c)
        {
            if (HasContent(a)) return a;
            if (HasContent(b)) return b;
            if (HasContent(c)) return c;
            return a;
        }

        static bool HasContent(HoverInfoLanguageSlot s) =>
            !string.IsNullOrWhiteSpace(s.title)
            || !string.IsNullOrWhiteSpace(s.message)
            || s.sound != null;
    }
}
