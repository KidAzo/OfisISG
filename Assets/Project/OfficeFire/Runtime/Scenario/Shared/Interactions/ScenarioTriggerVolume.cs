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

        private void OnEnable()
        {
            if (remindWhenNotEntered && !_hasTriggered)
            {
                StartReminderRoutine();
            }
        }

        private void OnDisable()
        {
            StopReminderRoutine();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggerOnce && _hasTriggered)
            {
                return;
            }

            if (!IsInPlayerLayer(other.gameObject.layer))
            {
                return;
            }

            _hasTriggered = true;
            StopReminderRoutine();
            DispatchAction();
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
                Debug.LogWarning("[ScenarioTriggerVolume] No target scenario (assign targetScenario or start a scenario).", this);
                return false;
            }

            scenario.HandleAction(actionId);
            return true;
        }

        private bool TryResolveScenario(out OfficeFireScenarioController scenario)
        {
            if (targetScenario != null)
            {
                scenario = targetScenario;
                return true;
            }

            return OfficeFireActiveScenarioLocator.TryGetActive(out scenario);
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
