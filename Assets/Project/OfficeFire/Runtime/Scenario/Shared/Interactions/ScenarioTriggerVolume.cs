using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Player enters the trigger → <see cref="OfficeFireScenarioController.HandleAction"/> once; reminders stop.
    /// Optional: if the player never enters, call <see cref="HandleAction"/> on a delay/loop (use only for simple triggers;
    /// scenario milestones such as NoticeSmoke should use state-based reminders on the scenario controller instead).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ScenarioTriggerVolume : MonoBehaviour, IPlayerTriggerVolumeRefresh
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

        [SerializeField]
        private UnityEvent onTrigger;

        private bool _hasTriggered;
        private Coroutine _reminderRoutine;
        private readonly System.Collections.Generic.HashSet<Collider> _insideColliders = new System.Collections.Generic.HashSet<Collider>();

        private void OnEnable()
        {
            if (remindWhenNotEntered && !_hasTriggered)
                StartReminderRoutine();

            StartCoroutine(ManualOverlapCheckRoutine());
        }

        private IEnumerator ManualOverlapCheckRoutine()
        {
            yield return new WaitForFixedUpdate();
            RefreshPlayerOverlap();
        }

        public void RefreshPlayerOverlap()
        {
            if (triggerOnce && _hasTriggered)
                return;

            Collider volume = GetComponent<Collider>();
            if (volume == null || !volume.enabled)
                return;

            Collider[] overlaps = PlayerTriggerOverlapUtility.QueryLayerColliders(volume, playerLayer, transform);
            if (overlaps != null)
            {
                for (int i = 0; i < overlaps.Length; i++)
                    ProcessTrigger(overlaps[i]);
            }

            CharacterController[] controllers = PlayerTriggerOverlapUtility.FindActiveCharacterControllers();
            for (int i = 0; i < controllers.Length; i++)
            {
                CharacterController controller = controllers[i];
                if (controller == null || !controller.gameObject.activeInHierarchy)
                    continue;

                if (!PlayerTriggerOverlapUtility.IsLayerInMask(controller.gameObject.layer, playerLayer))
                    continue;

                if (!PlayerTriggerOverlapUtility.CharacterControllerIntersectsVolume(controller, volume))
                    continue;

                ProcessCharacterController(controller);
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
            if (!PlayerTriggerOverlapUtility.IsLayerInMask(other.gameObject.layer, playerLayer))
                return;

            if (!_insideColliders.Add(other))
                return;

            TryDispatchTrigger();
        }

        void ProcessCharacterController(CharacterController controller)
        {
            if (controller == null)
                return;

            Collider proxy = controller.GetComponent<Collider>();
            if (proxy != null)
            {
                ProcessTrigger(proxy);
                return;
            }

            TryDispatchTrigger();
        }

        void TryDispatchTrigger()
        {
            if (triggerOnce && _hasTriggered)
                return;

            _hasTriggered = true;
            StopReminderRoutine();
            DispatchAction();
            onTrigger?.Invoke();
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
                return;

            StopCoroutine(_reminderRoutine);
            _reminderRoutine = null;
        }

        private IEnumerator ReminderRoutine()
        {
            if (initialDelayBeforeReminder > 0f)
                yield return new WaitForSeconds(initialDelayBeforeReminder);

            while (!_hasTriggered)
            {
                if (!DispatchAction())
                    yield break;

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
                return true;

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
                c.isTrigger = true;
        }
    }
}
