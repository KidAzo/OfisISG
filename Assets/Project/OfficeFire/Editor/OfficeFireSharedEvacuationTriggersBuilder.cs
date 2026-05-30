using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Woi.OfficeFire;

namespace Woi.OfficeFire.Editor
{
    /// <summary>
    /// Ensures evacuation triggers shared by Archive, Server, and Kitchen live under
    /// <c>04_SharedInteractions/EvacuationTriggers</c> and route to the active scenario.
    /// </summary>
    public static class OfficeFireSharedEvacuationTriggersBuilder
    {
        private const string RootName = "======FireModules======";
        private const string MenuPath = "Tools/Woi/Office Fire/Scene/Ensure Shared Evacuation Triggers";

        [MenuItem(MenuPath, false, 22)]
        private static void EnsureSharedEvacuationTriggersActiveScene()
        {
            EnsureSharedEvacuationTriggersInScene(SceneManager.GetActiveScene());
        }

        [MenuItem(MenuPath, true, 22)]
        private static bool EnsureSharedEvacuationTriggersActiveSceneValidate()
        {
            return !Application.isPlaying;
        }

        public static void EnsureSharedEvacuationTriggersInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Office Fire Scene] Scene is not valid or not loaded: " + scene.path);
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Ensure Shared Evacuation Triggers");
            int undoGroup = Undo.GetCurrentGroup();

            var created = new List<string>();
            var reused = new List<string>();
            var componentsAdded = new List<string>();
            var componentsAlreadyPresent = new List<string>();
            var componentWarnings = new List<string>();

            Transform root = OfficeFireSceneHierarchyBuilder.EnsureFireModulesRoot(scene, created, reused);
            if (root == null)
            {
                componentWarnings.Add("Could not find or create ======FireModules====== root.");
                LogSummary(scene.path, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            Transform sharedRoot = OfficeFireSceneHierarchyBuilder.EnsureChild(root, "04_SharedInteractions", created, reused);
            Transform evacuationTriggers = OfficeFireSceneHierarchyBuilder.EnsureChild(
                sharedRoot,
                "EvacuationTriggers",
                created,
                reused);

            RemoveScenarioLocalSharedTriggers(root, componentWarnings);
            MoveSharedTriggersToFolder(root, evacuationTriggers, created, reused);
            EnsureMinimumSharedTriggers(evacuationTriggers, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
            RewireSharedTriggers(evacuationTriggers, componentsAdded, componentsAlreadyPresent, componentWarnings);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            LogSummary(scene.path, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
        }

        public static bool IsSharedEvacuationTriggerName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            if (objectName.StartsWith("Trigger_ReachedExitDoor"))
            {
                return true;
            }

            if (objectName.StartsWith("Trigger_AssemblyArea") && !objectName.StartsWith("Trigger_AssemblyAreaDoor"))
            {
                return true;
            }

            return objectName.StartsWith("Trigger_Elevator_");
        }

        public static bool IsSharedEvacuationActionId(string actionId)
        {
            return actionId == OfficeFireSharedScenarioActions.ReachedExitDoor
                   || actionId == OfficeFireSharedScenarioActions.ReachAssemblyArea
                   || actionId == OfficeFireSharedScenarioActions.ElevatorProximity;
        }

        private static void RemoveScenarioLocalSharedTriggers(Transform fireModulesRoot, List<string> componentWarnings)
        {
            Transform scenarios = fireModulesRoot.Find("03_Scenarios");
            if (scenarios == null)
            {
                return;
            }

            for (int i = 0; i < scenarios.childCount; i++)
            {
                Transform scenarioRoot = scenarios.GetChild(i);
                Transform triggers = scenarioRoot.Find("Triggers");
                if (triggers == null)
                {
                    continue;
                }

                for (int c = triggers.childCount - 1; c >= 0; c--)
                {
                    Transform child = triggers.GetChild(c);
                    if (child == null || !IsSharedEvacuationTriggerName(child.name))
                    {
                        continue;
                    }

                    componentWarnings.Add(
                        "Removed scenario-local shared trigger '" +
                        OfficeFireSceneHierarchyBuilder.FullPath(child) +
                        "' (use 04_SharedInteractions/EvacuationTriggers).");
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        private static void MoveSharedTriggersToFolder(
            Transform fireModulesRoot,
            Transform evacuationTriggers,
            List<string> created,
            List<string> reused)
        {
            ScenarioTriggerVolume[] volumes = fireModulesRoot.GetComponentsInChildren<ScenarioTriggerVolume>(true);
            for (int i = 0; i < volumes.Length; i++)
            {
                ScenarioTriggerVolume volume = volumes[i];
                if (volume == null)
                {
                    continue;
                }

                Transform triggerTransform = volume.transform;
                if (triggerTransform.IsChildOf(evacuationTriggers))
                {
                    reused.Add(OfficeFireSceneHierarchyBuilder.FullPath(triggerTransform));
                    continue;
                }

                if (!IsSharedEvacuationTriggerName(triggerTransform.name)
                    && !IsSharedEvacuationActionId(ReadActionId(volume)))
                {
                    continue;
                }

                Undo.SetTransformParent(triggerTransform, evacuationTriggers, "Office Fire: Move shared evacuation trigger");
                triggerTransform.SetParent(evacuationTriggers, true);
                reused.Add(OfficeFireSceneHierarchyBuilder.FullPath(triggerTransform));
            }

            Transform legacyGeneral = fireModulesRoot.Find("General");
            if (legacyGeneral == null)
            {
                return;
            }

            for (int c = legacyGeneral.childCount - 1; c >= 0; c--)
            {
                Transform child = legacyGeneral.GetChild(c);
                if (child == null || !IsSharedEvacuationTriggerName(child.name))
                {
                    continue;
                }

                Undo.SetTransformParent(child, evacuationTriggers, "Office Fire: Move shared evacuation trigger");
                child.SetParent(evacuationTriggers, true);
                reused.Add(OfficeFireSceneHierarchyBuilder.FullPath(child));
            }
        }

        private static string ReadActionId(ScenarioTriggerVolume volume)
        {
            SerializedObject so = new SerializedObject(volume);
            SerializedProperty actionIdProp = so.FindProperty("actionId");
            return actionIdProp != null ? actionIdProp.stringValue : string.Empty;
        }

        private static void EnsureMinimumSharedTriggers(
            Transform evacuationTriggers,
            List<string> created,
            List<string> reused,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            EnsureSharedTrigger(
                evacuationTriggers,
                "Trigger_ReachedExitDoor",
                OfficeFireSharedScenarioActions.ReachedExitDoor,
                created,
                reused,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            EnsureSharedTrigger(
                evacuationTriggers,
                "Trigger_AssemblyArea",
                OfficeFireSharedScenarioActions.ReachAssemblyArea,
                created,
                reused,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            EnsureSharedTrigger(
                evacuationTriggers,
                "Trigger_Elevator_A",
                OfficeFireSharedScenarioActions.ElevatorProximity,
                created,
                reused,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            EnsureSharedTrigger(
                evacuationTriggers,
                "Trigger_Elevator_B",
                OfficeFireSharedScenarioActions.ElevatorProximity,
                created,
                reused,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
        }

        private static void EnsureSharedTrigger(
            Transform evacuationTriggers,
            string triggerName,
            string actionId,
            List<string> created,
            List<string> reused,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            Transform existing = FindSharedTrigger(evacuationTriggers, triggerName, actionId);
            if (existing != null)
            {
                reused.Add(OfficeFireSceneHierarchyBuilder.FullPath(existing));
                WireSharedTrigger(existing, actionId, componentsAdded, componentsAlreadyPresent, componentWarnings);
                return;
            }

            Transform triggerTransform = OfficeFireSceneHierarchyBuilder.EnsureChild(
                evacuationTriggers,
                triggerName,
                created,
                reused);
            WireSharedTrigger(triggerTransform, actionId, componentsAdded, componentsAlreadyPresent, componentWarnings);
        }

        private static Transform FindSharedTrigger(Transform evacuationTriggers, string triggerName, string actionId)
        {
            Transform direct = evacuationTriggers.Find(triggerName);
            if (direct != null)
            {
                return direct;
            }

            ScenarioTriggerVolume[] volumes = evacuationTriggers.GetComponentsInChildren<ScenarioTriggerVolume>(true);
            for (int i = 0; i < volumes.Length; i++)
            {
                ScenarioTriggerVolume volume = volumes[i];
                if (volume == null)
                {
                    continue;
                }

                if (ReadActionId(volume) != actionId)
                {
                    continue;
                }

                if (triggerName.StartsWith("Trigger_Elevator_"))
                {
                    if (volume.name == triggerName)
                    {
                        return volume.transform;
                    }

                    continue;
                }

                return volume.transform;
            }

            return null;
        }

        private static void RewireSharedTriggers(
            Transform evacuationTriggers,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            ScenarioTriggerVolume[] volumes = evacuationTriggers.GetComponentsInChildren<ScenarioTriggerVolume>(true);
            for (int i = 0; i < volumes.Length; i++)
            {
                ScenarioTriggerVolume volume = volumes[i];
                if (volume == null || !IsSharedEvacuationTriggerName(volume.name))
                {
                    continue;
                }

                string actionId = ReadActionId(volume);
                if (string.IsNullOrEmpty(actionId))
                {
                    actionId = ResolveActionIdFromName(volume.name);
                }

                WireSharedTrigger(volume.transform, actionId, componentsAdded, componentsAlreadyPresent, componentWarnings);
            }
        }

        private static string ResolveActionIdFromName(string objectName)
        {
            if (objectName.StartsWith("Trigger_ReachedExitDoor"))
            {
                return OfficeFireSharedScenarioActions.ReachedExitDoor;
            }

            if (objectName.StartsWith("Trigger_AssemblyArea"))
            {
                return OfficeFireSharedScenarioActions.ReachAssemblyArea;
            }

            if (objectName.StartsWith("Trigger_Elevator_"))
            {
                return OfficeFireSharedScenarioActions.ElevatorProximity;
            }

            return string.Empty;
        }

        private static void WireSharedTrigger(
            Transform host,
            string actionId,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            if (host == null)
            {
                return;
            }

            BoxCollider box = host.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(host.gameObject);
                componentsAdded.Add($"BoxCollider on '{OfficeFireSceneHierarchyBuilder.FullPath(host)}'");
            }

            box.isTrigger = true;
            if (box.size.sqrMagnitude < 0.01f)
            {
                box.size = new Vector3(2f, 2f, 2f);
            }

            ScenarioTriggerVolume trigger = OfficeFireSceneHierarchyBuilder.TryAddComponent<ScenarioTriggerVolume>(
                host.gameObject,
                "ScenarioTriggerVolume",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            if (trigger == null)
            {
                return;
            }

            Undo.RecordObject(trigger, "Office Fire: Wire shared ScenarioTriggerVolume");
            SerializedObject so = new SerializedObject(trigger);
            SerializedProperty actionIdProp = so.FindProperty("actionId");
            if (actionIdProp != null && !string.IsNullOrEmpty(actionId))
            {
                actionIdProp.stringValue = actionId;
            }

            SerializedProperty targetProp = so.FindProperty("targetScenario");
            if (targetProp != null)
            {
                targetProp.objectReferenceValue = null;
            }

            SerializedProperty layerProp = so.FindProperty("playerLayer");
            if (layerProp != null)
            {
                int playerMask = LayerMask.GetMask("Player");
                layerProp.intValue = playerMask != 0 ? playerMask : ~0;
            }

            so.ApplyModifiedProperties();
        }

        private static void LogSummary(
            string scenePath,
            List<string> created,
            List<string> reused,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Office Fire Scene] Shared evacuation triggers pass complete (" + scenePath + ").");
            sb.AppendLine("--- Created GameObjects ---");
            OfficeFireSceneHierarchyBuilder.AppendLines(sb, created);
            sb.AppendLine("--- Reused / moved GameObjects ---");
            OfficeFireSceneHierarchyBuilder.AppendLines(sb, reused);
            sb.AppendLine("--- Components added (this run) ---");
            OfficeFireSceneHierarchyBuilder.AppendLines(sb, componentsAdded);
            sb.AppendLine("--- Components already present (skipped add) ---");
            OfficeFireSceneHierarchyBuilder.AppendLines(sb, componentsAlreadyPresent);

            Debug.Log(sb.ToString());

            foreach (string warning in componentWarnings)
            {
                if (!string.IsNullOrEmpty(warning))
                {
                    Debug.LogWarning("[Office Fire Scene] " + warning);
                }
            }
        }
    }
}
