using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Optional breaker gate for archive scenes that include a physical switch.
    /// Disabled by default — archive flow only requires alarm before extinguishing.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Archive Electrical Safety Setup")]
    public sealed class OfficeFireArchiveElectricalSafetySetup : MonoBehaviour
    {
        [SerializeField]
        private bool enableBreakerGate;

        [SerializeField]
        private ArchiveRoomScenarioController scenario;

        [SerializeField]
        private FireSource fireSource;

        [Header("Debug")]
        [SerializeField]
        private bool enableDebugLogs = true;

        private ElectricalFireSafetyController _safetyController;
        private FireExtinguishPrerequisiteGate _prerequisiteGate;

        public bool IsBreakerOff => _safetyController != null && _safetyController.IsBreakerOff;

        private void Awake()
        {
            if (!enableBreakerGate)
            {
                return;
            }

            if (scenario == null)
            {
                scenario = GetComponent<ArchiveRoomScenarioController>();
            }

            if (fireSource == null)
            {
                fireSource = FindFirstObjectByType<FireSource>(FindObjectsInactive.Include);
            }

            EnsureSafetyComponents();
        }

        private void OnEnable()
        {
            if (_safetyController == null)
            {
                return;
            }

            _safetyController.OnBreakerTurnedOff.AddListener(HandleBreakerTurnedOff);
        }

        private void OnDisable()
        {
            if (_safetyController == null)
            {
                return;
            }

            _safetyController.OnBreakerTurnedOff.RemoveListener(HandleBreakerTurnedOff);
        }

        /// <summary>
        /// Turns off the breaker so extinguisher spray can affect the fire.
        /// </summary>
        public void CutPower()
        {
            EnsureSafetyComponents();

            if (_safetyController == null)
            {
                LogWarning("ElectricalFireSafetyController yok — elektrik kesilemedi.");
                return;
            }

            _safetyController.TurnOffBreaker();
            Log($"CutPower cagrildi. IsBreakerOff={_safetyController.IsBreakerOff}");
        }

        private void EnsureSafetyComponents()
        {
            if (fireSource == null)
            {
                LogWarning("FireSource atanmadi — sondurucu on kosulu kurulamadi.");
                return;
            }

            GameObject fireObject = fireSource.gameObject;

            _safetyController = fireObject.GetComponent<ElectricalFireSafetyController>();
            if (_safetyController == null)
            {
                _safetyController = fireObject.AddComponent<ElectricalFireSafetyController>();
            }

            _safetyController.RegisterFireSource(fireSource);

            _prerequisiteGate = fireObject.GetComponent<FireExtinguishPrerequisiteGate>();
            if (_prerequisiteGate == null)
            {
                _prerequisiteGate = fireObject.AddComponent<FireExtinguishPrerequisiteGate>();
            }

            _prerequisiteGate.ConfigureForBreakerOnly(_safetyController);
            Log(
                $"Elektrik guvenlik kuruldu. gate.CanExtinguish={_prerequisiteGate.CanExtinguish}, " +
                $"IsBreakerOff={_safetyController.IsBreakerOff}");
        }

        private void HandleBreakerTurnedOff()
        {
            if (scenario == null || !enableBreakerGate)
            {
                return;
            }

            Log("Salter kapatildi — pull_power_plug senaryoya gonderiliyor.");
            scenario.HandleAction(ArchiveRoomScenarioController.Actions.PullPowerPlug);
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.Log($"[ArchiveElectricalSafety] {message}", this);
        }

        private void LogWarning(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.LogWarning($"[ArchiveElectricalSafety] {message}", this);
        }
    }
}
