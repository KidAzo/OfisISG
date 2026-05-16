using System;
using UnityEngine;

namespace Woi.UI.Popups.Localization
{
    /// <summary>
    /// One language line for <see cref="LocalizedText"/>.
    /// Use ISO-style codes: en, tr, de, …
    /// </summary>
    [Serializable]
    public sealed class LocalizedStringEntry
    {
        [Tooltip("Language code, e.g. en, tr")]
        public string languageCode = "en";

        [TextArea(1, 4)]
        public string text;
    }
}
