using UnityEngine;
using Woi.Equipment;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Dispatches <see cref="ArchiveRoomScenarioController.Actions.GrabExtinguisher"/> when the player
    /// physically equips an extinguisher (PC interact or VR grab).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Archive Extinguisher Grab Bridge")]
    public sealed class OfficeFireArchiveExtinguisherGrabScenarioBridge : MonoBehaviour
    {
        [SerializeField]
        private ArchiveRoomScenarioController scenario;

        [SerializeField]
        private PlayerExtinguisherEquipment extinguisherEquipment;

        [SerializeField]
        private bool enableDebugLogs;

        private ExtinguisherPickupItem _dispatchedForItem;

        private void Awake()
        {
            if (scenario == null)
            {
                scenario = GetComponent<ArchiveRoomScenarioController>();
            }
        }

        private void OnEnable()
        {
            BindEquipmentListeners();
        }

        private void Start()
        {
            BindEquipmentListeners();
        }

        private void OnDisable()
        {
            UnbindEquipmentListeners();
            _dispatchedForItem = null;
        }

        private void BindEquipmentListeners()
        {
            if (extinguisherEquipment == null)
            {
                extinguisherEquipment = FindFirstObjectByType<PlayerExtinguisherEquipment>(
                    FindObjectsInactive.Include);
            }

            if (extinguisherEquipment == null)
            {
                LogWarning("PlayerExtinguisherEquipment bulunamadi — grab_extinguisher gonderilmeyecek.");
                return;
            }

            extinguisherEquipment.OnExtinguisherChanged -= HandleExtinguisherChanged;
            extinguisherEquipment.OnExtinguisherChanged += HandleExtinguisherChanged;
        }

        private void UnbindEquipmentListeners()
        {
            if (extinguisherEquipment == null)
            {
                return;
            }

            extinguisherEquipment.OnExtinguisherChanged -= HandleExtinguisherChanged;
        }

        private void HandleExtinguisherChanged(ExtinguisherPickupItem item)
        {
            if (item == null)
            {
                _dispatchedForItem = null;
                return;
            }

            if (ReferenceEquals(item, _dispatchedForItem))
            {
                return;
            }

            _dispatchedForItem = item;
            DispatchGrabExtinguisher();
        }

        private void DispatchGrabExtinguisher()
        {
            if (scenario == null)
            {
                LogWarning("ArchiveRoomScenarioController yok — grab_extinguisher gonderilemedi.");
                return;
            }

            Log("Sondurucu alindi — grab_extinguisher gonderiliyor.");
            scenario.HandleAction(ArchiveRoomScenarioController.Actions.GrabExtinguisher);
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.Log($"[ArchiveExtinguisherGrabBridge] {message}", this);
        }

        private void LogWarning(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.LogWarning($"[ArchiveExtinguisherGrabBridge] {message}", this);
        }
    }
}
