using UnityEngine;
using Woi.Equipment;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Dispatches <see cref="ServerRoomScenarioController.Actions.GrabExtinguisher"/> when the player
    /// physically equips an extinguisher (PC interact or VR grab).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Server Extinguisher Grab Bridge")]
    public sealed class OfficeFireServerExtinguisherGrabScenarioBridge : MonoBehaviour
    {
        [SerializeField]
        private ServerRoomScenarioController scenario;

        [SerializeField]
        private PlayerExtinguisherEquipment extinguisherEquipment;

        [SerializeField]
        private bool enableDebugLogs;

        private ExtinguisherPickupItem _dispatchedForItem;

        private void Awake()
        {
            if (scenario == null)
            {
                scenario = GetComponent<ServerRoomScenarioController>();
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
                LogWarning("ServerRoomScenarioController yok — grab_extinguisher gonderilemedi.");
                return;
            }

            Log("Sondurucu alindi — grab_extinguisher gonderiliyor.");
            scenario.HandleAction(ServerRoomScenarioController.Actions.GrabExtinguisher);
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.Log($"[ServerExtinguisherGrabBridge] {message}", this);
        }

        private void LogWarning(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.LogWarning($"[ServerExtinguisherGrabBridge] {message}", this);
        }
    }
}
