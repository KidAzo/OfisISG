using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Archive power cut interactable: E → turn off breaker + dispatch <c>pull_power_plug</c>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Archive Power Cut Interactable")]
    public sealed class ArchivePowerCutInteractable : MonoBehaviour, ISelectable
    {
        [SerializeField]
        private bool isSelectable = true;

        [SerializeField]
        private ArchiveRoomScenarioController targetScenario;

        [SerializeField]
        private OfficeFireArchiveElectricalSafetySetup electricalSafetySetup;

        [Header("Debug")]
        [SerializeField]
        private bool enableDebugLogs = true;

        public bool IsSelectable => isSelectable;

        private void Awake()
        {
            if (targetScenario == null)
            {
                targetScenario = FindFirstObjectByType<ArchiveRoomScenarioController>();
            }

            if (electricalSafetySetup == null && targetScenario != null)
            {
                electricalSafetySetup = targetScenario.GetComponent<OfficeFireArchiveElectricalSafetySetup>();
            }
        }

        public void Select(SelectionContext context)
        {
            if (!isSelectable)
            {
                return;
            }

            if (targetScenario == null)
            {
                Debug.LogWarning("[ArchivePowerCutInteractable] targetScenario atanmadi.", this);
                return;
            }

            if (enableDebugLogs)
            {
                Debug.Log("[ArchivePowerCutInteractable] Elektrik kesme secildi — pull_power_plug gonderiliyor.", this);
            }

            targetScenario.HandleAction(ArchiveRoomScenarioController.Actions.PullPowerPlug);
        }
    }
}
