using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;
using Woi.Events;

[AddComponentMenu("Soap/EventListeners/EventListener"+nameof(ExtinguisherChangedEvent))]
public class EventListenerExtinguisherChangedEvent : EventListenerGeneric<ExtinguisherChangedEvent>
{
    [SerializeField] private EventResponse[] _eventResponses = null;
    protected override EventResponse<ExtinguisherChangedEvent>[] EventResponses => _eventResponses;

    [System.Serializable]
    public class EventResponse : EventResponse<ExtinguisherChangedEvent>
    {
        [SerializeField] private ScriptableEventExtinguisherChangedEvent _scriptableEvent = null;
        public override ScriptableEvent<ExtinguisherChangedEvent> ScriptableEvent => _scriptableEvent;

        [SerializeField] private ExtinguisherChangedEventUnityEvent _response = null;
        public override UnityEvent<ExtinguisherChangedEvent> Response => _response;
    }

    [System.Serializable]
    public class ExtinguisherChangedEventUnityEvent : UnityEvent<ExtinguisherChangedEvent>
    {
        
    }
}
