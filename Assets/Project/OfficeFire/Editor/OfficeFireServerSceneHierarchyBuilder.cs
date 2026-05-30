using System.Collections.Generic;
using System.Text;
using FireExtinguisher.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Woi.Equipment;
using Woi.OfficeFire;

namespace Woi.OfficeFire.Editor
{
    /// <summary>
    /// Ensures Server Room scenario wiring matches Archive Room (triggers, bridges, extinguisher pickup).
    /// </summary>
    public static class OfficeFireServerSceneHierarchyBuilder
    {
        private const string RootName = "======FireModules======";
        private const string MenuPath = "Tools/Woi/Office Fire/Scene/Ensure Server Room Setup";

        [MenuItem(MenuPath, false, 24)]
        private static void EnsureServerRoomSetupActiveScene()
        {
            EnsureServerRoomSetupInScene(SceneManager.GetActiveScene());
        }

        [MenuItem(MenuPath, true, 24)]
        private static bool EnsureServerRoomSetupActiveSceneValidate()
        {
            return !Application.isPlaying;
        }

        public static void EnsureServerRoomSetupInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Office Fire Scene] Scene is not valid or not loaded: " + scene.path);
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Ensure Server Room Setup");
            int undoGroup = Undo.GetCurrentGroup();

            var created = new List<string>();
            var reused = new List<string>();
            var componentsAdded = new List<string>();
            var componentsAlreadyPresent = new List<string>();
            var componentWarnings = new List<string>();

            Transform serverRoot = FindServerRoomRoot(scene);
            if (serverRoot == null)
            {
                Debug.LogError("[Office Fire Scene] ServerRoom not found under 03_Scenarios.", null);
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            ServerRoomScenarioController controller =
                serverRoot.GetComponentInChildren<ServerRoomScenarioController>(true);
            if (controller == null)
            {
                componentWarnings.Add("ServerRoomScenarioController not found.");
            }

            Transform triggers = EnsureChild(serverRoot, "Triggers", created, reused);
            Transform interactables = EnsureChild(serverRoot, "Interactables", created, reused);
            Transform evacuation = EnsureChild(serverRoot, "Evacuation", created, reused);

            if (controller != null)
            {
                EnsureControllerComponents(controller, componentsAdded, componentsAlreadyPresent, componentWarnings);
                WireServerScenario(
                    controller,
                    serverRoot,
                    evacuation,
                    created,
                    reused,
                    componentWarnings);
                EnsureServerTriggers(triggers, controller, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
                EnsureServerInteractables(interactables, controller, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
                FixSuppressionAlarms(serverRoot, controller, componentWarnings);
                WireExtinguisherWallPickup(serverRoot, componentWarnings, componentsAdded, componentsAlreadyPresent);
                RewireServerInteractions(serverRoot, controller, componentWarnings);
            }

            OfficeFireSharedEvacuationTriggersBuilder.EnsureSharedEvacuationTriggersInScene(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            LogSummary(scene.path, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
        }

        private static Transform FindServerRoomRoot(Scene scene)
        {
            GameObject fireModulesRoot = FindSceneRootByName(scene, RootName);
            if (fireModulesRoot == null)
            {
                return null;
            }

            Transform scenarios = fireModulesRoot.transform.Find("03_Scenarios");
            return scenarios != null ? scenarios.Find("ServerRoom") : null;
        }

        private static void EnsureControllerComponents(
            ServerRoomScenarioController controller,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            OfficeFireSceneHierarchyBuilder.TryAddComponent<OfficeFireServerFireExtinguishBridge>(
                controller.gameObject,
                "OfficeFireServerFireExtinguishBridge",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            OfficeFireSceneHierarchyBuilder.TryAddComponent<OfficeFireServerExtinguisherGrabScenarioBridge>(
                controller.gameObject,
                "OfficeFireServerExtinguisherGrabScenarioBridge",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            OfficeFireSceneHierarchyBuilder.TryAddComponent<OfficeFireServerExtinguisherHudBridge>(
                controller.gameObject,
                "OfficeFireServerExtinguisherHudBridge",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            OfficeFireServerFireExtinguishBridge extinguishBridge =
                controller.GetComponent<OfficeFireServerFireExtinguishBridge>();
            if (extinguishBridge != null)
            {
                Undo.RecordObject(extinguishBridge, "Office Fire: Wire Server FireExtinguishBridge");
                SerializedObject bridgeSo = new SerializedObject(extinguishBridge);
                SerializedProperty scenarioProp = bridgeSo.FindProperty("scenario");
                if (scenarioProp != null)
                {
                    scenarioProp.objectReferenceValue = controller;
                }

                FireSource fireSource = controller.GetComponentInParent<Transform>() != null
                    ? controller.transform.root.GetComponentInChildren<FireSource>(true)
                    : null;
                if (fireSource == null && controller.ScenarioRoot != null)
                {
                    fireSource = controller.ScenarioRoot.GetComponentInChildren<FireSource>(true);
                }

                if (fireSource == null)
                {
                    fireSource = Object.FindFirstObjectByType<FireSource>(FindObjectsInactive.Include);
                }

                SerializedProperty fireSourceProp = bridgeSo.FindProperty("fireSource");
                if (fireSourceProp != null && fireSource != null)
                {
                    fireSourceProp.objectReferenceValue = fireSource;
                }

                bridgeSo.ApplyModifiedProperties();
            }
        }

        private static void WireServerScenario(
            ServerRoomScenarioController controller,
            Transform serverRoot,
            Transform evacuationRoot,
            List<string> created,
            List<string> reused,
            List<string> componentWarnings)
        {
            Undo.RecordObject(controller, "Office Fire: Wire ServerRoomScenarioController");
            SerializedObject so = new SerializedObject(controller);

            SerializedProperty scenarioRootProp = so.FindProperty("scenarioRoot");
            if (scenarioRootProp != null)
            {
                scenarioRootProp.objectReferenceValue = serverRoot.gameObject;
            }

            EvacuationNpcDirector director = evacuationRoot.GetComponent<EvacuationNpcDirector>();
            if (director == null)
            {
                director = evacuationRoot.gameObject.AddComponent<EvacuationNpcDirector>();
            }

            SerializedProperty evacuationProp = so.FindProperty("evacuationNpcDirector");
            if (evacuationProp != null)
            {
                evacuationProp.objectReferenceValue = director;
            }

            ScenarioFireGrowthController fireGrowth =
                controller.GetComponent<ScenarioFireGrowthController>()
                ?? serverRoot.GetComponentInChildren<ScenarioFireGrowthController>(true);
            SerializedProperty fireGrowthProp = so.FindProperty("fireGrowthController");
            if (fireGrowthProp != null && fireGrowth != null)
            {
                fireGrowthProp.objectReferenceValue = fireGrowth;
            }

            GameObject evacuationNpcsRoot = EnsureEvacuationNpcsRoot(evacuationRoot, created, reused);
            WireEvacuationStarted(controller, evacuationNpcsRoot, componentWarnings);
            WireVoicePresenter(controller, componentWarnings);
            so.ApplyModifiedProperties();
        }

        private static GameObject EnsureEvacuationNpcsRoot(
            Transform evacuationRoot,
            List<string> created,
            List<string> reused)
        {
            EnsureChild(evacuationRoot, "Paths", created, reused);
            Transform npcs = EnsureChild(evacuationRoot, "Npcs", created, reused);
            if (npcs.gameObject.activeSelf)
            {
                Undo.RecordObject(npcs.gameObject, "Office Fire: Deactivate Server evacuation Npcs");
                npcs.gameObject.SetActive(false);
            }

            return npcs.gameObject;
        }

        private static void WireEvacuationStarted(
            ServerRoomScenarioController controller,
            GameObject evacuationNpcsRoot,
            List<string> componentWarnings)
        {
            if (controller == null)
            {
                return;
            }

            if (evacuationNpcsRoot == null)
            {
                componentWarnings.Add("Server evacuation Npcs root missing — onEvacuationStarted not wired.");
                return;
            }

            Undo.RecordObject(controller, "Office Fire: Wire Server onEvacuationStarted");
            SerializedObject so = new SerializedObject(controller);
            SerializedProperty eventProp = so.FindProperty("onEvacuationStarted");
            if (eventProp == null)
            {
                componentWarnings.Add("ServerRoomScenarioController: onEvacuationStarted not found.");
                return;
            }

            SerializedProperty callsProp = eventProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (callsProp == null)
            {
                componentWarnings.Add("ServerRoomScenarioController: onEvacuationStarted calls not found.");
                return;
            }

            for (int i = callsProp.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty call = callsProp.GetArrayElementAtIndex(i);
                SerializedProperty target = call.FindPropertyRelative("m_Target");
                if (target != null && target.objectReferenceValue == evacuationNpcsRoot)
                {
                    so.ApplyModifiedProperties();
                    return;
                }
            }

            int index = callsProp.arraySize;
            callsProp.InsertArrayElementAtIndex(index);
            SerializedProperty newCall = callsProp.GetArrayElementAtIndex(index);
            newCall.FindPropertyRelative("m_Target").objectReferenceValue = evacuationNpcsRoot;
            newCall.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                "UnityEngine.GameObject, UnityEngine";
            newCall.FindPropertyRelative("m_MethodName").stringValue = "SetActive";
            newCall.FindPropertyRelative("m_Mode").enumValueIndex = 6;
            newCall.FindPropertyRelative("m_CallState").enumValueIndex = 2;
            SerializedProperty arguments = newCall.FindPropertyRelative("m_Arguments");
            if (arguments != null)
            {
                arguments.FindPropertyRelative("m_BoolArgument").boolValue = true;
            }

            so.ApplyModifiedProperties();
        }

        private static void WireVoicePresenter(
            ServerRoomScenarioController controller,
            List<string> componentWarnings)
        {
            OfficeFireVoiceLineContentPresenter presenter =
                Object.FindFirstObjectByType<OfficeFireVoiceLineContentPresenter>(FindObjectsInactive.Include);
            if (presenter == null)
            {
                componentWarnings.Add("OfficeFireVoiceLineContentPresenter not found — announcements will not play.");
                return;
            }

            Undo.RecordObject(controller, "Office Fire: Wire Server voice presenter");
            SerializedObject so = new SerializedObject(controller);
            SerializedProperty announcementProp = so.FindProperty("onAnnouncementRequested");
            if (announcementProp == null)
            {
                componentWarnings.Add("ServerRoomScenarioController: onAnnouncementRequested not found.");
                return;
            }

            SerializedProperty callsProp = announcementProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (callsProp == null)
            {
                componentWarnings.Add("ServerRoomScenarioController: announcement UnityEvent calls not found.");
                return;
            }

            bool alreadyWired = false;
            for (int i = 0; i < callsProp.arraySize; i++)
            {
                SerializedProperty call = callsProp.GetArrayElementAtIndex(i);
                SerializedProperty target = call.FindPropertyRelative("m_Target");
                if (target != null && target.objectReferenceValue == presenter)
                {
                    alreadyWired = true;
                    break;
                }
            }

            if (!alreadyWired)
            {
                int index = callsProp.arraySize;
                callsProp.InsertArrayElementAtIndex(index);
                SerializedProperty call = callsProp.GetArrayElementAtIndex(index);
                call.FindPropertyRelative("m_Target").objectReferenceValue = presenter;
                call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                    "Woi.OfficeFire.OfficeFireVoiceLineContentPresenter, Woi.OfficeFire.Integration";
                call.FindPropertyRelative("m_MethodName").stringValue = "PlayVoiceLine";
                call.FindPropertyRelative("m_Mode").enumValueIndex = 0;
                call.FindPropertyRelative("m_CallState").enumValueIndex = 2;
            }

            so.ApplyModifiedProperties();
        }

        private static void EnsureServerTriggers(
            Transform triggers,
            ServerRoomScenarioController controller,
            List<string> created,
            List<string> reused,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            EnsureTrigger(triggers, "Trigger_RoomProximity", ServerRoomScenarioController.Actions.NoticeSmoke, controller, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
            EnsureTrigger(triggers, "Trigger_RoomEntered", ServerRoomScenarioController.Actions.EnterServerRoom, controller, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
            EnsureTrigger(triggers, "Trigger_LeaveServerRoom", ServerRoomScenarioController.Actions.LeaveServerRoom, controller, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
            EnsureTrigger(triggers, "Trigger_AssemblyAreaDoor", ServerRoomScenarioController.Actions.ReachedAssemblyAreaDoor, controller, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
        }

        private static void EnsureServerInteractables(
            Transform interactables,
            ServerRoomScenarioController controller,
            List<string> created,
            List<string> reused,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            WireSelectable(
                EnsureChild(interactables, "ExtinguisherPickup", created, reused),
                controller,
                ServerRoomScenarioController.Actions.GrabExtinguisher,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            WireSelectable(
                EnsureChild(interactables, "WaterSource", created, reused),
                controller,
                ServerRoomScenarioController.Actions.UseWater,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            WireSelectable(
                EnsureChild(interactables, "ExtinguisherUse", created, reused),
                controller,
                ServerRoomScenarioController.Actions.UseExtinguisher,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
        }

        private static void FixSuppressionAlarms(
            Transform serverRoot,
            ServerRoomScenarioController controller,
            List<string> componentWarnings)
        {
            Alarm[] alarms = serverRoot.GetComponentsInChildren<Alarm>(true);
            for (int i = 0; i < alarms.Length; i++)
            {
                Alarm alarm = alarms[i];
                Undo.RecordObject(alarm, "Office Fire: Wire Server suppression alarm");
                SerializedObject so = new SerializedObject(alarm);
                SerializedProperty actionIdProp = so.FindProperty("actionId");
                if (actionIdProp != null)
                {
                    actionIdProp.stringValue = ServerRoomScenarioController.Actions.PressSuppressionButton;
                }

                SerializedProperty targetProp = so.FindProperty("targetScenario");
                if (targetProp != null)
                {
                    targetProp.objectReferenceValue = controller;
                }

                SerializedProperty instructionTextProp = so.FindProperty("instructionText");
                if (instructionTextProp != null)
                {
                    instructionTextProp.stringValue = "Press E to activate suppression";
                }

                SerializedProperty instructionTextTrProp = so.FindProperty("instructionTextTurkish");
                if (instructionTextTrProp != null)
                {
                    instructionTextTrProp.stringValue = "S\u00f6nd\u00fcrme sistemini devreye almak i\u00e7in E'ye bas\u0131n";
                }

                so.ApplyModifiedProperties();
            }
        }

        private static void WireExtinguisherWallPickup(
            Transform serverRoot,
            List<string> componentWarnings,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent)
        {
            ExtinguisherPickupItem[] pickups = serverRoot.GetComponentsInChildren<ExtinguisherPickupItem>(true);
            if (pickups.Length == 0)
            {
                componentWarnings.Add(
                    "No ExtinguisherPickupItem under ServerRoom — add FireExtinguisherE Variant to ServerRoom prefab.");
                return;
            }

            for (int i = 0; i < pickups.Length; i++)
            {
                GameObject host = pickups[i].gameObject;

                BoxCollider box = host.GetComponent<BoxCollider>();
                if (box == null)
                {
                    box = Undo.AddComponent<BoxCollider>(host);
                    componentsAdded.Add($"BoxCollider on '{OfficeFireSceneHierarchyBuilder.FullPath(host.transform)}'");
                }

                box.isTrigger = true;

                OfficeFireSceneHierarchyBuilder.TryAddComponent<Outline>(
                    host,
                    "Outline",
                    componentsAdded,
                    componentsAlreadyPresent,
                    componentWarnings);

                OfficeFireSceneHierarchyBuilder.TryAddComponent<SelectableInstructionPrompt>(
                    host,
                    "SelectableInstructionPrompt",
                    componentsAdded,
                    componentsAlreadyPresent,
                    componentWarnings);
            }
        }

        private static void RewireServerInteractions(
            Transform serverRoot,
            ServerRoomScenarioController controller,
            List<string> componentWarnings)
        {
            SelectableScenarioAction[] selectables = serverRoot.GetComponentsInChildren<SelectableScenarioAction>(true);
            for (int i = 0; i < selectables.Length; i++)
            {
                WireSelectableTarget(selectables[i], controller, componentWarnings);
            }

            ScenarioTriggerVolume[] triggers = serverRoot.GetComponentsInChildren<ScenarioTriggerVolume>(true);
            for (int i = 0; i < triggers.Length; i++)
            {
                ScenarioTriggerVolume triggerVolume = triggers[i];
                if (triggerVolume == null)
                {
                    continue;
                }

                SerializedObject triggerSo = new SerializedObject(triggerVolume);
                SerializedProperty actionIdProp = triggerSo.FindProperty("actionId");
                string actionId = actionIdProp != null ? actionIdProp.stringValue : string.Empty;
                if (OfficeFireSharedEvacuationTriggersBuilder.IsSharedEvacuationActionId(actionId)
                    || OfficeFireSharedEvacuationTriggersBuilder.IsSharedEvacuationTriggerName(triggerVolume.name))
                {
                    continue;
                }

                WireTriggerTarget(triggerVolume, controller, componentWarnings);
            }

            Alarm[] alarms = serverRoot.GetComponentsInChildren<Alarm>(true);
            for (int i = 0; i < alarms.Length; i++)
            {
                WireAlarmTarget(alarms[i], controller, componentWarnings);
            }
        }

        private static Transform EnsureChild(Transform parent, string name, List<string> created, List<string> reused)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                reused.Add(OfficeFireSceneHierarchyBuilder.FullPath(child));
                return child;
            }

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Office Fire: Create " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            created.Add(OfficeFireSceneHierarchyBuilder.FullPath(go.transform));
            return go.transform;
        }

        private static void EnsureTrigger(
            Transform triggers,
            string triggerName,
            string actionId,
            ServerRoomScenarioController controller,
            List<string> created,
            List<string> reused,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            Transform triggerTransform = triggers.Find(triggerName);
            if (triggerTransform == null)
            {
                GameObject triggerGo = new GameObject(triggerName);
                Undo.RegisterCreatedObjectUndo(triggerGo, "Office Fire: Create " + triggerName);
                triggerGo.transform.SetParent(triggers, false);
                triggerTransform = triggerGo.transform;
                created.Add(OfficeFireSceneHierarchyBuilder.FullPath(triggerTransform));
            }
            else
            {
                reused.Add(OfficeFireSceneHierarchyBuilder.FullPath(triggerTransform));
            }

            WireTrigger(triggerTransform, controller, actionId, componentsAdded, componentsAlreadyPresent, componentWarnings);
        }

        private static void WireSelectable(
            Transform host,
            ServerRoomScenarioController controller,
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

        private static void WireTrigger(
            Transform host,
            ServerRoomScenarioController controller,
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

            Undo.RecordObject(trigger, "Office Fire: Wire ScenarioTriggerVolume");
            SerializedObject so = new SerializedObject(trigger);
            SerializedProperty actionIdProp = so.FindProperty("actionId");
            if (actionIdProp != null)
            {
                actionIdProp.stringValue = actionId;
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

        private static void WireSelectableTarget(
            SelectableScenarioAction action,
            ServerRoomScenarioController controller,
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

        private static void WireTriggerTarget(
            ScenarioTriggerVolume trigger,
            ServerRoomScenarioController controller,
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

        private static void WireAlarmTarget(
            Alarm alarm,
            ServerRoomScenarioController controller,
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

        private static GameObject FindSceneRootByName(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == name)
                {
                    return roots[i];
                }
            }

            return null;
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
            sb.AppendLine("[Office Fire Scene] Server Room setup — " + scenePath);
            AppendList(sb, "Created", created);
            AppendList(sb, "Reused", reused);
            AppendList(sb, "Components added", componentsAdded);
            AppendList(sb, "Components already present", componentsAlreadyPresent);
            AppendList(sb, "Warnings", componentWarnings);
            Debug.Log(sb.ToString());
        }

        private static void AppendList(StringBuilder sb, string title, List<string> items)
        {
            sb.AppendLine(title + " (" + items.Count + "):");
            for (int i = 0; i < items.Count; i++)
            {
                sb.AppendLine("  - " + items[i]);
            }
        }

        /// <summary>
        /// Invoked from Unity batch mode:
        /// -executeMethod Woi.OfficeFire.Editor.OfficeFireServerSceneHierarchyBuilder.BatchEnsureServerRoomSetup
        /// </summary>
        public static void BatchEnsureServerRoomSetup()
        {
            const string scenePath = "Assets/Project/Scenes/FireModule/FireModule_Office.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EnsureServerRoomSetupInScene(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
