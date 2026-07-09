using System;
using Obvious.Soap;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace FireExtinguisher.Core
{
    /// <summary>
    /// Central runtime controller for a fire extinguisher.
    /// Integrates the three provider interfaces with the <see cref="ExtinguishEvaluator"/>
    /// and manages the agent capacity over time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Assign the three provider components (<see cref="IAimProvider"/>,
    /// <see cref="ISprayInputProvider"/>, <see cref="IHoldStateProvider"/>) via the
    /// Inspector fields. Each must be a <see cref="MonoBehaviour"/> on the same or a
    /// nearby GameObject that implements the respective interface.
    /// </para>
    /// <para>
    /// The controller does not read input, apply VFX, play audio, or contain any
    /// training or scoring logic. It raises events so downstream systems can react
    /// without coupling to this component.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Fire Extinguisher/Extinguisher Controller")]
    public sealed class ExtinguisherController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Configuration")]
        [Tooltip("ScriptableObject defining this extinguisher's agent type, spray geometry, " +
                 "extinguish power, and consumption rate.")]
        [SerializeField] private ExtinguisherData _extinguisherData;

        [Header("Capacity")]
        [Tooltip("Total agent charge. Displayed as the '100%' reference value. " +
                 "Drain speed is controlled by ExtinguisherData.ConsumptionRate.")]
        [SerializeField, Min(0.1f)] private float _maxCapacity = 20f;

        [Header("Spray world origin")]
        [Tooltip("When set, the SphereCast spray uses this transform's world position as origin instead of " +
                 "IAimProvider.SprayOrigin. Direction and cone checks still come from the aim provider. " +
                 "Use to anchor the nozzle to a child bone or offset object while the controller lives on the root.")]
        [SerializeField] private Transform _sprayWorldOriginTransform;

        [Header("Providers")]
        [Tooltip("MonoBehaviour that implements IAimProvider. " +
                 "Supplies spray origin, direction, and aim validity each frame.")]
        [SerializeField] private MonoBehaviour _aimProviderSource;

        [Tooltip("MonoBehaviour that implements ISprayInputProvider. " +
                 "Supplies trigger held/started/stopped state each frame.")]
        [SerializeField] private MonoBehaviour _sprayInputProviderSource;

        [Tooltip("MonoBehaviour that implements IHoldStateProvider. " +
                 "Supplies whether the extinguisher is currently held and usable.")]
        [SerializeField] private MonoBehaviour _holdStateProviderSource;

        [Header("Evaluation")]
        [Tooltip("Evaluator component that performs the SphereCast and returns an ExtinguishResult. " +
                 "Must be on this GameObject or a child.")]
        [SerializeField] private ExtinguishEvaluator _evaluator;

        [Header("SO Events")]
        [Tooltip("Raised whenever the normalized capacity changes. Payload: capacity as integer 0–100.")]
        [SerializeField] private ScriptableEventInt _onCapacityChangedEvent;

        [Tooltip("Raised each discharge tick while spray effectively hits a fire zone with non-zero " +
                 "suppression (after evaluation). Hook audio, scoring, or UI feedback here.")]
        [SerializeField] private ScriptableEventNoParam _onEstinguishing;

        [Tooltip("Raised once per refill cycle when charge hits zero while spraying, or on the first spray press when already empty.")]
        [SerializeField] private ScriptableEventNoParam _onExtinguisherDepletedEvent;

        [Tooltip("Raised once per refill cycle when charge hits zero while spraying, or on the first spray press when already empty.")]
        [SerializeField] private UnityEvent _onExtinguisherDepletedUnity = new UnityEvent();

        private IAimProvider        _aimProvider;
        private ISprayInputProvider _sprayInputProvider;
        private IHoldStateProvider  _holdStateProvider;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private float _normalizedCapacity = 1f;
        private bool  _isDischarging;

        /// <summary>
        /// After <see cref="RaiseDepletedFeedbackOnce"/> runs, blocks repeats until capacity is refilled (&gt; 0).
        /// Covers both "drained to zero while spraying" and "already empty, first squeeze" cases.
        /// </summary>
        private bool _depletedFeedbackRaisedUntilRefill;

        /// <summary>
        /// Safety pin must be pulled before discharge is allowed. Reset when the extinguisher
        /// is returned to the world (see <see cref="ResetAfterWorldDrop"/>).
        /// </summary>
        private bool _pinPulled;

        /// <summary>
        /// XR: hose spline mesh stays hidden after pin until nozzle proximity snap; see <c>ViewmodelHoseSplinePinVisualGate</c>. PC ignores.
        /// </summary>
        bool _vrHoseSplineVisualReady;

        private Vector3 _resolvedSprayWorldOrigin;
        private Vector3 _resolvedSprayWorldDirection;

        // ── Public read-only state ────────────────────────────────────────────────

        /// <summary>The configuration asset driving this extinguisher's behaviour.</summary>
        public ExtinguisherData ExtinguisherData => _extinguisherData;

        /// <summary>Whether the extinguisher is currently discharging agent.</summary>
        public bool IsDischarging => _isDischarging;

        /// <summary>Whether the agent charge has been fully depleted.</summary>
        public bool IsDepleted => _normalizedCapacity <= 0f;

        /// <summary>
        /// Remaining charge as a fraction of <see cref="MaxCapacity"/>, in the range [0, 1].
        /// </summary>
        public float NormalizedCapacity => _normalizedCapacity;

        /// <summary>
        /// Remaining charge in the same units as <see cref="MaxCapacity"/>.
        /// </summary>
        public float RemainingCapacity => _normalizedCapacity * _maxCapacity;

        /// <summary>Total agent charge this extinguisher started with.</summary>
        public float MaxCapacity => _maxCapacity;

        /// <summary>
        /// Whether the safety pin has been pulled this equip cycle. Spray is blocked until <c>true</c>.
        /// </summary>
        public bool IsPinPulled => _pinPulled;

        /// <summary>
        /// XR port: spline hose may show only after this becomes <c>true</c> (nozzle hand snap). PC port does not use this flag.
        /// </summary>
        public bool IsVrHoseSplineVisualReady => _vrHoseSplineVisualReady;

        /// <summary>
        /// XR: set by VR nozzle snap / restore. PC builds ignore.
        /// </summary>
        public void SetVrHoseSplineVisualReady(bool ready) => _vrHoseSplineVisualReady = ready;

        /// <summary>
        /// World-space origin passed to <see cref="ExtinguishEvaluator.Evaluate"/> for the spray pair
        /// (after optional spray world origin transform override). Updated each <c>Update</c>.
        /// </summary>
        public Vector3 ResolvedSprayWorldOrigin => _resolvedSprayWorldOrigin;

        /// <summary>
        /// World-space spray direction from <see cref="IAimProvider"/> (same frame as <see cref="ResolvedSprayWorldOrigin"/>).
        /// </summary>
        public Vector3 ResolvedSprayWorldDirection => _resolvedSprayWorldDirection;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised each discharge tick when the normalized capacity changes.
        /// The argument is the new normalized capacity in [0, 1].
        /// Also raised when capacity is restored via <see cref="ResetCapacity"/>.
        /// </summary>
        public event Action<float> OnCapacityChanged;

        /// <summary>
        /// Raised once per refill cycle when the charge reaches zero: either while spraying (drain)
        /// or on the first spray press when already empty (pin pulled, held, valid setup).
        /// </summary>
        public event Action OnExtinguisherDepleted;

        /// <summary>Inspector / UnityEvent hook for <see cref="OnExtinguisherDepleted"/>.</summary>
        public UnityEvent OnExtinguisherDepletedUnity => _onExtinguisherDepletedUnity;

        /// <summary>
        /// Raised on the first frame a valid spray discharge begins.
        /// </summary>
        public event Action OnSprayStarted;

        /// <summary>
        /// Raised on the frame a discharge ends, whether due to trigger release,
        /// loss of hold, lost aim validity, or depletion.
        /// </summary>
        public event Action OnSprayStopped;

        /// <summary>
        /// Raised once when the safety pin transitions from unpulled to pulled.
        /// </summary>
        public event Action OnPinPulled;

        /// <summary>
        /// Raised each discharge tick after the <see cref="ExtinguishEvaluator"/> runs.
        /// The argument contains all hit, angle, distance, and suppression data for the tick.
        /// Subscribe here to build VFX, audio, or training logic on top of the framework.
        /// </summary>
        public event Action<ExtinguishResult> OnSprayEvaluated;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private Transform _originalSprayWorldOrigin;

        private void Awake()
        {
            _originalSprayWorldOrigin = _sprayWorldOriginTransform;
            ResolveProviders();

            _normalizedCapacity = 1f;

            if (_extinguisherData == null)
                Debug.LogWarning("[ExtinguisherController] No ExtinguisherData assigned.", this);

            if (_evaluator == null)
                _evaluator = GetComponentInChildren<ExtinguishEvaluator>();

            if (_evaluator == null)
                Debug.LogWarning("[ExtinguisherController] No ExtinguishEvaluator found. " +
                                 "Spray will not be evaluated.", this);

            ComputeResolvedSprayPose(out _resolvedSprayWorldOrigin, out _resolvedSprayWorldDirection);
        }

        private void Update()
        {
            ComputeResolvedSprayPose(out _resolvedSprayWorldOrigin, out _resolvedSprayWorldDirection);

            // Stop any active discharge if the extinguisher is no longer held.
            if (_holdStateProvider != null && _holdStateProvider.WasDroppedThisFrame)
            {
                if (_isDischarging)
                    StopDischarge();
                return;
            }

            TryRaiseDepletedOnFirstEmptySprayAttempt();

            if (!CanBeginOrContinueSpraying())
            {
                if (_isDischarging)
                    StopDischarge();
                return;
            }

            // Spray start — rising edge from the input provider.
            if (_sprayInputProvider.IsSprayStartedThisFrame)
                BeginDischarge();

            // Continuous discharge tick (must run before spray-stop handling this frame).
            // Otherwise a same-frame press+release never evaluates — VFX that only listen to
            // OnSprayEvaluated would never see a tick and stream/impact state desynchronises.
            if (_isDischarging)
                TickDischarge(Time.deltaTime);

            // Spray stop — falling edge from the input provider.
            if (_sprayInputProvider.IsSprayStoppedThisFrame && _isDischarging)
                StopDischarge();
        }

        // ── VR Support ────────────────────────────────────────────────────────────

        public void SetVRNozzle(Transform vrNozzle)
        {
            if (vrNozzle != null)
                _sprayWorldOriginTransform = vrNozzle;
        }

        public void RestoreOriginalNozzle()
        {
            _sprayWorldOriginTransform = _originalSprayWorldOrigin;
            _vrHoseSplineVisualReady = false;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Restores the agent charge to full capacity.
        /// </summary>
        
        [Button]
        public void ResetCapacity()
        {
            SetNormalizedCapacity(1f);
        }

        /// <summary>
        /// Sets the agent charge to a specific normalized value.
        /// </summary>
        /// <param name="normalizedAmount">
        /// Target charge as a fraction of <see cref="MaxCapacity"/>, clamped to [0, 1].
        /// </param>
        public void ResetCapacity(float normalizedAmount)
        {
            SetNormalizedCapacity(Mathf.Clamp01(normalizedAmount));
        }

        /// <summary>
        /// Pulls the safety pin so discharge can begin when the player holds the spray input.
        /// Idempotent — returns <c>false</c> when the pin was already pulled.
        /// </summary>
        /// <returns><c>true</c> if this call transitioned the pin from unpulled to pulled.</returns>
        public bool PullPin()
        {
            if (_pinPulled)
                return false;

            _pinPulled = true;
            _vrHoseSplineVisualReady = false;
            OnPinPulled?.Invoke();
            return true;
        }

        /// <summary>
        /// Called when the extinguisher is dropped back into the world. Stops any active spray,
        /// clears the pin state, and refills agent to full capacity so the next pickup starts fresh.
        /// </summary>
        public void ResetAfterWorldDrop()
        {
            if (_isDischarging)
                StopDischarge();

            _pinPulled = false;
            _vrHoseSplineVisualReady = false;
            ResetCapacity();
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private void ResolveProviders()
        {
            _aimProvider = ResolveInterface<IAimProvider>(
                _aimProviderSource, nameof(IAimProvider));

            _sprayInputProvider = ResolveInterface<ISprayInputProvider>(
                _sprayInputProviderSource, nameof(ISprayInputProvider));

            _holdStateProvider = ResolveInterface<IHoldStateProvider>(
                _holdStateProviderSource, nameof(IHoldStateProvider));
        }

        private T ResolveInterface<T>(MonoBehaviour source, string interfaceName) where T : class
        {
            if (source == null)
            {
                Debug.LogWarning(
                    $"[ExtinguisherController] No source assigned for {interfaceName}.", this);
                return null;
            }

            T resolved = source as T;
            if (resolved == null)
                Debug.LogError(
                    $"[ExtinguisherController] '{source.name}' does not implement {interfaceName}.", this);

            return resolved;
        }

        /// <summary>
        /// Returns true when all conditions for spraying are satisfied this frame.
        /// Does not consume any state — safe to call multiple times per frame.
        /// </summary>
        private bool CanBeginOrContinueSpraying()
        {
            if (!_pinPulled)                                         return false;
            if (IsDepleted)                                          return false;
            if (_extinguisherData == null)                           return false;
            if (_aimProvider == null)    return false;
            if (_holdStateProvider == null || !_holdStateProvider.IsHeld) return false;
            if (_sprayInputProvider == null)                         return false;
            return true;
        }

        private void BeginDischarge()
        {
            if (_isDischarging) return;

            _isDischarging = true;
            OnSprayStarted?.Invoke();
        }

        private void StopDischarge()
        {
            if (!_isDischarging) return;

            _isDischarging = false;
            OnSprayStopped?.Invoke();
        }

        /// <summary>Ends active spray immediately (training results / session end).</summary>
        public void StopDischargeIfActive()
        {
            if (_isDischarging)
                StopDischarge();
        }

        private void TickDischarge(float deltaTime)
        {
            // ── 1. Drain capacity ────────────────────────────────────────────────
            //
            // ConsumptionRate is in the same absolute units as MaxCapacity (e.g. kg/s).
            // Dividing by MaxCapacity converts it to a normalized fraction for internal use.
            // Duration = MaxCapacity / ConsumptionRate.

            float drain = (_extinguisherData.ConsumptionRate / _maxCapacity) * deltaTime;
            SetNormalizedCapacity(Mathf.Max(0f, _normalizedCapacity - drain));

            // ── 2. Depletion check ───────────────────────────────────────────────

            if (_normalizedCapacity <= 0f)
            {
                StopDischarge();
                RaiseDepletedFeedbackOnce();
                return;
            }

            // ── 3. Spray evaluation ──────────────────────────────────────────────

            if (_evaluator == null) return;

            ExtinguishResult result = _evaluator.Evaluate(
                _resolvedSprayWorldOrigin,
                _resolvedSprayWorldDirection,
                _aimProvider.EvaluationOrigin,
                _aimProvider.EvaluationDirection,
                _extinguisherData,
                deltaTime);

            OnSprayEvaluated?.Invoke(result);

            if (result.DidHitZone && result.ExtinguishAmountCalculated > 0f)
                _onEstinguishing?.Raise();
        }

        private void SetNormalizedCapacity(float value)
        {
            _normalizedCapacity = value;
            if (value > 0f)
                _depletedFeedbackRaisedUntilRefill = false;

            OnCapacityChanged?.Invoke(_normalizedCapacity);
            // Multiply by 1000 so the HUD can display one decimal place (e.g. 787 → 78.7%).
            _onCapacityChangedEvent?.Raise(Mathf.RoundToInt(_normalizedCapacity * 1000f));
        }

        /// <summary>
        /// Pin pulled, held, spray input started this frame, and capacity is already zero — first empty-squeeze feedback.
        /// </summary>
        private void TryRaiseDepletedOnFirstEmptySprayAttempt()
        {
            if (_depletedFeedbackRaisedUntilRefill || !IsDepleted)
                return;

            if (_sprayInputProvider == null || !_sprayInputProvider.IsSprayStartedThisFrame)
                return;

            if (!_pinPulled)
                return;

            if (_holdStateProvider == null || !_holdStateProvider.IsHeld)
                return;

            if (_extinguisherData == null || _aimProvider == null)
                return;

            RaiseDepletedFeedbackOnce();
        }

        private void RaiseDepletedFeedbackOnce()
        {
            if (_depletedFeedbackRaisedUntilRefill)
                return;

            _depletedFeedbackRaisedUntilRefill = true;
            OnExtinguisherDepleted?.Invoke();
            _onExtinguisherDepletedEvent?.Raise();
            _onExtinguisherDepletedUnity?.Invoke();
        }

        /// <summary>
        /// Resolves spray pair used for evaluation and debug gizmos. Call from edit-mode gizmo drawers
        /// when <see cref="_sprayWorldOriginTransform"/> is used so Scene view matches play mode.
        /// </summary>
        public void ComputeResolvedSprayPose(out Vector3 origin, out Vector3 direction)
        {
            if (_aimProvider == null)
            {
                origin = _sprayWorldOriginTransform != null
                    ? _sprayWorldOriginTransform.position
                    : transform.position;
                direction = transform.forward;
                return;
            }

            origin = _sprayWorldOriginTransform != null
                ? _sprayWorldOriginTransform.position
                : _aimProvider.SprayOrigin;
            direction = _aimProvider.SprayDirection;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_aimProviderSource != null && _aimProviderSource is not IAimProvider)
                Debug.LogWarning(
                    $"[ExtinguisherController] '{_aimProviderSource.name}' does not implement IAimProvider.", this);

            if (_sprayInputProviderSource != null && _sprayInputProviderSource is not ISprayInputProvider)
                Debug.LogWarning(
                    $"[ExtinguisherController] '{_sprayInputProviderSource.name}' does not implement ISprayInputProvider.", this);

            if (_holdStateProviderSource != null && _holdStateProviderSource is not IHoldStateProvider)
                Debug.LogWarning(
                    $"[ExtinguisherController] '{_holdStateProviderSource.name}' does not implement IHoldStateProvider.", this);
        }
#endif
    }
}