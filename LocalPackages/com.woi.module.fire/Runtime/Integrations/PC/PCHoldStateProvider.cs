using FireExtinguisher.Core;
using UnityEngine;

namespace FireExtinguisher.PC
{
    /// <summary>
    /// PC implementation of <see cref="IHoldStateProvider"/>.
    /// Tracks whether the extinguisher is currently equipped via a simple boolean
    /// that can be toggled from the Inspector or via the <see cref="Equip"/> and
    /// <see cref="Unequip"/> methods.
    /// </summary>
    /// <remarks>
    /// On PC, "holding" is typically determined by an equip/hotbar system rather
    /// than a physical grab. Wire <see cref="Equip"/> and <see cref="Unequip"/> to
    /// whatever inventory or proximity system your project uses. If the extinguisher
    /// starts equipped, enable <see cref="_equippedAtStart"/>.
    /// </remarks>
    [AddComponentMenu("Fire Extinguisher/PC/PC Hold State Provider")]
    public sealed class PCHoldStateProvider : MonoBehaviour, IHoldStateProvider
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Tooltip("Whether the extinguisher is considered held/equipped when the scene starts.")]
        [SerializeField] private bool _equippedAtStart = true;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private bool _isHeld;
        private bool _wasHeldLastFrame;

        // ── IHoldStateProvider ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public bool IsHeld => _isHeld;

        /// <inheritdoc/>
        /// <remarks>
        /// <c>true</c> for the one frame in which <see cref="IsHeld"/> transitioned
        /// from <c>false</c> to <c>true</c>.
        /// </remarks>
        public bool WasPickedUpThisFrame => _isHeld && !_wasHeldLastFrame;

        /// <inheritdoc/>
        /// <remarks>
        /// <c>true</c> for the one frame in which <see cref="IsHeld"/> transitioned
        /// from <c>true</c> to <c>false</c>.
        /// </remarks>
        public bool WasDroppedThisFrame => !_isHeld && _wasHeldLastFrame;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _isHeld = _equippedAtStart;
            _wasHeldLastFrame = _isHeld;
        }

        private void LateUpdate()
        {
            // Snapshot the current state after all Update() calls for this frame
            // so that WasPickedUpThisFrame / WasDroppedThisFrame remain accurate
            // for the entire frame in which Equip / Unequip was called.
            _wasHeldLastFrame = _isHeld;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Marks the extinguisher as equipped and held.
        /// <see cref="WasPickedUpThisFrame"/> will be <c>true</c> for the remainder
        /// of the current frame.
        /// </summary>
        public void Equip()
        {
            _isHeld = true;
        }

        /// <summary>
        /// Marks the extinguisher as unequipped and not held.
        /// <see cref="WasDroppedThisFrame"/> will be <c>true</c> for the remainder
        /// of the current frame.
        /// </summary>
        public void Unequip()
        {
            _isHeld = false;
        }

        /// <summary>
        /// Toggles the held state. Calls <see cref="Equip"/> or <see cref="Unequip"/>
        /// depending on the current state.
        /// </summary>
        public void Toggle()
        {
            if (_isHeld) Unequip();
            else         Equip();
        }
    }
}
