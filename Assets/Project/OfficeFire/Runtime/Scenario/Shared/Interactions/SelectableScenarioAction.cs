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

            targetScenario.HandleAction(actionId);

            if (onSelected != null)
            {
                onSelected.Invoke();
            }
        }
    }
}
