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
    /// Builds the Archive Room scenario hierarchy under <c>03_Scenarios</c>, mirroring the Kitchen/Cafe layout.
    /// </summary>
    public static class OfficeFireArchiveSceneHierarchyBuilder
    {
        private const string RootName = "======FireModules======";
        private const string MenuPath = "Tools/Woi/Office Fire/Scene/Create Archive Room Hierarchy";
        private const string BatchMenuPath = "Tools/Woi/Office Fire/Scene/Create Archive Room In All Scenario Scenes";

        private static readonly string[] ScenarioScenePaths =
        {
            "Assets/Project/Scenes/Office.unity",
            "Assets/Project/Scenes/FireModule/FireModule_Office.unity",
        };

        [MenuItem(MenuPath, false, 21)]
        private static void CreateArchiveHierarchyActiveScene()
        {
            CreateArchiveHierarchyInScene(SceneManager.GetActiveScene());
        }

        [MenuItem(BatchMenuPath, false, 22)]
        private static void CreateArchiveHierarchyAllScenarioScenes()
        {
            string previousScene = SceneManager.GetActiveScene().path;

            for (int i = 0; i < ScenarioScenePaths.Length; i++)
            {
                string path = ScenarioScenePaths[i];
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning("[Office Fire Scene] Scene not found, skipped: " + path);
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                CreateArchiveHierarchyInScene(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (!string.IsNullOrEmpty(previousScene))
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }
        }

        /// <summary>
        /// Invoked from Unity batch mode: -executeMethod Woi.OfficeFire.Editor.OfficeFireArchiveSceneHierarchyBuilder.BatchCreateArchiveHierarchy
        /// </summary>
        public static void BatchCreateArchiveHierarchy()
        {
            CreateArchiveHierarchyAllScenarioScenes();
        }

        private static void CreateArchiveHierarchyInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Office Fire Scene] Scene is not valid or not loaded: " + scene.path);
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Archive Room Hierarchy");
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
                return;
            }

            Transform t00 = OfficeFireSceneHierarchyBuilder.EnsureChild(root, "00_Runtime", created, reused);
            Transform t01 = OfficeFireSceneHierarchyBuilder.EnsureChild(root, "01_Player", created, reused);
            Transform t03 = OfficeFireSceneHierarchyBuilder.EnsureChild(root, "03_Scenarios", created, reused);
            Transform t04 = OfficeFireSceneHierarchyBuilder.EnsureChild(root, "04_SharedInteractions", created, reused);
            Transform t05 = OfficeFireSceneHierarchyBuilder.EnsureChild(root, "05_UI", created, reused);

            Transform tPcInteractor = OfficeFireSceneHierarchyBuilder.EnsureChild(t04, "PCSelectableInteractor", created, reused);
            Transform tPcHover = OfficeFireSceneHierarchyBuilder.EnsureChild(t04, "PCHoverInteractor", created, reused);
            OfficeFireSceneHierarchyBuilder.EnsureChild(t05, "Popup", created, reused);
            OfficeFireSceneHierarchyBuilder.EnsureChild(t05, "Objective", created, reused);
            OfficeFireSceneHierarchyBuilder.EnsureChild(t05, "Report", created, reused);

            PCSelectableInteractor pcSelectable = OfficeFireSceneHierarchyBuilder.TryAddComponent<PCSelectableInteractor>(
                tPcInteractor.gameObject,
                "PCSelectableInteractor",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            WirePcSelectableInteractor(pcSelectable, componentWarnings);

            OfficeFireSceneHierarchyBuilder.TryAddComponent<PCHoverInteractor>(
                tPcHover.gameObject,
                "PCHoverInteractor",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            Transform spawnPoints = OfficeFireSceneHierarchyBuilder.EnsureChild(t01, "SpawnPoints", created, reused);
            Transform spawnArchive = OfficeFireSceneHierarchyBuilder.EnsureChild(spawnPoints, "Spawn_ArchiveRoom", created, reused);

            Transform archiveRoot = OfficeFireSceneHierarchyBuilder.EnsureChild(t03, "ArchiveRoom", created, reused);
            Transform controllerFolder = OfficeFireSceneHierarchyBuilder.EnsureChild(archiveRoot, "Controller", created, reused);
            Transform effectsFolder = OfficeFireSceneHierarchyBuilder.EnsureChild(archiveRoot, "Effects", created, reused);
            Transform interactables = OfficeFireSceneHierarchyBuilder.EnsureChild(archiveRoot, "Interactables", created, reused);
            Transform triggers = OfficeFireSceneHierarchyBuilder.EnsureChild(archiveRoot, "Triggers", created, reused);
            Transform guidance = OfficeFireSceneHierarchyBuilder.EnsureChild(archiveRoot, "Guidance", created, reused);
            Transform evacuationFolder = OfficeFireSceneHierarchyBuilder.EnsureChild(archiveRoot, "Evacuation", created, reused);
            Transform evacuationPaths = OfficeFireSceneHierarchyBuilder.EnsureChild(evacuationFolder, "Paths", created, reused);
            Transform evacuationNpcs = OfficeFireSceneHierarchyBuilder.EnsureChild(evacuationFolder, "Npcs", created, reused);

            OfficeFireSceneHierarchyBuilder.EnsureChild(effectsFolder, "Smoke", created, reused);
            Transform fireGrowthHost = OfficeFireSceneHierarchyBuilder.EnsureChild(effectsFolder, "FireGrowth", created, reused);
            OfficeFireSceneHierarchyBuilder.TryAddComponent<ArchiveRoomFireGrowthController>(
                fireGrowthHost.gameObject,
                "ArchiveRoomFireGrowthController",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            WireSelectable(
                OfficeFireSceneHierarchyBuilder.EnsureChild(interactables, "SmokeObservation", created, reused),
                null,
                ArchiveRoomScenarioController.Actions.NoticeSmoke,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            WireDoor(
                OfficeFireSceneHierarchyBuilder.EnsureChild(interactables, "ArchiveDoor", created, reused),
                null,
                ArchiveRoomScenarioController.Actions.OpenArchiveDoor,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            WireSelectable(
                OfficeFireSceneHierarchyBuilder.EnsureChild(interactables, "WaterSource", created, reused),
                null,
                ArchiveRoomScenarioController.Actions.UseWater,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            WireAlarm(
                OfficeFireSceneHierarchyBuilder.EnsureChild(interactables, "AlarmButton", created, reused),
                null,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            WireSelectable(
                OfficeFireSceneHierarchyBuilder.EnsureChild(interactables, "ExtinguisherPickup", created, reused),
                null,
                ArchiveRoomScenarioController.Actions.GrabExtinguisher,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            WireSelectable(
                OfficeFireSceneHierarchyBuilder.EnsureChild(interactables, "ExtinguisherUse", created, reused),
                null,
                ArchiveRoomScenarioController.Actions.UseExtinguisher,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            WirePowerCut(
                OfficeFireSceneHierarchyBuilder.EnsureChild(interactables, "PowerPlug", created, reused),
                null,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            WireTrigger(
                OfficeFireSceneHierarchyBuilder.EnsureChild(triggers, "Trigger_ExitArchiveRoom", created, reused),
                null,
                ArchiveRoomScenarioController.Actions.ExitArchiveRoom,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            WireTrigger(
                OfficeFireSceneHierarchyBuilder.EnsureChild(triggers, "Trigger_AssemblyArea", created, reused),
                null,
                ArchiveRoomScenarioController.Actions.ReachAssemblyArea,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            OfficeFireSceneHierarchyBuilder.EnsureChild(guidance, "EmergencyLights", created, reused);
            OfficeFireSceneHierarchyBuilder.EnsureChild(guidance, "ExitSigns", created, reused);

            Transform tBootstrap = OfficeFireSceneHierarchyBuilder.EnsureChild(t00, "OfficeFireScenarioBootstrapper", created, reused);
            Transform tInit = OfficeFireSceneHierarchyBuilder.EnsureChild(t00, "OfficeFirePlayerInitializer", created, reused);
            Transform tArchiveController = OfficeFireSceneHierarchyBuilder.EnsureChild(
                controllerFolder,
                "ArchiveRoomScenarioController",
                created,
                reused);

            ArchiveRoomScenarioController archiveController = OfficeFireSceneHierarchyBuilder.TryAddComponent<ArchiveRoomScenarioController>(
                tArchiveController.gameObject,
                "ArchiveRoomScenarioController",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            OfficeFireSceneHierarchyBuilder.TryAddComponent<OfficeFireArchiveFireExtinguishBridge>(
                tArchiveController.gameObject,
                "OfficeFireArchiveFireExtinguishBridge",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            OfficeFireSceneHierarchyBuilder.TryAddComponent<OfficeFireArchiveExtinguisherGrabScenarioBridge>(
                tArchiveController.gameObject,
                "OfficeFireArchiveExtinguisherGrabScenarioBridge",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            // Breaker gate is opt-in only (enableBreakerGate on the component). Archive flow uses alarm → extinguish.

            OfficeFireSceneHierarchyBuilder.TryAddComponent<OfficeFireScenarioBootstrapper>(
                tBootstrap.gameObject,
                "OfficeFireScenarioBootstrapper",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            OfficeFireSceneHierarchyBuilder.TryAddComponent<OfficeFirePlayerInitializer>(
                tInit.gameObject,
                "OfficeFirePlayerInitializer",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            EvacuationNpcDirector evacuationDirector = WireEvacuation(
                evacuationFolder,
                evacuationPaths,
                evacuationNpcs,
                created,
                reused,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            WireArchiveScenario(archiveController, archiveRoot, evacuationDirector, componentWarnings);
            RewireArchiveInteractions(archiveRoot, archiveController, componentWarnings);

            OfficeFireSceneHierarchyBuilder.WireSpawnPoint(
                tInit.gameObject,
                spawnArchive,
                OfficeFireScenarioId.ArchiveRoom,
                componentWarnings);
            OfficeFireSceneHierarchyBuilder.WireBootstrapperController(
                tBootstrap.gameObject,
                tInit.gameObject,
                archiveController != null ? archiveController.gameObject : null,
                componentWarnings);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);

            LogSummary(scene.path, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
        }

        private static EvacuationNpcDirector WireEvacuation(
            Transform evacuationRoot,
            Transform pathsFolder,
            Transform npcsFolder,
            List<string> created,
            List<string> reused,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            if (evacuationRoot == null)
            {
                return null;
            }

            EvacuationNpcDirector director = OfficeFireSceneHierarchyBuilder.TryAddComponent<EvacuationNpcDirector>(
                evacuationRoot.gameObject,
                "EvacuationNpcDirector",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            if (pathsFolder == null || npcsFolder == null)
            {
                return director;
            }

            int configured = OfficeFireArchiveEvacuationNpcBuilder.SetupEvacuationNpcsInScene(
                evacuationRoot.gameObject.scene,
                OfficeFireArchiveEvacuationNpcBuilder.DefaultNpcCount);

            if (configured == 0)
            {
                componentWarnings.Add(
                    "Evacuation: Could not auto-create NPC/path pairs. Use Woi/Office Fire/Archive/Setup Evacuation NPCs.");
            }

            return director;
        }

        private static void WireArchiveScenario(
            ArchiveRoomScenarioController archiveController,
            Transform archiveRoot,
            EvacuationNpcDirector evacuationDirector,
            List<string> componentWarnings)
        {
            if (archiveController == null || archiveRoot == null)
            {
                return;
            }

            Undo.RecordObject(archiveController, "Office Fire: Wire ArchiveRoomScenarioController");
            SerializedObject so = new SerializedObject(archiveController);
            SerializedProperty scenarioRootProp = so.FindProperty("scenarioRoot");
            if (scenarioRootProp != null)
            {
                scenarioRootProp.objectReferenceValue = archiveRoot.gameObject;
            }
            else
            {
                componentWarnings.Add("ArchiveRoomScenarioController: serialized field 'scenarioRoot' not found.");
            }

            SerializedProperty evacuationDirectorProp = so.FindProperty("evacuationNpcDirector");
            if (evacuationDirectorProp != null && evacuationDirector != null)
            {
                evacuationDirectorProp.objectReferenceValue = evacuationDirector;
            }
            else if (evacuationDirectorProp == null)
            {
                componentWarnings.Add("ArchiveRoomScenarioController: serialized field 'evacuationNpcDirector' not found.");
            }

            ArchiveRoomFireGrowthController fireGrowth =
                archiveRoot.GetComponentInChildren<ArchiveRoomFireGrowthController>(true);
            SerializedProperty fireGrowthProp = so.FindProperty("fireGrowthController");
            if (fireGrowthProp != null)
            {
                fireGrowthProp.objectReferenceValue = fireGrowth;
            }
            else
            {
                componentWarnings.Add("ArchiveRoomScenarioController: serialized field 'fireGrowthController' not found.");
            }

            so.ApplyModifiedProperties();
        }

        private static void RewireArchiveInteractions(
            Transform archiveRoot,
            ArchiveRoomScenarioController archiveController,
            List<string> componentWarnings)
        {
            if (archiveRoot == null || archiveController == null)
            {
                return;
            }

            SelectableScenarioAction[] selectables = archiveRoot.GetComponentsInChildren<SelectableScenarioAction>(true);
            for (int i = 0; i < selectables.Length; i++)
            {
                WireSelectableTarget(selectables[i], archiveController, componentWarnings);
            }

            DoorScenarioAction[] doorActions = archiveRoot.GetComponentsInChildren<DoorScenarioAction>(true);
            for (int i = 0; i < doorActions.Length; i++)
            {
                WireDoorTarget(doorActions[i], archiveController, componentWarnings);
            }

            ScenarioTriggerVolume[] triggerVolumes = archiveRoot.GetComponentsInChildren<ScenarioTriggerVolume>(true);
            for (int i = 0; i < triggerVolumes.Length; i++)
            {
                WireTriggerTarget(triggerVolumes[i], archiveController, componentWarnings);
            }

            Alarm[] alarms = archiveRoot.GetComponentsInChildren<Alarm>(true);
            for (int i = 0; i < alarms.Length; i++)
            {
                WireAlarmTarget(alarms[i], archiveController, componentWarnings);
            }

            ArchivePowerCutInteractable[] powerCutInteractables =
                archiveRoot.GetComponentsInChildren<ArchivePowerCutInteractable>(true);
            OfficeFireArchiveElectricalSafetySetup electricalSafety =
                archiveController.GetComponent<OfficeFireArchiveElectricalSafetySetup>();
            for (int i = 0; i < powerCutInteractables.Length; i++)
            {
                WirePowerCutTarget(powerCutInteractables[i], archiveController, electricalSafety, componentWarnings);
            }
        }

        private static void WireDoor(
            Transform host,
            OfficeFireScenarioController controller,
            string actionId,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            if (host == null)
            {
                return;
            }

            SelectableScenarioAction legacyClickAction = host.GetComponent<SelectableScenarioAction>();
            if (legacyClickAction != null)
            {
                Undo.DestroyObjectImmediate(legacyClickAction);
            }

            OfficeFireSceneHierarchyBuilder.TryAddComponent<SelectableDoor>(
                host.gameObject,
                "SelectableDoor",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            DoorScenarioAction doorAction = OfficeFireSceneHierarchyBuilder.TryAddComponent<DoorScenarioAction>(
                host.gameObject,
                "DoorScenarioAction",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            if (doorAction == null)
            {
                return;
            }

            Undo.RecordObject(doorAction, "Office Fire: Wire DoorScenarioAction");
            SerializedObject so = new SerializedObject(doorAction);
            SerializedProperty actionIdProp = so.FindProperty("actionId");
            if (actionIdProp != null)
            {
                actionIdProp.stringValue = actionId;
            }
            else
            {
                componentWarnings.Add("DoorScenarioAction: serialized field 'actionId' not found.");
            }

            if (controller != null)
            {
                SerializedProperty targetProp = so.FindProperty("targetScenario");
                if (targetProp != null)
                {
                    targetProp.objectReferenceValue = controller;
                }
            }

            so.ApplyModifiedProperties();
        }

        private static void WireDoorTarget(
            DoorScenarioAction doorAction,
            OfficeFireScenarioController controller,
            List<string> componentWarnings)
        {
            if (doorAction == null || controller == null)
            {
                return;
            }

            Undo.RecordObject(doorAction, "Office Fire: Wire DoorScenarioAction target");
            SerializedObject so = new SerializedObject(doorAction);
            SerializedProperty targetProp = so.FindProperty("targetScenario");
            if (targetProp != null)
            {
                targetProp.objectReferenceValue = controller;
            }
            else
            {
                componentWarnings.Add("DoorScenarioAction: serialized field 'targetScenario' not found.");
            }

            so.ApplyModifiedProperties();
        }

        private static void WirePcSelectableInteractor(
            PCSelectableInteractor interactor,
            List<string> componentWarnings)
        {
            if (interactor == null)
            {
                return;
            }

            Undo.RecordObject(interactor, "Office Fire: Wire PCSelectableInteractor");
            SerializedObject so = new SerializedObject(interactor);

            SerializedProperty interactProp = so.FindProperty("interactInputEvent");
            if (interactProp != null)
            {
                const string interactInputAssetPath =
                    "Assets/Project/OfficeFire/ScriptableObjects/Events/onInteractInput.asset";

                ScriptableObject interactInput = AssetDatabase.LoadAssetAtPath<ScriptableObject>(interactInputAssetPath);
                if (interactInput == null)
                {
                    const string packageInteractInputAssetPath =
                        "Packages/com.woi.module.fire/Runtime/InputSystem/InputsSO/InputEvents/onInteractInput.asset";
                    interactInput = AssetDatabase.LoadAssetAtPath<ScriptableObject>(packageInteractInputAssetPath);
                }

                if (interactInput != null)
                {
                    interactProp.objectReferenceValue = interactInput;
                }
                else
                {
                    componentWarnings.Add("PCSelectableInteractor: onInteractInput asset not found.");
                }
            }
            else
            {
                componentWarnings.Add("PCSelectableInteractor: serialized field 'interactInputEvent' not found.");
            }

            so.ApplyModifiedProperties();
        }

        private static void WireAlarm(
            Transform host,
            ArchiveRoomScenarioController controller,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            if (host == null)
            {
                return;
            }

            SelectableScenarioAction legacyAction = host.GetComponent<SelectableScenarioAction>();
            if (legacyAction != null)
            {
                Undo.DestroyObjectImmediate(legacyAction);
            }

            OfficeFireSceneHierarchyBuilder.TryAddComponent<Outline>(
                host.gameObject,
                "Outline",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            Alarm alarm = OfficeFireSceneHierarchyBuilder.TryAddComponent<Alarm>(
                host.gameObject,
                "Alarm",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            if (alarm == null)
            {
                return;
            }

            Undo.RecordObject(alarm, "Office Fire: Wire Alarm");
            SerializedObject so = new SerializedObject(alarm);

            SerializedProperty actionIdProp = so.FindProperty("actionId");
            if (actionIdProp != null)
            {
                actionIdProp.stringValue = ArchiveRoomScenarioController.Actions.PressAlarm;
            }
            else
            {
                componentWarnings.Add("Alarm: serialized field 'actionId' not found.");
            }

            SerializedProperty alarmPressedProp = so.FindProperty("alarmPressed");
            if (alarmPressedProp != null)
            {
                const string alarmPressedAssetPath =
                    "Assets/Project/OfficeFire/ScriptableObjects/Events/onAlarmPressed.asset";
                ScriptableObject alarmPressed = AssetDatabase.LoadAssetAtPath<ScriptableObject>(alarmPressedAssetPath);
                if (alarmPressed != null)
                {
                    alarmPressedProp.objectReferenceValue = alarmPressed;
                }
                else
                {
                    componentWarnings.Add("Alarm: onAlarmPressed asset not found at " + alarmPressedAssetPath);
                }
            }
            else
            {
                componentWarnings.Add("Alarm: serialized field 'alarmPressed' not found.");
            }

            if (controller != null)
            {
                SerializedProperty targetProp = so.FindProperty("targetScenario");
                if (targetProp != null)
                {
                    targetProp.objectReferenceValue = controller;
                }
            }

            so.ApplyModifiedProperties();
        }

        private static void WireAlarmTarget(
            Alarm alarm,
            ArchiveRoomScenarioController controller,
            List<string> componentWarnings)
        {
            if (alarm == null || controller == null)
            {
                return;
            }

            Undo.RecordObject(alarm, "Office Fire: Wire Alarm target");
            SerializedObject so = new SerializedObject(alarm);
            SerializedProperty targetProp = so.FindProperty("targetScenario");
            if (targetProp != null)
            {
                targetProp.objectReferenceValue = controller;
            }
            else
            {
                componentWarnings.Add("Alarm: serialized field 'targetScenario' not found.");
            }

            so.ApplyModifiedProperties();
        }

        private static void WirePowerCut(
            Transform host,
            ArchiveRoomScenarioController controller,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            if (host == null)
            {
                return;
            }

            SelectableScenarioAction legacyAction = host.GetComponent<SelectableScenarioAction>();
            if (legacyAction != null)
            {
                Undo.DestroyObjectImmediate(legacyAction);
            }

            ArchivePowerCutInteractable powerCut = OfficeFireSceneHierarchyBuilder.TryAddComponent<ArchivePowerCutInteractable>(
                host.gameObject,
                "ArchivePowerCutInteractable",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            if (powerCut == null || controller == null)
            {
                return;
            }

            WirePowerCutTarget(
                powerCut,
                controller,
                controller.GetComponent<OfficeFireArchiveElectricalSafetySetup>(),
                componentWarnings);
        }

        private static void WirePowerCutTarget(
            ArchivePowerCutInteractable powerCut,
            ArchiveRoomScenarioController controller,
            OfficeFireArchiveElectricalSafetySetup electricalSafety,
            List<string> componentWarnings)
        {
            if (powerCut == null || controller == null)
            {
                return;
            }

            Undo.RecordObject(powerCut, "Office Fire: Wire ArchivePowerCutInteractable");
            SerializedObject so = new SerializedObject(powerCut);
            SerializedProperty scenarioProp = so.FindProperty("targetScenario");
            if (scenarioProp != null)
            {
                scenarioProp.objectReferenceValue = controller;
            }
            else
            {
                componentWarnings.Add("ArchivePowerCutInteractable: serialized field 'targetScenario' not found.");
            }

            SerializedProperty safetyProp = so.FindProperty("electricalSafetySetup");
            if (safetyProp != null)
            {
                safetyProp.objectReferenceValue = electricalSafety;
            }

            so.ApplyModifiedProperties();
        }

        private static void WireSelectable(
            Transform host,
            ArchiveRoomScenarioController controller,
            string actionId,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            if (host == null)
            {
                return;
            }

            SelectableScenarioAction action = OfficeFireSceneHierarchyBuilder.TryAddComponent<SelectableScenarioAction>(
                host.gameObject,
                "SelectableScenarioAction",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            if (action == null)
            {
                return;
            }

            Undo.RecordObject(action, "Office Fire: Wire SelectableScenarioAction");
            SerializedObject so = new SerializedObject(action);
            SerializedProperty actionIdProp = so.FindProperty("actionId");
            if (actionIdProp != null)
            {
                actionIdProp.stringValue = actionId;
            }
            else
            {
                componentWarnings.Add("SelectableScenarioAction: serialized field 'actionId' not found.");
            }

            if (controller != null)
            {
                SerializedProperty targetProp = so.FindProperty("targetScenario");
                if (targetProp != null)
                {
                    targetProp.objectReferenceValue = controller;
                }
            }

            so.ApplyModifiedProperties();
        }

        private static void WireSelectableTarget(
            SelectableScenarioAction action,
            ArchiveRoomScenarioController controller,
            List<string> componentWarnings)
        {
            if (action == null || controller == null)
            {
                return;
            }

            Undo.RecordObject(action, "Office Fire: Wire SelectableScenarioAction target");
            SerializedObject so = new SerializedObject(action);
            SerializedProperty targetProp = so.FindProperty("targetScenario");
            if (targetProp != null)
            {
                targetProp.objectReferenceValue = controller;
            }
            else
            {
                componentWarnings.Add("SelectableScenarioAction: serialized field 'targetScenario' not found.");
            }

            so.ApplyModifiedProperties();
        }

        private static void WireTrigger(
            Transform host,
            ArchiveRoomScenarioController controller,
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
            box.size = new Vector3(2f, 2f, 2f);

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

            Undo.RecordObject(trigger, "Office Fire: Wire ScenarioTriggerVolume");
            SerializedObject so = new SerializedObject(trigger);
            SerializedProperty actionIdProp = so.FindProperty("actionId");
            if (actionIdProp != null)
            {
                actionIdProp.stringValue = actionId;
            }
            else
            {
                componentWarnings.Add("ScenarioTriggerVolume: serialized field 'actionId' not found.");
            }

            SerializedProperty layerProp = so.FindProperty("playerLayer");
            if (layerProp != null)
            {
                int playerMask = LayerMask.GetMask("Player");
                layerProp.intValue = playerMask != 0 ? playerMask : ~0;
            }

            if (controller != null)
            {
                SerializedProperty targetProp = so.FindProperty("targetScenario");
                if (targetProp != null)
                {
                    targetProp.objectReferenceValue = controller;
                }
            }

            so.ApplyModifiedProperties();
        }

        private static void WireTriggerTarget(
            ScenarioTriggerVolume trigger,
            ArchiveRoomScenarioController controller,
            List<string> componentWarnings)
        {
            if (trigger == null || controller == null)
            {
                return;
            }

            Undo.RecordObject(trigger, "Office Fire: Wire ScenarioTriggerVolume target");
            SerializedObject so = new SerializedObject(trigger);
            SerializedProperty targetProp = so.FindProperty("targetScenario");
            if (targetProp != null)
            {
                targetProp.objectReferenceValue = controller;
            }
            else
            {
                componentWarnings.Add("ScenarioTriggerVolume: serialized field 'targetScenario' not found.");
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
            sb.AppendLine("[Office Fire Scene] Archive Room hierarchy pass complete.");
            sb.AppendLine("Scene: " + scenePath);
            sb.AppendLine("--- Created GameObjects ---");
            OfficeFireSceneHierarchyBuilder.AppendLines(sb, created);
            sb.AppendLine("--- Reused GameObjects ---");
            OfficeFireSceneHierarchyBuilder.AppendLines(sb, reused);
            sb.AppendLine("--- Components added (this run) ---");
            OfficeFireSceneHierarchyBuilder.AppendLines(sb, componentsAdded);
            sb.AppendLine("--- Components already present (skipped add) ---");
            OfficeFireSceneHierarchyBuilder.AppendLines(sb, componentsAlreadyPresent);

            Debug.Log(sb.ToString());

            foreach (string w in componentWarnings)
            {
                if (!string.IsNullOrEmpty(w))
                {
                    Debug.LogWarning("[Office Fire Scene] " + w);
                }
            }
        }
    }
}
