namespace FireExtinguisher.Core
{
    /// <summary>
    /// Provides spray trigger state to the extinguisher system.
    /// Implement this interface to route spray input from any source:
    /// a keyboard key, a gamepad trigger, an XR controller button,
    /// an animation event, or an automated test.
    /// </summary>
    /// <remarks>
    /// This interface deliberately contains no Unity Input System types.
    /// The implementation is responsible for reading its own input source
    /// and exposing only the distilled boolean state.
    /// </remarks>
    public interface ISprayInputProvider
    {
        /// <summary>
        /// Whether the spray trigger is held down this frame.
        /// Remains <c>true</c> for every frame the trigger is held.
        /// </summary>
        bool IsSprayHeld { get; }

        /// <summary>
        /// Whether the spray trigger was pressed this frame (rising edge).
        /// Must be <c>true</c> for exactly one frame when the trigger transitions
        /// from released to held.
        /// </summary>
        bool IsSprayStartedThisFrame { get; }

        /// <summary>
        /// Whether the spray trigger was released this frame (falling edge).
        /// Must be <c>true</c> for exactly one frame when the trigger transitions
        /// from held to released.
        /// </summary>
        bool IsSprayStoppedThisFrame { get; }
    }
}
