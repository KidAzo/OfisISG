using System;
using System.Collections.Generic;
using UnityEngine;

namespace Woi.UI.Popups.Localization
{
    /// <summary>
    /// Serializable multi-language string for Inspector authoring.
    /// Resolution order is handled by <see cref="LocalizationService"/>.
    /// </summary>
    [Serializable]
    public sealed class LocalizedText
    {
        public List<LocalizedStringEntry> entries = new List<LocalizedStringEntry>();

        public static LocalizedText FromPair(string languageCode, string text)
        {
            var lt = new LocalizedText();
            lt.entries.Add(new LocalizedStringEntry { languageCode = languageCode, text = text });
            return lt;
        }

        /// <summary>Single English line convenience.</summary>
        public static LocalizedText English(string text) => FromPair("en", text);
    }
}
