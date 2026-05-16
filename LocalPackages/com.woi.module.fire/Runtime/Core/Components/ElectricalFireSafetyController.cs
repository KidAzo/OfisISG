using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FireExtinguisher.Core
{
    /// <summary>
    /// Tracks the prerequisite safety sequence for electrical fire scenarios and
    /// gradually extinguishes assigned <see cref="FireSource"/> objects once the
    /// full sequence (breaker off → emergency button pressed) is completed.
    ///
    /// Sequence:
    ///   1. Player turns off the breaker  → <see cref="IsBreakerOff"/> = true.
    ///   2. Player presses emergency button → fire slowly extinguishes.
    ///   If the button is pressed BEFORE the breaker is off → nothing happens
    ///   (invalid press event is raised for feedback).
    /// </summary>
    [AddComponentMenu("Fire Extinguisher/Safety/Electrical Fire Safety Controller")]
    public sealed class ElectricalFireSafetyController : MonoBehaviour
    {
        private const string LogPrefix = "[ElectricalFireSafety]";

        // ── Fire targets ─────────────────────────────────────────────────────────

        [Header("Fire Sources")]
        [Tooltip("All FireSource objects inside this electrical fire case. " +
                 "When safety is completed these fires are gradually extinguished.")]
        [SerializeField] private List<FireSource> _fireSources = new List<FireSource>();

        [Tooltip("Seconds over which each FireSource is fully suppressed after the " +
                 "emergency button is pressed with the breaker already off.")]
        [SerializeField, Min(0.1f)] private float _extinguishDuration = 5f;

        // ── Events ───────────────────────────────────────────────────────────────

        [Header("Events")]
        [Tooltip("Raised when the breaker is successfully turned off.")]
        [SerializeField] private UnityEvent _onBreakerTurnedOff = new UnityEvent();

        [Tooltip("Raised when the button is pressed while the breaker is still ON. " +
                 "Wire audio/visual 'invalid' feedback here.")]
        [SerializeField] private UnityEvent _onInvalidButtonPress = new UnityEvent();

        [Tooltip("Raised the moment the emergency button is accepted (breaker was already off).")]
        [SerializeField] private UnityEvent _onEmergencyButtonPressed = new UnityEvent();

        [Tooltip("Raised once the safety sequence is fully complete and extinguishing begins.")]
        [SerializeField] private UnityEvent _onSafetyCompleted = new UnityEvent();

        [Tooltip("Raised once every assigned FireSource reaches zero intensity.")]
        [SerializeField] private UnityEvent _onAllFiresExtinguished = new UnityEvent();

        // ── State ────────────────────────────────────────────────────────────────

        public bool IsBreakerOff { get; private set; }
        public bool IsEmergencyButtonPressed { get; private set; }
        public bool IsSafetyCompleted { get; private set; }
        public bool IsExtinguishing { get; private set; }

        // ── Event accessors ──────────────────────────────────────────────────────

        public UnityEvent OnBreakerTurnedOff      => _onBreakerTurnedOff;
        public UnityEvent OnInvalidButtonPress     => _onInvalidButtonPress;
        public UnityEvent OnEmergencyButtonPressed => _onEmergencyButtonPressed;
        public UnityEvent OnSafetyCompleted        => _onSafetyCompleted;
        public UnityEvent OnAllFiresExtinguished   => _onAllFiresExtinguished;

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Call this from the breaker interactable when the breaker is flipped off.
        /// Idempotent — calling it a second time is a no-op.
        /// </summary>
        public void TurnOffBreaker()
        {
            if (IsBreakerOff)
            {
                Debug.Log($"{LogPrefix} Breaker is already off.", this);
                return;
            }

            IsBreakerOff = true;
            Debug.Log($"{LogPrefix} Breaker turned off.", this);
            _onBreakerTurnedOff?.Invoke();

            // If the player already pressed the emergency button before cutting the breaker,
            // treat it as valid now and complete the safety sequence immediately.
            if (IsEmergencyButtonPressed && !AreAllMonitoredFiresAlreadyExtinguished())
            {
                Debug.Log($"{LogPrefix} Emergency button was already pressed — completing safety sequence now.", this);
                CompleteSafety();
            }
            else if (IsEmergencyButtonPressed)
            {
                Debug.Log(
                    $"{LogPrefix} Emergency button was already pressed but all monitored fires are already extinguished — skipping safety completion.",
                    this);
            }
        }

        /// <summary>
        /// Call this from the emergency button interactable when the button is pressed.
        /// If the breaker is still ON, the press is rejected and <see cref="_onInvalidButtonPress"/>
        /// is raised so you can play an error sound / visual.
        /// If the breaker is OFF, the safety sequence completes and the fires are extinguished.
        /// When every assigned <see cref="FireSource"/> is already extinguished (e.g. by extinguisher),
        /// this method returns without raising completion events — keep button press sound on the
        /// emergency button interactable’s <c>UnityEvent</c> (it runs before this call).
        /// </summary>
        public void PressEmergencyButton()
        {
            if (!IsBreakerOff)
            {
                // Breaker is still on — record the press but don't extinguish yet.
                // When the breaker IS turned off, TurnOffBreaker() will see
                // IsEmergencyButtonPressed = true and complete the sequence automatically.
                if (!IsEmergencyButtonPressed)
                {
                    IsEmergencyButtonPressed = true;
                    Debug.Log($"{LogPrefix} Emergency button pressed before breaker was off — recorded, waiting for breaker.", this);
                }

                _onInvalidButtonPress?.Invoke();
                return;
            }

            if (AreAllMonitoredFiresAlreadyExtinguished())
            {
                Debug.Log(
                    $"{LogPrefix} Emergency button ignored — all monitored fires are already extinguished (no safety / sprinkler events).",
                    this);
                return;
            }

            if (!IsEmergencyButtonPressed)
            {
                IsEmergencyButtonPressed = true;
                Debug.Log($"{LogPrefix} Emergency button accepted.", this);
                _onEmergencyButtonPressed?.Invoke();
            }

            CompleteSafety();
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// True when every non-null entry in <see cref="_fireSources"/> is <see cref="FireSource.IsExtinguished"/>.
        /// Empty or all-null list returns false (preserves previous “no sources” behaviour).
        /// </summary>
        public bool AreAllMonitoredFiresAlreadyExtinguished()
        {
            if (_fireSources == null || _fireSources.Count == 0)
                return false;

            bool any = false;
            for (int i = 0; i < _fireSources.Count; i++)
            {
                FireSource fs = _fireSources[i];
                if (fs == null)
                    continue;

                any = true;
                if (!fs.IsExtinguished)
                    return false;
            }

            return any;
        }

        private void CompleteSafety()
        {
            if (IsSafetyCompleted)
                return;

            if (AreAllMonitoredFiresAlreadyExtinguished())
            {
                Debug.Log(
                    $"{LogPrefix} CompleteSafety skipped — all monitored fires already extinguished (no completion events).",
                    this);
                return;
            }

            IsSafetyCompleted = true;
            Debug.Log($"{LogPrefix} Electrical fire safety completed — beginning extinguishment.", this);
            _onSafetyCompleted?.Invoke();

            if (_fireSources == null || _fireSources.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} No FireSources assigned — nothing to extinguish.", this);
                return;
            }

            StartCoroutine(ExtinguishRoutine());
        }

        private IEnumerator ExtinguishRoutine()
        {
            IsExtinguishing = true;
            Debug.Log($"{LogPrefix} Extinguishing {_fireSources.Count} fire source(s) over {_extinguishDuration}s.", this);

            int finishedCount = 0;

            // Launch one coroutine per source so each runs independently.
            foreach (FireSource source in _fireSources)
            {
                if (source == null) { finishedCount++; continue; }
                StartCoroutine(ExtinguishSource(source, () => finishedCount++));
            }

            // Wait until every source coroutine has signalled completion.
            while (finishedCount < _fireSources.Count)
                yield return null;

            IsExtinguishing = false;
            Debug.Log($"{LogPrefix} All fires fully extinguished.", this);
            _onAllFiresExtinguished?.Invoke();
        }

        /// <summary>
        /// Gradually drains every zone of <paramref name="source"/> to zero over
        /// <see cref="_extinguishDuration"/> seconds, then invokes <paramref name="onDone"/>.
        /// </summary>
        private IEnumerator ExtinguishSource(FireSource source, System.Action onDone)
        {
            IReadOnlyList<FireTargetZone> zones = source.Zones;

            // Pre-calculate the suppression rate per zone per second
            // so that regardless of zone max-intensity the fire reaches zero
            // in _extinguishDuration seconds.
            while (true)
            {
                if (source == null || source.IsExtinguished)
                    break;

                bool anyActive = false;
                foreach (FireTargetZone zone in zones)
                {
                    if (zone == null || zone.IsExtinguished)
                        continue;

                    anyActive = true;
                    float suppressionThisFrame = zone.MaxIntensity > 0f
                        ? (zone.MaxIntensity / _extinguishDuration) * Time.deltaTime
                        : zone.CurrentIntensity; // drain instantly if no valid max

                    zone.ApplySuppression(suppressionThisFrame);
                }

                if (!anyActive)
                    break;

                yield return null;
            }

            onDone?.Invoke();
        }
    }
}
