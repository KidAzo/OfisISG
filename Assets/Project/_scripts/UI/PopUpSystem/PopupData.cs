using UnityEngine;
using Woi.Events;

namespace Woi.PopUpSystem
{
    [CreateAssetMenu(fileName = "PopupData", menuName = "SO/Popup/Popup Data")]
    public class PopupData : ScriptableObject
    {
        public string title;
        [TextArea] public string message;
        public PopupTriggerType triggerType;
        public bool isHazard = false;

        [Header("Trigger Settings")] public float proximityDistance = 5f; // OnPlayerProximity için
        public float delayTime = 2f; // OnTimer için
        public string eventName; // OnEvent için

        [Header("Duration")] public bool autoClose = false;
        public float displayDuration = 3f;
    }
}