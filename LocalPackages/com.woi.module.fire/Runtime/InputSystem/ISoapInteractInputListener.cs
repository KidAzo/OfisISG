using Obvious.Soap;

namespace Woi.InputSystem
{
    /// <summary>
    /// Scene objects that subscribe to <c>onInteractInput</c> (E / VR trigger) via serialized Soap assets.
    /// <see cref="InputManager.SyncPcPlayerSoapEvents"/> and <see cref="InputManager.SyncVrInteractSoapEvents"/>
    /// rebind them to the live context instance loaded via Addressables.
    /// </summary>
    public interface ISoapInteractInputListener
    {
        bool IsListeningToDifferentInteractEvent(ScriptableEventNoParam liveInteractEvent);

        void RebindInteractInputEvent(ScriptableEventNoParam liveInteractEvent);
    }
}
