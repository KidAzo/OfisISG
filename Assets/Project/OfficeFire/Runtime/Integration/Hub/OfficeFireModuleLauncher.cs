using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Thin entry point for future HUB / login UI: forwards to the scene bootstrapper.
    /// </summary>
    public sealed class OfficeFireModuleLauncher : MonoBehaviour, IOfficeFireModuleLauncher
    {
        [SerializeField]
        private OfficeFireScenarioBootstrapper bootstrapper;

        public void LaunchScenario(OfficeFireScenarioId scenarioId)
        {
            if (bootstrapper == null)
            {
                Debug.LogWarning("[OfficeFireModuleLauncher] Bootstrapper is not assigned.", this);
                return;
            }

            bootstrapper.StartScenario(scenarioId);
        }
    }
}
