using UnityEngine;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    public class ScenarioInteractable : MonoBehaviour
    {
        [SerializeField]
        private string actionId;

        [SerializeField]
        private OfficeFireScenarioController targetScenario;

        [SerializeField]
        private bool interactOnce = true;

        [SerializeField]
        private UnityEvent onInteracted = new UnityEvent();

        private bool _hasInteracted;

        public string ActionId => actionId;

        public void Interact()
        {
            if (interactOnce && _hasInteracted)
            {
                return;
            }

            if (targetScenario == null)
            {
                Debug.LogWarning("[ScenarioInteractable] Missing targetScenario.", this);
                return;
            }

            targetScenario.HandleAction(actionId);

            if (interactOnce)
            {
                _hasInteracted = true;
            }

            if (onInteracted != null)
            {
                onInteracted.Invoke();
            }
        }
    }
}
