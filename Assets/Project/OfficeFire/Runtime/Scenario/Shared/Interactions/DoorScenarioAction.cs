using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// After <see cref="SelectableDoor"/> finishes opening, calls
    /// <see cref="OfficeFireScenarioController.HandleAction"/> with the action id set in the Inspector.
    /// </summary>
    [RequireComponent(typeof(SelectableDoor))]
    public sealed class DoorScenarioAction : MonoBehaviour
    {
        [SerializeField]
        private OfficeFireScenarioController targetScenario;

        [SerializeField]
        private string actionId;

        [SerializeField]
        private bool dispatchOnlyOnce = true;

        private SelectableDoor _door;
        private bool _hasDispatched;

        private void OnEnable()
        {
            _door = GetComponent<SelectableDoor>();
            _door.AddOpenedListener(OnDoorOpened);
        }

        private void OnDisable()
        {
            if (_door != null)
            {
                _door.RemoveOpenedListener(OnDoorOpened);
            }
        }

        private void OnDoorOpened()
        {
            if (dispatchOnlyOnce && _hasDispatched)
            {
                return;
            }

            if (targetScenario == null)
            {
                Debug.LogWarning("[DoorScenarioAction] targetScenario is not assigned.", this);
                return;
            }

            if (string.IsNullOrEmpty(actionId))
            {
                Debug.LogWarning("[DoorScenarioAction] actionId is empty.", this);
                return;
            }

            targetScenario.HandleAction(actionId);
            _hasDispatched = true;
        }
    }
}
