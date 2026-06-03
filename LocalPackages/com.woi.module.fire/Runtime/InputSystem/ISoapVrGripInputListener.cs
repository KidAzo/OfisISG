using Obvious.Soap;

namespace Woi.InputSystem
{
    /// <summary>
    /// Scene objects that subscribe to <c>preOnGameFinishEvent</c> (VR right grip) via serialized Soap assets.
    /// <see cref="InputManager.SyncVrGripSoapEvents"/> rebinds them to the live <see cref="VrInputContext"/> instance.
    /// </summary>
    public interface ISoapVrGripInputListener
    {
        bool IsListeningToDifferentGripEvent(ScriptableEventNoParam liveGripEvent);

        void RebindGripInputEvent(ScriptableEventNoParam liveGripEvent);
    }
}
