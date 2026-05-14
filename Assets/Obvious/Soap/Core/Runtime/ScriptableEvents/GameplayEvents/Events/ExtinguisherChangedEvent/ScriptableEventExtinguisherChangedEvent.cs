using UnityEngine;
using Obvious.Soap;
using Woi.Events;

[CreateAssetMenu(fileName = "ScriptableEvent" + nameof(ExtinguisherChangedEvent), menuName = "Soap/ScriptableEvents/"+ nameof(ExtinguisherChangedEvent))]
public class ScriptableEventExtinguisherChangedEvent : ScriptableEvent<ExtinguisherChangedEvent>
{
    
}

