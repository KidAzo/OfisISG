using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireExtinguisher.Core
{
    /// <summary>
    /// Manages the lifecycle and intensity of a fire, composed of one or more
    /// <see cref="FireTargetZone"/> child components.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This component is the authoritative owner of fire state. All suppression
    /// must flow through <see cref="ReceiveSpray"/> so that compatibility checks
    /// and resistance scaling are consistently applied.
    /// </para>
    /// <para>
    /// Add <see cref="FireTargetZone"/> components to child GameObjects and either
    /// assign them manually in the Inspector or leave the list empty to let this
    /// component collect all children automatically on <c>Awake</c>.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Fire Extinguisher/Fire Source")]
    public sealed class FireSource : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────────

        [Header("Configuration")]
        [Tooltip("ScriptableObject that defines this fire's class, intensity, and compatibility rules.")]
        [SerializeField] private FireData _fireData;

        [Header("Zones")]
        [Tooltip("Target zones that make up this fire. Leave empty to auto-collect all " +
                 "FireTargetZone components found in immediate children at Awake.")]
        [SerializeField] private List<FireTargetZone> _zones = new List<FireTargetZone>();

        [Header("State Thresholds")]
        [Tooltip("Normalised intensity (0–1) below which the fire transitions to the " +
                 "Suppressed state. Must be greater than zero.")]
        [SerializeField, Range(0.01f, 0.5f)] private float _suppressedThreshold = 0.2f;

        [Header("Intensification")]
        [Tooltip("When enabled, the fire slowly grows in intensity while it is not being actively suppressed.")]
        [SerializeField] private bool _intensificationEnabled = false;

        [Tooltip("Intensity gained per second per zone as a fraction of MaxIntensity. " +
                 "Applied while the fire is Active or Suppressed and no spray has landed " +
                 "within the delay window.")]
        [SerializeField, Range(0f, 0.5f)] private float _intensificationRate = 0.02f;

        [Tooltip("Seconds without any effective spray before intensification kicks in.")]
        [SerializeField, Min(0f)] private float _intensificationDelay = 3f;

        [Header("Reignition")]
        [Tooltip("Whether this fire can reignite after being fully extinguished.")]
        [SerializeField] private bool _reignitionEnabled = false;

        [Tooltip("Seconds after extinguishment before reignition begins.")]
        [SerializeField, Min(0f)] private float _reignitionDelay = 30f;

        [Tooltip("Intensity per second restored during reignition (fraction of MaxIntensity).")]
        [SerializeField, Range(0f, 1f)] private float _reignitionRate = 0.05f;

        // ── Private state ────────────────────────────────────────────────────────

        private FireSourceState _state = FireSourceState.Active;
        private float _reignitionTimer;
        private bool _reignitionCountdownActive;
        // Starts at MaxValue so intensification begins immediately if enabled and no spray has landed.
        private float _timeSinceLastSpray = float.MaxValue;

        // ── Public read-only state ───────────────────────────────────────────────

        /// <summary>The configuration asset that defines this fire's properties.</summary>
        public FireData Data => _fireData;

        /// <summary>Current lifecycle state of this fire source.</summary>
        public FireSourceState State => _state;

        /// <summary>
        /// Read-only view of the zones that compose this fire.
        /// Do not modify zone state directly; use <see cref="ReceiveSpray"/> instead.
        /// </summary>
        public IReadOnlyList<FireTargetZone> Zones => _zones;

        /// <summary>
        /// Average normalised intensity across all zones, in the range [0, 1].
        /// Returns 0 when there are no zones.
        /// </summary>
        public float CurrentNormalizedIntensity
        {
            get
            {
                if (_zones.Count == 0) return 0f;

                float total = 0f;
                foreach (FireTargetZone zone in _zones)
                    total += zone.NormalizedIntensity;

                return total / _zones.Count;
            }
        }

        /// <summary><c>true</c> when all zones are extinguished.</summary>
        public bool IsExtinguished => _state == FireSourceState.Extinguished;

        // ── Events ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised whenever the <see cref="FireSourceState"/> changes.
        /// The argument is the new state.
        /// </summary>
        public event Action<FireSourceState> OnStateChanged;

        /// <summary>
        /// Raised each time the aggregate normalised intensity changes.
        /// The argument is the new normalised intensity (0–1).
        /// </summary>
        public event Action<float> OnIntensityChanged;

        /// <summary>Raised once when all zones reach zero intensity.</summary>
        public event Action OnFullyExtinguished;

        /// <summary>
        /// Raised when a previously extinguished fire begins to reignite.
        /// Only raised when <c>reignitionEnabled</c> is <c>true</c>.
        /// </summary>
        public event Action OnReignited;

        // ── Unity lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (_zones.Count == 0)
                GetComponentsInChildren(true, _zones);

            if (_fireData == null)
            {
                Debug.LogWarning($"[FireSource] '{name}' has no FireData assigned. " +
                                 "Compatibility checks will always return Neutral.", this);
            }

            InitialiseZones();
        }

        private void OnEnable()
        {
            foreach (FireTargetZone zone in _zones)
            {
                zone.OnIntensityChanged  += HandleZoneIntensityChanged;
                zone.OnZoneExtinguished  += HandleZoneExtinguished;
            }
        }

        private void OnDisable()
        {
            foreach (FireTargetZone zone in _zones)
            {
                zone.OnIntensityChanged  -= HandleZoneIntensityChanged;
                zone.OnZoneExtinguished  -= HandleZoneExtinguished;
            }
        }

        private void Update()
        {
            if (_intensificationEnabled)
            {
                _timeSinceLastSpray += Time.deltaTime;

                bool canIntensify = _state == FireSourceState.Active
                                 || _state == FireSourceState.Suppressed;

                if (canIntensify && _timeSinceLastSpray >= _intensificationDelay)
                    ApplyIntensification();
            }

            if (!_reignitionEnabled)
                return;

            if (_reignitionCountdownActive)
            {
                _reignitionTimer -= Time.deltaTime;
                if (_reignitionTimer <= 0f)
                {
                    _reignitionCountdownActive = false;
                    TransitionTo(FireSourceState.Reigniting);
                    OnReignited?.Invoke();
                }
                return;
            }

            if (_state == FireSourceState.Reigniting)
                ApplyReignition();
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Checks whether the supplied extinguisher type is compatible with this fire.
        /// </summary>
        /// <param name="extinguisherType">The agent type to evaluate.</param>
        /// <returns>
        /// <see cref="CompatibilityResult.Effective"/> if allowed,
        /// <see cref="CompatibilityResult.Forbidden"/> if forbidden,
        /// or <see cref="CompatibilityResult.Neutral"/> otherwise.
        /// Returns <see cref="CompatibilityResult.Neutral"/> when no <see cref="FireData"/> is assigned.
        /// </returns>
        public CompatibilityResult CheckCompatibility(ExtinguisherType extinguisherType)
        {
            if (_fireData == null)
                return CompatibilityResult.Neutral;

            foreach (ExtinguisherType forbidden in _fireData.ForbiddenExtinguisherTypes)
                if (forbidden == extinguisherType) return CompatibilityResult.Forbidden;

            foreach (ExtinguisherType allowed in _fireData.AllowedExtinguisherTypes)
                if (allowed == extinguisherType) return CompatibilityResult.Effective;

            return CompatibilityResult.Neutral;
        }

        /// <summary>
        /// Applies extinguishing suppression to a specific zone after validating
        /// compatibility and scaling by the fire's resistance.
        /// </summary>
        /// <remarks>
        /// This is the primary entry point for the spray evaluator.
        /// Suppression is silently ignored when:
        /// <list type="bullet">
        ///   <item>The fire is already extinguished.</item>
        ///   <item>The extinguisher type is <see cref="CompatibilityResult.Forbidden"/>.</item>
        ///   <item><paramref name="zone"/> is not registered with this source.</item>
        /// </list>
        /// </remarks>
        /// <param name="extinguisherType">Agent type being applied.</param>
        /// <param name="zone">The specific zone that was hit.</param>
        /// <param name="rawSuppression">
        /// Base suppression amount before resistance scaling.
        /// Typically derived from <c>ExtinguisherData.ExtinguishPower * deltaTime * coverage</c>.
        /// </param>
        public void ReceiveSpray(ExtinguisherType extinguisherType, FireTargetZone zone, float rawSuppression)
        {
            if (_state == FireSourceState.Extinguished) return;
            if (zone == null || !_zones.Contains(zone)) return;
            if (rawSuppression <= 0f) return;

            var gate = GetComponent<FireExtinguishPrerequisiteGate>();
            if (gate != null && !gate.CanExtinguish)
            {
                return;
            }

            // Strict matching: only extinguisher types explicitly listed as Effective
            // are allowed to suppress this fire. Neutral (unlisted) and Forbidden types
            // are both blocked here as a safety net; the primary check happens upstream
            // in ExtinguishEvaluator via ExtinguisherData.CanExtinguish().
            CompatibilityResult compatibility = CheckCompatibility(extinguisherType);
            if (compatibility != CompatibilityResult.Effective) return;

            _timeSinceLastSpray = 0f;

            float resistance = (_fireData != null) ? _fireData.ExtinguishResistance : 1f;
            float effectiveSuppression = rawSuppression / Mathf.Max(0.01f, resistance);

            zone.ApplySuppression(effectiveSuppression);
        }

        /// <summary>
        /// Resets all zones to their initial intensity and restores the fire to
        /// <see cref="FireSourceState.Active"/>.
        /// </summary>
        public void ResetFire()
        {
            _reignitionCountdownActive = false;
            _reignitionTimer = 0f;
            _timeSinceLastSpray = float.MaxValue;

            InitialiseZones();
            TransitionTo(FireSourceState.Active);
        }

        /// <summary>
        /// Marks recent external suppression (blanket, electrical safety, etc.) so intensification does not
        /// counteract gradual drain while <see cref="_intensificationEnabled"/> is on.
        /// </summary>
        public void NotifyExternalSuppressionTick() => _timeSinceLastSpray = 0f;

        /// <summary>
        /// Drains every active zone over <paramref name="remainingDurationSeconds"/> (wall-clock).
        /// Call once per frame from a coroutine. Returns <c>false</c> when no active zones remain.
        /// </summary>
        public bool ApplyGradualSuppressionStep(float remainingDurationSeconds, float deltaTime)
        {
            if (_state == FireSourceState.Extinguished || remainingDurationSeconds <= 0f || deltaTime <= 0f)
                return false;

            NotifyExternalSuppressionTick();

            bool anyActive = false;
            float stepWindow = Mathf.Max(remainingDurationSeconds, deltaTime);

            foreach (FireTargetZone zone in _zones)
            {
                if (zone == null || zone.IsExtinguished)
                    continue;

                anyActive = true;
                float drain = (zone.CurrentIntensity / stepWindow) * deltaTime;
                zone.ApplySuppression(drain);
            }

            return anyActive;
        }

        /// <summary>Forces all zones to zero intensity immediately (e.g. end of timed blanket extinguish).</summary>
        public void ForceExtinguishAllZones()
        {
            foreach (FireTargetZone zone in _zones)
            {
                if (zone != null && !zone.IsExtinguished)
                    zone.ApplySuppression(zone.CurrentIntensity);
            }
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private void InitialiseZones()
        {
            float maxIntensity = _fireData != null ? _fireData.MaxIntensity : 1f;
            foreach (FireTargetZone zone in _zones)
                zone.Initialise(maxIntensity);
        }

        private void HandleZoneIntensityChanged(FireTargetZone zone, float newIntensity)
        {
            _ = zone;
            _ = newIntensity;

            OnIntensityChanged?.Invoke(CurrentNormalizedIntensity);
            EvaluateState();
        }

        private void HandleZoneExtinguished(FireTargetZone zone)
        {
            _ = zone;
            EvaluateState();
        }

        private void EvaluateState()
        {
            float normalized = CurrentNormalizedIntensity;

            if (normalized <= 0f)
            {
                if (_state != FireSourceState.Extinguished)
                {
                    TransitionTo(FireSourceState.Extinguished);
                    OnFullyExtinguished?.Invoke();

                    if (_reignitionEnabled)
                    {
                        _reignitionTimer = _reignitionDelay;
                        _reignitionCountdownActive = true;
                    }
                }
                return;
            }

            FireSourceState target = normalized <= _suppressedThreshold
                ? FireSourceState.Suppressed
                : FireSourceState.Active;

            TransitionTo(target);
        }

        private void ApplyIntensification()
        {
            float maxIntensity = _fireData != null ? _fireData.MaxIntensity : 1f;
            float gainPerZone  = maxIntensity * _intensificationRate * Time.deltaTime;

            foreach (FireTargetZone zone in _zones)
                zone.ApplyIntensification(gainPerZone);
        }

        private void ApplyReignition()
        {
            float maxIntensity = _fireData != null ? _fireData.MaxIntensity : 1f;
            float gainPerZone = maxIntensity * _reignitionRate * Time.deltaTime;

            foreach (FireTargetZone zone in _zones)
            {
                if (zone.IsExtinguished)
                    zone.ResetZone(gainPerZone);
            }

            if (CurrentNormalizedIntensity >= _suppressedThreshold)
                TransitionTo(FireSourceState.Active);
        }

        private void TransitionTo(FireSourceState newState)
        {
            if (_state == newState) return;

            _state = newState;
            OnStateChanged?.Invoke(_state);
        }
    }
}
