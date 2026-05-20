using UnityEngine;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    public sealed class SelectableScenarioAction : MonoBehaviour, ISelectable
    {
        [SerializeField]
        private bool isSelectable = true;

        [SerializeField]
        private OfficeFireScenarioController targetScenario;

        [SerializeField]
        private string actionId;

        [SerializeField]
        private UnityEvent onSelected = new UnityEvent();

        public string ActionId => actionId;

        public bool IsSelectable => isSelectable;

        public void Select(SelectionContext context)
        {
            if (!isSelectable)
            {
                return;
            }

            if (targetScenario == null)
            {
                Debug.LogWarning("[SelectableScenarioAction] targetScenario is not assigned.", this);
                return;
            }

            if (string.IsNullOrEmpty(actionId))
            {
                Debug.LogWarning("[SelectableScenarioAction] actionId is empty.", this);
                return;
            }

            DispatchScenarioAction();
        }

        /// <summary>
        /// Invokes <see cref="OfficeFireScenarioController.HandleAction"/> (e.g. from <see cref="SelectableDoor"/> onOpened).
        /// </summary>
        public void DispatchScenarioAction()
        {
            if (targetScenario == null)
            {
                Debug.LogWarning("[SelectableScenarioAction] targetScenario is not assigned.", this);
                return;
            }

            if (string.IsNullOrEmpty(actionId))
            {
                Debug.LogWarning("[SelectableScenarioAction] actionId is empty.", this);
                return;
            }

            targetScenario.HandleAction(actionId);

            if (actionId == "use_extinguisher" || actionId == "grab_extinguisher")
            {
                Debug.Log(
                    $"[SelectableScenarioAction] '{actionId}' -> '{targetScenario.name}' ({targetScenario.GetType().Name})",
                    this);
            }

            if (onSelected != null)
            {
                onSelected.Invoke();
            }
        }
    }
}
