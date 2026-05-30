using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Dispatches blanket grab/use actions to <see cref="ServerRoomScenarioController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Server Blanket Scenario Bridge")]
    public sealed class OfficeFireServerBlanketScenarioBridge : MonoBehaviour
    {
        [SerializeField]
        private ServerRoomScenarioController scenario;

        [SerializeField]
        private PlayerFireBlanketEquipment blanketEquipment;

        [SerializeField]
        private FireBlanketUseController blanketUseController;

        [SerializeField]
        private bool enableDebugLogs;

        private FireBlanketPickupItem _dispatchedGrabForItem;

        private void Awake()
        {
            if (scenario == null)
            {
                scenario = GetComponent<ServerRoomScenarioController>();
            }
        }

        private void OnEnable()
        {
            BindListeners();
        }

        private void Start()
        {
            BindListeners();
        }

        private void OnDisable()
        {
            UnbindListeners();
            _dispatchedGrabForItem = null;
        }

        private void BindListeners()
        {
            if (blanketEquipment == null)
            {
                blanketEquipment = FindFirstObjectByType<PlayerFireBlanketEquipment>(FindObjectsInactive.Include);
            }

            if (blanketUseController == null)
            {
                blanketUseController = FindFirstObjectByType<FireBlanketUseController>(FindObjectsInactive.Include);
            }

            if (blanketEquipment != null)
            {
                blanketEquipment.OnBlanketChanged -= HandleBlanketChanged;
                blanketEquipment.OnBlanketChanged += HandleBlanketChanged;
            }

            if (blanketUseController != null)
            {
                blanketUseController.BlanketFireExtinguished -= HandleBlanketFireExtinguished;
                blanketUseController.BlanketFireExtinguished += HandleBlanketFireExtinguished;
            }
        }

        private void UnbindListeners()
        {
            if (blanketEquipment != null)
            {
                blanketEquipment.OnBlanketChanged -= HandleBlanketChanged;
            }

            if (blanketUseController != null)
            {
                blanketUseController.BlanketFireExtinguished -= HandleBlanketFireExtinguished;
            }
        }

        private void HandleBlanketChanged(FireBlanketPickupItem item)
        {
            if (item == null)
            {
                _dispatchedGrabForItem = null;
                return;
            }

            if (ReferenceEquals(item, _dispatchedGrabForItem))
            {
                return;
            }

            _dispatchedGrabForItem = item;
            DispatchGrabBlanket();
        }

        private void HandleBlanketFireExtinguished()
        {
            DispatchUseBlanket();
        }

        private void DispatchGrabBlanket()
        {
            if (scenario == null)
            {
                LogWarning("ServerRoomScenarioController missing — grab_blanket not sent.");
                return;
            }

            Log("Blanket equipped — grab_blanket.");
            scenario.HandleAction(ServerRoomScenarioController.Actions.GrabBlanket);
        }

        private void DispatchUseBlanket()
        {
            if (scenario == null)
            {
                LogWarning("ServerRoomScenarioController missing — use_blanket not sent.");
                return;
            }

            Log("Blanket used on fire — use_blanket.");
            scenario.HandleAction(ServerRoomScenarioController.Actions.UseBlanket);
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.Log($"[ServerBlanketScenarioBridge] {message}", this);
        }

        private void LogWarning(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.LogWarning($"[ServerBlanketScenarioBridge] {message}", this);
        }
    }
}
