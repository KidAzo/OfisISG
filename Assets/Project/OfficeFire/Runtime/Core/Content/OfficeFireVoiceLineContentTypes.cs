using System;
using UnityEngine;
using WoiUtils.AudioSystem;

namespace Woi.OfficeFire
{
    [Serializable]
    public sealed class OfficeFireVoiceLinePopupText
    {
        [TextArea]
        public string TurkishTitle;

        [TextArea]
        public string TurkishBody;

        [TextArea]
        public string EnglishTitle;

        [TextArea]
        public string EnglishBody;
    }

    [Serializable]
    public sealed class OfficeFireVoiceLineEntry
    {
        public OfficeFireVoiceLineId Id;
        public OfficeFireVoiceLinePopupText Popup;

        [Tooltip("Woi Audio localized sound (EN + TR SoundDefinitions in one asset).")]
        public LocalizedSoundDefinition Voice;
    }
}
