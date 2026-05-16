using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace FireExtinguisher.Core
{
    /// <summary>
    /// Opt-in gate that blocks a <see cref="FireSource"/> from accepting extinguisher
    /// spray until a specific safety prerequisite has been met.
    ///
    /// Multiple gate modes are available:
    /// <list type="bullet">
    ///   <item>
    ///     <term>BreakerOffOnly</term>
    ///     <description>
    ///       The gate opens as soon as the electrical breaker is turned off.
    ///       Use this for fire cases where the player extinguishes the fire themselves
    ///       with a tube/extinguisher after cutting the power.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>FullSafetySequence</term>
    ///     <description>
    ///       The gate opens only after the full sequence is complete:
    ///       breaker off → emergency button pressed.
    ///       The fire is then extinguished automatically by the controller.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>ValveOpenOnly</term>
    ///     <description>
    ///       Class C pipe scenario: the gate opens when the scene valve is fully open
    ///       (<see cref="ClassCFireValveController.IsValveOpen"/>), after the interactable
    ///       rotation animation completes.
    ///     </description>
    ///   </item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Fire Extinguisher/Safety/Fire Extinguish Prerequisite Gate")]
    public sealed class FireExtinguishPrerequisiteGate : MonoBehaviour
    {
        // ── Types ────────────────────────────────────────────────────────────────

        public enum GateMode
        {
            /// <summary>
            /// Gate opens once the breaker is switched off.
            /// Player then extinguishes the fire manually with an extinguisher.
            /// </summary>
            BreakerOffOnly,

            /// <summary>
            /// Gate opens only after the full sequence (breaker off + emergency button).
            /// The <see cref="ElectricalFireSafetyController"/> extinguishes the fire automatically.
            /// </summary>
            FullSafetySequence,

            /// <summary>
            /// Class C: gate opens when <see cref="ClassCFireValveController"/> reports the pipe valve open.
            /// </summary>
            ValveOpenOnly,
        }

        // ── Inspector ────────────────────────────────────────────────────────────

        [Tooltip("Electrical case: breaker + emergency button state.")]
        [SerializeField] private ElectricalFireSafetyController _controller;

        [Tooltip("Class C case: pipe valve prerequisite (used when mode is ValveOpenOnly).")]
        [SerializeField] private ClassCFireValveController _valveController;

        [Tooltip(
            "BreakerOffOnly  — gate opens when the switch is turned off; player uses extinguisher.\n" +
            "FullSafetySequence — gate opens after breaker off + emergency button; fire auto-extinguishes.\n" +
            "ValveOpenOnly — Class C; gate opens when the pipe valve interaction finishes.")]
        [SerializeField] private GateMode _mode = GateMode.FullSafetySequence;

        [Header("Feedback — Class C valve closed")]
        [Tooltip(
            "ValveOpenOnly: doğru tüple yangına isabet var, suppression kapıda bloklu (valf kapalı). " +
            "Cooldown: Blocked Attempt Cooldown.")]
        [FormerlySerializedAs("_onExtinguishAttemptBlockedByPrerequisite")]
        [SerializeField] private UnityEvent _onValveClosedSprayAttempt = new UnityEvent();

        [Header("Feedback — electrical breaker still on")]
        [Tooltip(
            "BreakerOffOnly: doğru tüple yangına isabet var, suppression kapıda bloklu (şalter hâlâ açık). " +
            "Aynı cooldown alanı kullanılır.")]
        [SerializeField] private UnityEvent _onBreakerOnSprayAttempt = new UnityEvent();

        [Tooltip("Valf ve şalter 'kapalı ön koşul' feedback event'leri için ortak minimum saniye aralığı.")]
        [SerializeField, Min(0f)] private float _blockedAttemptCooldownSeconds = 0.6f;

        private float _lastBlockedAttemptEventTime = float.NegativeInfinity;

        // ── Public API ───────────────────────────────────────────────────────────

        public GateMode Mode => _mode;

        /// <summary>
        /// Valf (ValveOpenOnly) veya şalter (BreakerOffOnly) ön koşulu sağlanmadan spray SOAP'larını
        /// bastırmak için eğitim köprüleri bunu kullanır. FullSafetySequence bu mantığa dahil değildir.
        /// </summary>
        public bool ShouldSuppressTrainingSprayFeedback =>
            !CanExtinguish &&
            (_mode == GateMode.ValveOpenOnly || _mode == GateMode.BreakerOffOnly);

        public UnityEvent OnValveClosedSprayAttempt => _onValveClosedSprayAttempt;

        public UnityEvent OnBreakerOnSprayAttempt => _onBreakerOnSprayAttempt;

        /// <summary>
        /// <see cref="ExtinguishEvaluator"/> çağırır: kapı hâlâ kapalıyken etkili spray denemesi.
        /// Yalnızca <see cref="GateMode.ValveOpenOnly"/> ve <see cref="GateMode.BreakerOffOnly"/> için ilgili event'i tetikler.
        /// </summary>
        public void NotifyPrerequisiteBlockedSprayAttempt()
        {
            if (CanExtinguish)
                return;

            if (_mode != GateMode.ValveOpenOnly && _mode != GateMode.BreakerOffOnly)
                return;

            if (_blockedAttemptCooldownSeconds > 0f &&
                Time.time - _lastBlockedAttemptEventTime < _blockedAttemptCooldownSeconds)
            {
                return;
            }

            _lastBlockedAttemptEventTime = Time.time;

            if (_mode == GateMode.ValveOpenOnly)
                _onValveClosedSprayAttempt?.Invoke();
            else
                _onBreakerOnSprayAttempt?.Invoke();
        }

        /// <summary>
        /// <c>true</c> when the prerequisite defined by <see cref="_mode"/> is satisfied
        /// and the <see cref="FireSource"/> may receive extinguisher spray.
        /// </summary>
        public bool CanExtinguish
        {
            get
            {
                if (_mode == GateMode.ValveOpenOnly)
                {
                    if (_valveController == null)
                        return false;

                    return _valveController.IsValveOpen;
                }

                if (_controller == null)
                    return true; // no controller = no restriction

                return _mode switch
                {
                    GateMode.BreakerOffOnly     => _controller.IsBreakerOff,
                    GateMode.FullSafetySequence => _controller.IsSafetyCompleted,
                    _                           => false,
                };
            }
        }
    }
}
