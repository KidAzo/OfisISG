using System;
using UnityEngine;

namespace FireExtinguisher.Core
{
    /// <summary>
    /// Represents a single hittable area within a <see cref="FireSource"/>.
    /// Attach this component to a child GameObject of a <see cref="FireSource"/>.
    /// A Collider on the same GameObject allows the spray evaluator to detect hits.
    /// </summary>
    /// <remarks>
    /// Suppression is applied by <see cref="FireSource"/> after it has validated
    /// compatibility and applied resistance scaling. Direct external calls to
    /// <see cref="ApplySuppression"/> bypass those checks and should be avoided
    /// in favour of routing through <see cref="FireSource.ReceiveSpray"/>.
    /// </remarks>
    [AddComponentMenu("Fire Extinguisher/Fire Target Zone")]
    public sealed class FireTargetZone : MonoBehaviour
    {
        [Header("Zone Identity")]
        [Tooltip("Describes the physical role of this zone within the fire.")]
        [SerializeField] private FireZoneType _zoneType = FireZoneType.Base;

        [Header("Suppression")]
        [Tooltip("Multiplier applied to incoming suppression for this zone. " +
                 "Values above 1 make the zone easier to suppress; below 1 make it harder.")]
        [SerializeField, Range(0f, 2f)] private float _effectMultiplier = 1f;

        [Header("Hit Evaluation")]
        [Tooltip("Half-angle tolerance in degrees. A spray ray whose angle offset from " +
                 "this zone's centre is within this value receives full coverage. " +
                 "Coverage falls off linearly beyond this angle up to the extinguisher cone limit.")]
        [SerializeField, Range(0f, 45f)] private float _angleTolerance = 15f;

        [Header("Initial State")]
        [Tooltip("Starting intensity as a fraction of the owner FireSource's MaxIntensity (0–1).")]
        [SerializeField, Range(0f, 1f)] private float _initialIntensityRatio = 1f;

        private float _maxIntensity;
        private float _currentIntensity;

        // ── Public state ────────────────────────────────────────────────────────

        /// <summary>The physical role this zone plays within its parent fire.</summary>
        public FireZoneType ZoneType => _zoneType;

        /// <summary>
        /// Multiplier applied to all incoming suppression.
        /// Determined by zone type and designer configuration.
        /// </summary>
        public float EffectMultiplier => _effectMultiplier;

        /// <summary>
        /// Half-angle tolerance in degrees used during spray hit evaluation.
        /// A spray ray within this angle of the zone centre receives full coverage.
        /// </summary>
        public float AngleTolerance => _angleTolerance;

        /// <summary>Absolute intensity value in the range [0, <see cref="MaxIntensity"/>].</summary>
        public float CurrentIntensity => _currentIntensity;

        /// <summary>Intensity normalised to the range [0, 1] relative to this zone's maximum.</summary>
        public float NormalizedIntensity => _maxIntensity > 0f ? _currentIntensity / _maxIntensity : 0f;

        /// <summary>Maximum intensity this zone was initialised with.</summary>
        public float MaxIntensity => _maxIntensity;

        /// <summary><c>true</c> when <see cref="CurrentIntensity"/> has reached zero.</summary>
        public bool IsExtinguished => _currentIntensity <= 0f;

        // ── Events ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised once when this zone's intensity reaches zero.
        /// The argument is this zone instance.
        /// </summary>
        public event Action<FireTargetZone> OnZoneExtinguished;

        /// <summary>
        /// Raised whenever this zone's intensity changes.
        /// Arguments are the zone instance and the new absolute intensity value.
        /// </summary>
        public event Action<FireTargetZone, float> OnIntensityChanged;

        // ── Internal lifecycle (called by FireSource only) ───────────────────────

        /// <summary>
        /// Initialises this zone to its starting intensity.
        /// Called by the owning <see cref="FireSource"/> during its <c>Awake</c>.
        /// </summary>
        /// <param name="maxIntensity">
        /// The maximum intensity value defined on the parent <see cref="FireData"/>.
        /// </param>
        internal void Initialise(float maxIntensity)
        {
            _maxIntensity = maxIntensity;
            _currentIntensity = _maxIntensity * _initialIntensityRatio;
        }

        /// <summary>
        /// Restores this zone to its initial state.
        /// Called by the owning <see cref="FireSource"/> when the fire is reset.
        /// </summary>
        /// <param name="maxIntensity">The maximum intensity to restore to.</param>
        internal void ResetZone(float maxIntensity)
        {
            bool wasExtinguished = IsExtinguished;
            Initialise(maxIntensity);

            if (wasExtinguished)
                OnIntensityChanged?.Invoke(this, _currentIntensity);
        }

        // ── Suppression ─────────────────────────────────────────────────────────

        /// <summary>
        /// Reduces this zone's intensity by the supplied amount scaled by
        /// <see cref="EffectMultiplier"/>.
        /// </summary>
        /// <remarks>
        /// Prefer routing suppression through <see cref="FireSource.ReceiveSpray"/>
        /// so that compatibility validation and resistance scaling are applied first.
        /// </remarks>
        /// <param name="amount">Raw suppression amount (before <see cref="EffectMultiplier"/>).</param>
        public void ApplySuppression(float amount)
        {
            if (IsExtinguished || amount <= 0f)
                return;

            float scaled = amount * _effectMultiplier;
            _currentIntensity = Mathf.Max(0f, _currentIntensity - scaled);

            OnIntensityChanged?.Invoke(this, _currentIntensity);

            if (_currentIntensity <= 0f)
                OnZoneExtinguished?.Invoke(this);
        }

        /// <summary>
        /// Increases this zone's intensity by <paramref name="amount"/>, capped at
        /// <see cref="MaxIntensity"/>. Called by <see cref="FireSource"/> when the
        /// intensification system is active.
        /// </summary>
        /// <param name="amount">Absolute intensity units to add.</param>
        internal void ApplyIntensification(float amount)
        {
            if (amount <= 0f || _maxIntensity <= 0f) return;

            float previous = _currentIntensity;
            _currentIntensity = Mathf.Min(_maxIntensity, _currentIntensity + amount);

            if (_currentIntensity > previous)
                OnIntensityChanged?.Invoke(this, _currentIntensity);
        }
    }
}
