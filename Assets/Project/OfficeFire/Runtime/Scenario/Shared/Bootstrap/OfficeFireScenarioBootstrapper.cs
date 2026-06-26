using System.Collections.Generic;
using UnityEngine;

namespace Woi.OfficeFire
{
    [DefaultExecutionOrder(-200)]
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

        private void Awake()
        {
            OfficeFireExtinguisherHudBridge.EnsureOnBootstrapper();
            OfficeFireVrExtinguisherRigBootstrap.EnsureWired();
        }

        private void Start()
        {
            if (!autoStartOnPlay)
                return;

            OfficeFireScenarioId scenarioToStart = startScenario;

            if (OfficeFireLoginSession.IsSet)
            {
                OfficeFireSessionLanguage.SetRuntimeLanguageCode(OfficeFireLoginSession.LanguageCode);
                scenarioToStart = OfficeFireLoginSession.SelectedScenarioId;
                OfficeFireLoginSession.MarkScenarioConsumed();
            }

            StartScenario(scenarioToStart);
            OfficeFireLocalizedSignMaterials.ApplyAllInScene();
        }

        public void SetAutoStartOnPlay(bool enabled)
        {
            autoStartOnPlay = enabled;
        }

        public void StartConfiguredScenario()
        {
            StartConfiguredScenario(teleportPlayer: true);
        }

        public void StartConfiguredScenario(bool teleportPlayer)
        {
            StartScenario(startScenario, teleportPlayer);
        }

        /// <summary>
        /// Entry point for scenario selection. Safe to call from future LoginUI / ScenarioSelectionUI.
        /// </summary>
        public void StartScenario(OfficeFireScenarioId scenarioId)
        {
            StartScenario(scenarioId, teleportPlayer: true);
        }

        public void StartScenario(OfficeFireScenarioId scenarioId, bool teleportPlayer)
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
                    OfficeFireActiveScenarioLocator.UnregisterIfActive(controller);
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

            if (teleportPlayer && playerInitializer != null)
            {
                playerInitializer.InitializePlayer(scenarioId);
            }

            OfficeFireActiveScenarioLocator.RegisterActive(active);
            active.StartScenario();
            OfficeFireInputSync.RequestDelayedSync(this, $"StartScenario({scenarioId})");
            OfficeFireGameplayCameraSetup.RequestEnsureReady(this, $"StartScenario({scenarioId})");

            OfficeFireExtinguisherHudBridge bridge = GetComponent<OfficeFireExtinguisherHudBridge>();
            if (bridge != null)
            {
                bridge.RefreshBinding();
            }
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
