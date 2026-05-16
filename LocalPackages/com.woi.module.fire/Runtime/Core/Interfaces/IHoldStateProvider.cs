namespace FireExtinguisher.Core
{
    /// <summary>
    /// Reports whether the extinguisher is currently held, equipped,
    /// and in a state where spraying is physically possible.
    /// </summary>
    /// <remarks>
    /// This interface decouples the extinguisher controller from inventory,
    /// grab, or equip systems. On PC it may be driven by proximity or an
    /// equip key. In VR it is typically driven by an XRI grab interactable.
    /// The extinguisher controller gates all spray attempts behind
    /// <see cref="IsHeld"/>; if <c>false</c>, neither aim nor input
    /// is evaluated.
    /// </remarks>
    public interface IHoldStateProvider
    {
        /// <summary>
        /// Whether the extinguisher is currently held by the user and
        /// ready to operate. When <c>false</c>, the controller must
        /// ignore all spray input regardless of trigger state.
        /// </summary>
        bool IsHeld { get; }

        /// <summary>
        /// Whether the extinguisher transitioned to a held state this frame
        /// (rising edge). Use to trigger equip audio, animations, or UI.
        /// Must be <c>true</c> for exactly one frame on pickup.
        /// </summary>
        bool WasPickedUpThisFrame { get; }

        /// <summary>
        /// Whether the extinguisher transitioned out of a held state this frame
        /// (falling edge). Use to trigger drop audio, animations, or UI.
        /// Must be <c>true</c> for exactly one frame on release.
        /// </summary>
        bool WasDroppedThisFrame { get; }
    }
}
