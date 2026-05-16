using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.PlayerLoop;
using Woi.InputSystem;

namespace FireExtinguisher.PC
{
    /// <summary>
    /// PC implementation of <see cref="ISprayInputProvider"/> using Unity's
    /// new Input System (<c>UnityEngine.InputSystem</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The spray action is configured as an inline <see cref="InputAction"/> serialized
    /// directly on this component. Click the field in the Inspector to change the binding
    /// (default: left mouse button). Any button, key, or gamepad trigger can be assigned
    /// without code changes.
    /// </para>
    /// <para>
    /// The action is enabled automatically in <c>OnEnable</c> and disabled in
    /// <c>OnDisable</c>. Do not enable it manually from outside this component.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Fire Extinguisher/PC/PC Spray Input Provider")]
    public sealed class PCSprayInputProvider : MonoBehaviour, ISprayInputProvider
    {
        // ── Inspector ─────────────────────────────────────────────────────────────
        [SerializeField] private GameplayInputContext inputContext; // veya inject et
        private IFireInputReader fireInputReader => inputContext;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        // ── ISprayInputProvider ───────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// Uses <see cref="InputAction.IsPressed()"/> — true every frame the
        /// action value exceeds the press threshold.
        /// </remarks>
        public bool IsSprayHeld => fireInputReader.IsFireHolding;

        /// <inheritdoc/>
        /// <remarks>
        /// Uses <see cref="InputAction.WasPressedThisFrame()"/> — true for exactly
        /// one frame when the action transitions from released to pressed.
        /// </remarks>
        public bool IsSprayStartedThisFrame => fireInputReader.IsFireStartedThisFrame;

        /// <inheritdoc/>
        /// <remarks>
        /// Uses <see cref="InputAction.WasReleasedThisFrame()"/> — true for exactly
        /// one frame when the action transitions from pressed to released.
        /// </remarks>
        public bool IsSprayStoppedThisFrame => fireInputReader.IsFireStoppedThisFrame;
    }
}
