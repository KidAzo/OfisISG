using System.Collections.Generic;
using UnityEngine;

namespace Woi.OfficeFire
{
    public class OfficeFireScenarioBootstrapper : MonoBehaviour
    {
        [Header("Selection (Inspector for now; later LoginUI will call StartScenario)")]
        [SerializeField]
        private OfficeFireScenarioId startScenario = OfficeFireScenarioId.ArchiveRoom;

        [SerializeField]
        private List<OfficeFireScenarioController> scenarioControllers = new List<OfficeFireScenarioController>();

        [SerializeField]
        private bool autoStartOnPlay = true;

        [Header("Player")]
        [SerializeField]
        private OfficeFirePlayerInitializer playerInitializer;

        private void Start()
        {
            if (autoStartOnPlay)
            {
                StartScenario(startScenario);
            }
        }

        /// <summary>
        /// Entry point for scenario selection. Safe to call from future LoginUI / ScenarioSelectionUI.
        /// </summary>
        public void StartScenario(OfficeFireScenarioId scenarioId)
        {
            if (scenarioId == OfficeFireScenarioId.None)
            {
                Debug.LogWarning("[OfficeFireScenarioBootstrapper] StartScenario(None) ignored.", this);
                return;
            }

            OfficeFireScenarioController active = null;

            for (int i = 0; i < scenarioControllers.Count; i++)
            {
                OfficeFireScenarioController controller = scenarioControllers[i];
                if (controller == null)
                {
                    continue;
                }

                bool isMatch = controller.ScenarioId == scenarioId;
                if (!isMatch)
                {
                    controller.NotifyDeselected();
                }

                ApplyRootActive(controller, isMatch);

                if (isMatch)
                {
                    active = controller;
                }
            }

            if (active == null)
            {
                Debug.LogWarning(
                    $"[OfficeFireScenarioBootstrapper] No controller registered for {scenarioId}.",
                    this);
                return;
            }

            if (playerInitializer != null)
            {
                playerInitializer.InitializePlayer(scenarioId);
            }

            active.StartScenario();
        }

        private static void ApplyRootActive(OfficeFireScenarioController controller, bool active)
        {
            GameObject root = controller.ScenarioRoot;
            if (root != null && root.activeSelf != active)
            {
                root.SetActive(active);
            }
        }
    }
}
