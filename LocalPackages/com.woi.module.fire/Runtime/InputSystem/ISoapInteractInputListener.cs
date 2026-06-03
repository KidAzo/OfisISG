using Obvious.Soap;

namespace Woi.InputSystem
{
    /// <summary>
    /// Scene objects that subscribe to <c>onInteractInput</c> (E) via serialized Soap assets.
    /// <see cref="InputManager.SyncPcPlayerSoapEvents"/> rebinds them to the live GameplayInputContext instance.
    /// </summary>
    public interface ISoapInteractInputListener
    {
        bool IsListeningToDifferentInteractEvent(ScriptableEventNoParam liveInteractEvent);

        void RebindInteractInputEvent(ScriptableEventNoParam liveInteractEvent);
    }
}
