using Obvious.Soap;

namespace Woi.InputSystem
{
    /// <summary>
    /// Scene objects that subscribe to <c>onLeanInput</c> via serialized Soap assets.
    /// <see cref="InputManager.SyncPcPlayerSoapEvents"/> rebinds them to the live context instance loaded via Addressables.
    /// </summary>
    public interface ISoapLeanInputListener
    {
        bool IsListeningToDifferentLeanEvent(ScriptableEventFloat liveLeanEvent);

        void RebindLeanInputEvent(ScriptableEventFloat liveLeanEvent);
    }
}
