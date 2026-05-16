using System;
using System.Collections.Generic;
using UnityEngine;

namespace Woi.UI.Popups.Localization
{
    /// <summary>
    /// One language row: title and message stay together (localized together).
    /// </summary>
    [Serializable]
    public sealed class PopupLocalizedLine
    {
        [Tooltip("ISO-style code: en, tr, … Use each language once per variant — duplicate codes in the same variant are ignored after the first row.")]
        public string languageCode = LocalizationService.English;

        public string title;

        [TextArea(2, 8)]
        public string message;
    }

    /// <summary>
    /// One text slot for the popup (e.g. one queued audio clip). Contains multiple
    /// <see cref="PopupLocalizedLine"/> rows — one per language.
    /// </summary>
    [Serializable]
    public sealed class PopupContentVariant
    {
        [Tooltip("One row per language (e.g. one en row + one tr row). Do not repeat the same language code here. For another clip in a queue, add another Content Variant below — not another line with the same code.")]
        public List<PopupLocalizedLine> lines = new List<PopupLocalizedLine>();
    }
}
