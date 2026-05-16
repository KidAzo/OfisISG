using System;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    [Serializable]
    public class ObjectiveChangedEvent : UnityEvent<OfficeFireObjectiveId> { }

    [Serializable]
    public class VoiceLineEvent : UnityEvent<OfficeFireVoiceLineId> { }

    [Serializable]
    public class CorrectActionEvent : UnityEvent<OfficeFireCorrectActionId> { }

    [Serializable]
    public class MistakeEvent : UnityEvent<OfficeFireMistakeId> { }
}
