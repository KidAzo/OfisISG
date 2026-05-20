using System;
using UnityEngine;

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
    public sealed class OfficeFireVoiceLineAudioClip
    {
        public AudioClip TurkishClip;
        public AudioClip EnglishClip;
    }

    [Serializable]
    public sealed class OfficeFireVoiceLineEntry
    {
        public OfficeFireVoiceLineId Id;
        public OfficeFireVoiceLinePopupText Popup;
        public OfficeFireVoiceLineAudioClip Voice;
    }
}
