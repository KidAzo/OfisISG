using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Dispatches blanket use to <see cref="KitchenCafeScenarioController"/> when the kitchen fire is covered.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Kitchen Blanket Scenario Bridge")]
    public sealed class OfficeFireKitchenBlanketScenarioBridge : MonoBehaviour
    {
        [SerializeField]
        private KitchenCafeScenarioController scenario;

        [SerializeField]
        private FireBlanketUseController blanketUseController;

        [SerializeField]
        private bool enableDebugLogs;

        private void Awake()
        {
            if (scenario == null)
            {
                scenario = GetComponent<KitchenCafeScenarioController>();
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
        }

        private void BindListeners()
        {
            if (blanketUseController == null)
            {
                blanketUseController = FindPreferredBlanketUseController();
            }

            if (blanketUseController == null)
            {
                return;
            }

            blanketUseController.BlanketFireExtinguished -= HandleBlanketFireExtinguished;
            blanketUseController.BlanketFireExtinguished += HandleBlanketFireExtinguished;
        }

        private void UnbindListeners()
        {
            if (blanketUseController == null)
            {
                return;
            }

            blanketUseController.BlanketFireExtinguished -= HandleBlanketFireExtinguished;
        }

        private void HandleBlanketFireExtinguished()
        {
            if (scenario == null)
            {
                LogWarning("KitchenCafeScenarioController missing — blanket use not reported.");
                return;
            }

            Log("Blanket used on fire — reporting correct kitchen actions.");
            scenario.HandleBlanketUsedOnFire();
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.Log($"[KitchenBlanketScenarioBridge] {message}", this);
        }

        private void LogWarning(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.LogWarning($"[KitchenBlanketScenarioBridge] {message}", this);
        }

        private static FireBlanketUseController FindPreferredBlanketUseController()
        {
            FireBlanketUseController[] all = FindObjectsByType<FireBlanketUseController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.activeInHierarchy)
                {
                    return all[i];
                }
            }

            return all.Length > 0 ? all[0] : null;
        }
    }
}
