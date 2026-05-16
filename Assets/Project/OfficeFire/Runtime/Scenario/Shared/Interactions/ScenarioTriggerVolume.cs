using UnityEngine;

namespace Woi.OfficeFire
{
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

        private bool _hasTriggered;

        private void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null)
            {
                c.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggerOnce && _hasTriggered)
            {
                return;
            }

            if (targetScenario == null)
            {
                Debug.LogWarning("[ScenarioTriggerVolume] Missing targetScenario.", this);
                return;
            }

            if (!IsInPlayerLayer(other.gameObject.layer))
            {
                return;
            }

            _hasTriggered = true;
            targetScenario.HandleAction(actionId);
        }

        private bool IsInPlayerLayer(int layer)
        {
            return (playerLayer.value & (1 << layer)) != 0;
        }
    }
}
