using System;
using UnityEngine;

namespace Woi.OfficeFire
{
    [Serializable]
    public sealed class KitchenCafeContentCueEntry
    {
        public KitchenCafeContentCueId Id;

        [Header("Popup")]
        public KitchenCafePopupId PopupId;
        public PopupDurationMode PopupDurationMode = PopupDurationMode.UseVoiceClipLength;

        [Min(0f)]
        public float CustomPopupDuration = 3f;

        [Header("Voice")]
        public KitchenCafeVoiceId VoiceId;

        [Header("Behavior")]
        public bool StopPreviousVoice = true;
        public bool ClosePreviousPopup = true;
    }
}
