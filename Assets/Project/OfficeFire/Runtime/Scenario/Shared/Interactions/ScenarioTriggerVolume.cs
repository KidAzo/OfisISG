using System.Collections;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Player enters the trigger → <see cref="OfficeFireScenarioController.HandleAction"/> once; reminders stop.
    /// Optional: if the player never enters, call <see cref="HandleAction"/> on a delay/loop (use only for simple triggers;
    /// scenario milestones such as NoticeSmoke should use state-based reminders on the scenario controller instead).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ScenarioTriggerVolume : MonoBehaviour
    {
        [SerializeField]
        private string actionId;

        [SerializeField]
        [Tooltip("Optional fallback when no scenario is active via OfficeFireScenarioBootstrapper.")]
        private OfficeFireScenarioController targetScenario;

        [SerializeField]
        private LayerMask playerLayer;

        [SerializeField]
        private bool triggerOnce = true;

        [Header("Reminder when player does not enter")]
        [Tooltip("When enabled: wait Initial Delay, then call HandleAction on Loop Interval until the player enters this volume.")]
        [SerializeField]
        private bool remindWhenNotEntered;

        [Tooltip("Seconds to wait after enable before the first reminder (player has not entered yet).")]
        [SerializeField, Min(0f)]
        private float initialDelayBeforeReminder = 30f;

        [Tooltip("Seconds between reminder calls after the first one. Ignored after the player has entered.")]
        [SerializeField, Min(0.1f)]
        private float reminderLoopInterval = 15f;

        private bool _hasTriggered;
        private Coroutine _reminderRoutine;
        private readonly System.Collections.Generic.HashSet<Collider> _insideColliders = new System.Collections.Generic.HashSet<Collider>();

        private void OnEnable()
        {
            if (remindWhenNotEntered && !_hasTriggered)
            {
                StartReminderRoutine();
            }

            // Unity's physics engine often puts non-moving Rigidbodies to sleep.
            // If the player is standing perfectly still when this trigger is enabled, 
            // OnTriggerStay will NOT fire. We must manually check for overlaps.
            StartCoroutine(ManualOverlapCheckRoutine());
        }

        private IEnumerator ManualOverlapCheckRoutine()
        {
            // Wait for the physics engine to register the enabled collider
            yield return new WaitForFixedUpdate();

            Collider c = GetComponent<Collider>();
            if (c == null || !c.enabled) yield break;

            Collider[] overlaps = null;
            if (c is BoxCollider box)
            {
                overlaps = Physics.OverlapBox(
                    transform.TransformPoint(box.center), 
                    Vector3.Scale(box.size, transform.lossyScale) * 0.5f, 
                    transform.rotation, 
                    playerLayer, 
                    QueryTriggerInteraction.Collide);
            }
            else if (c is SphereCollider sphere)
            {
                float maxScale = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
                overlaps = Physics.OverlapSphere(
                    transform.TransformPoint(sphere.center), 
                    sphere.radius * maxScale, 
                    playerLayer, 
                    QueryTriggerInteraction.Collide);
            }
            else
            {
                overlaps = Physics.OverlapBox(
                    c.bounds.center, 
                    c.bounds.extents, 
                    Quaternion.identity, 
                    playerLayer, 
                    QueryTriggerInteraction.Collide);
            }

            if (overlaps != null)
            {
                for (int i = 0; i < overlaps.Length; i++)
                {
                    ProcessTrigger(overlaps[i]);
                }
            }
        }

        private void OnDisable()
        {
            StopReminderRoutine();
            _insideColliders.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            ProcessTrigger(other);
        }

        private void OnTriggerStay(Collider other)
        {
            ProcessTrigger(other);
        }

        private void ProcessTrigger(Collider other)
        {
            if (!IsInPlayerLayer(other.gameObject.layer))
            {
                return;
            }

            if (!_insideColliders.Add(other))
            {
                // Already inside
                return;
            }

            if (triggerOnce && _hasTriggered)
            {
                return;
            }

            _hasTriggered = true;
            StopReminderRoutine();
            DispatchAction();
        }

        private void OnTriggerExit(Collider other)
        {
            _insideColliders.Remove(other);
        }

        private void StartReminderRoutine()
        {
            StopReminderRoutine();
            _reminderRoutine = StartCoroutine(ReminderRoutine());
        }

        private void StopReminderRoutine()
        {
            if (_reminderRoutine == null)
            {
                return;
            }

            StopCoroutine(_reminderRoutine);
            _reminderRoutine = null;
        }

        private IEnumerator ReminderRoutine()
        {
            if (initialDelayBeforeReminder > 0f)
            {
                yield return new WaitForSeconds(initialDelayBeforeReminder);
            }

            while (!_hasTriggered)
            {
                if (!DispatchAction())
                {
                    yield break;
                }

                yield return new WaitForSeconds(reminderLoopInterval);
            }
        }

        private bool DispatchAction()
        {
            if (!TryResolveScenario(out OfficeFireScenarioController scenario))
            {
                Debug.LogWarning("[ScenarioTriggerVolume] No target scenario (start a scenario or assign a fallback targetScenario).", this);
                return false;
            }

            scenario.HandleAction(actionId);
            return true;
        }

        private bool TryResolveScenario(out OfficeFireScenarioController scenario)
        {
            if (OfficeFireActiveScenarioLocator.TryGetActive(out scenario))
            {
                return true;
            }

            if (targetScenario != null)
            {
                scenario = targetScenario;
                return true;
            }

            scenario = null;
            return false;
        }

        private void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null)
            {
                c.isTrigger = true;
            }
        }

        private bool IsInPlayerLayer(int layer)
        {
            return (playerLayer.value & (1 << layer)) != 0;
        }
    }
}
