using System;
using UnityEngine;

namespace Woi.OfficeFire
{
    [Serializable]
    public sealed class LocalizedPopupText
    {
        [TextArea]
        public string TurkishTitle;

        [TextArea]
        public string TurkishBody;

        [TextArea]
        public string EnglishTitle;

        [TextArea]
        public string EnglishBody;

        public string GetTitleForTurkish()
        {
            return TurkishTitle;
        }

        public string GetBodyForTurkish()
        {
            return TurkishBody;
        }

        public string GetTitleForEnglish()
        {
            return EnglishTitle;
        }

        public string GetBodyForEnglish()
        {
            return EnglishBody;
        }
    }

    [Serializable]
    public sealed class LocalizedVoiceClip
    {
        public AudioClip TurkishClip;
        public AudioClip EnglishClip;

        public AudioClip GetTurkishClip()
        {
            return TurkishClip;
        }

        public AudioClip GetEnglishClip()
        {
            return EnglishClip;
        }
    }

    [Serializable]
    public sealed class KitchenCafePopupEntry
    {
        public KitchenCafePopupId Id;
        public LocalizedPopupText Text;
    }

    [Serializable]
    public sealed class KitchenCafeVoiceEntry
    {
        public KitchenCafeVoiceId Id;
        public LocalizedVoiceClip Clip;
    }
}
