using System;
using UnityEngine;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    [Serializable]
    public class KitchenCafePopupEvent : UnityEvent<KitchenCafePopupId> { }

    [Serializable]
    public class KitchenCafeVoiceEvent : UnityEvent<KitchenCafeVoiceId> { }

    [Serializable]
    public class KitchenCafeContentCueEvent : UnityEvent<KitchenCafeContentCueId> { }
}
