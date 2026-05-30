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
    /// Ensures Kitchen Cafe scenario wiring matches Server Room (triggers, bridges, extinguisher pickup).
    /// </summary>
    public static class OfficeFireKitchenSceneHierarchyBuilder
    {
        private const string RootName = "======FireModules======";
        private const string MenuPath = "Tools/Woi/Office Fire/Scene/Ensure Kitchen Cafe Setup";
        private const string KitchenVoiceLineAssetPath =
            "Assets/Project/OfficeFire/ScriptableObjects/KitchenCafe/Content/KitchenCafeVoiceLineContentDatabase.asset";

        [MenuItem(MenuPath, false, 24)]
        private static void EnsureKitchenCafeSetupActiveScene()
        {
            EnsureKitchenCafeSetupInScene(SceneManager.GetActiveScene());
        }

        [MenuItem(MenuPath, true, 24)]
        private static bool EnsureKitchenCafeSetupActiveSceneValidate()
        {
            return !Application.isPlaying;
        }

        public static void EnsureKitchenCafeSetupInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Office Fire Scene] Scene is not valid or not loaded: " + scene.path);
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Ensure Kitchen Cafe Setup");
            int undoGroup = Undo.GetCurrentGroup();

            var created = new List<string>();
            var reused = new List<string>();
            var componentsAdded = new List<string>();
            var componentsAlreadyPresent = new List<string>();
            var componentWarnings = new List<string>();

            Transform serverRoot = FindKitchenCafeRoot(scene);
            if (serverRoot == null)
            {
                Debug.LogError("[Office Fire Scene] KitchenCafe not found under 03_Scenarios.", null);
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            KitchenCafeScenarioController controller =
                serverRoot.GetComponentInChildren<KitchenCafeScenarioController>(true);
            if (controller == null)
            {
                componentWarnings.Add("KitchenCafeScenarioController not found.");
            }

            Transform triggers = EnsureChild(serverRoot, "Triggers", created, reused);
            Transform interactables = EnsureChild(serverRoot, "Interactables", created, reused);
            Transform evacuation = EnsureChild(serverRoot, "Evacuation", created, reused);

            if (controller != null)
            {
                EnsureControllerComponents(controller, componentsAdded, componentsAlreadyPresent, componentWarnings);
                WireKitchenScenario(
                    controller,
                    serverRoot,
                    evacuation,
                    created,
                    reused,
                    componentWarnings);
                EnsureKitchenTriggers(triggers, controller, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
                EnsureKitchenInteractables(interactables, controller, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
                Transform mutfakRoot = EnsureMutfakv2UnderKitchen(scene, serverRoot, created, reused, componentWarnings);
                if (mutfakRoot != null)
                {
                    WireMutfakv2Interactables(
                        mutfakRoot,
                        controller,
                        componentsAdded,
                        componentsAlreadyPresent,
                        componentWarnings);
                }
                else
                {
                    componentWarnings.Add(
                        "Mutfakv2 not found — place Mutfakv2 under KitchenCafe and add Alarm + FireExtinguisher.");
                }

                FixSuppressionAlarms(serverRoot, controller, componentWarnings);
                WireExtinguisherWallPickup(serverRoot, componentWarnings, componentsAdded, componentsAlreadyPresent);
                RewireKitchenInteractions(serverRoot, controller, componentWarnings);
            }

            OfficeFireSharedEvacuationTriggersBuilder.EnsureSharedEvacuationTriggersInScene(scene);

            if (controller != null)
            {
                EnsureKitchenVoiceLineContentPresenter(scene, controller, componentsAdded, componentWarnings);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            LogSummary(scene.path, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
        }

        private static Transform FindKitchenCafeRoot(Scene scene)
        {
            GameObject fireModulesRoot = FindSceneRootByName(scene, RootName);
            if (fireModulesRoot == null)
            {
                return null;
            }

            Transform scenarios = fireModulesRoot.transform.Find("03_Scenarios");
            return scenarios != null ? scenarios.Find("KitchenCafe") : null;
        }

        private static Transform EnsureMutfakv2UnderKitchen(
            Scene scene,
            Transform kitchenRoot,
            List<string> created,
            List<string> reused,
            List<string> componentWarnings)
        {
            Transform mutfakRoot = FindMutfakv2Root(scene, kitchenRoot);
            if (mutfakRoot == null)
            {
                return null;
            }

            if (kitchenRoot != null && mutfakRoot.parent != kitchenRoot)
            {
                Undo.SetTransformParent(mutfakRoot, kitchenRoot, "Office Fire: Parent Mutfakv2 under KitchenCafe");
                created.Add("Reparented '" + OfficeFireSceneHierarchyBuilder.FullPath(mutfakRoot) + "' under KitchenCafe");
            }
            else
            {
                reused.Add(OfficeFireSceneHierarchyBuilder.FullPath(mutfakRoot));
            }

            return mutfakRoot;
        }

        private static Transform FindMutfakv2Root(Scene scene, Transform kitchenRoot)
        {
            if (kitchenRoot != null)
            {
                Transform underKitchen = FindChildByNameRecursive(kitchenRoot, "Mutfakv2");
                if (underKitchen != null)
                {
                    return underKitchen;
                }
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].name == "Mutfakv2")
                    {
                        return all[i];
                    }
                }
            }

            return null;
        }

        private static Transform FindChildByNameRecursive(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent.name == childName)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildByNameRecursive(parent.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void WireMutfakv2Interactables(
            Transform mutfakRoot,
            KitchenCafeScenarioController controller,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            Transform alarmHost = FindMutfakInteractableHost(
                mutfakRoot,
                "Alarm",
                "AlarmButton",
                "alarm");
            if (alarmHost != null)
            {
                OfficeFireAlarmWireHelper.WireAlarmLikeArchive(
                    alarmHost,
                    controller,
                    KitchenCafeScenarioController.Actions.PressSuppressionButton,
                    "Press E to activate suppression",
                    "S\u00f6nd\u00fcrme sistemini devreye almak i\u00e7in E'ye bas\u0131n",
                    componentsAdded,
                    componentsAlreadyPresent,
                    componentWarnings);
            }
            else
            {
                componentWarnings.Add("Mutfakv2/Alarm not found — add a child named Alarm or AlarmButton.");
            }

            int wiredExtinguishers =
                OfficeFireArchiveExtinguisherHoverOutlineSetup.WireExtinguisherHoverOutlinesUnder(mutfakRoot);
            if (wiredExtinguishers > 0)
            {
                componentsAlreadyPresent.Add(
                    $"Wired hover outline on {wiredExtinguishers} extinguisher(s) under Mutfakv2");
            }
            else
            {
                componentWarnings.Add(
                    "No ExtinguisherPickupItem under Mutfakv2 — ensure FireExtinguisherB Variant (or similar) is present.");
            }

            ExtinguisherPickupItem[] pickups = mutfakRoot.GetComponentsInChildren<ExtinguisherPickupItem>(true);
            for (int i = 0; i < pickups.Length; i++)
            {
                ExtinguisherPickupItem pickup = pickups[i];
                if (pickup == null)
                {
                    continue;
                }

                GameObject host = pickup.gameObject;
                OfficeFireSceneHierarchyBuilder.TryAddComponent<SelectableInstructionPrompt>(
                    host,
                    "SelectableInstructionPrompt",
                    componentsAdded,
                    componentsAlreadyPresent,
                    componentWarnings);
            }
        }

        private static Transform FindMutfakInteractableHost(
            Transform mutfakRoot,
            params string[] preferredNames)
        {
            if (mutfakRoot == null)
            {
                return null;
            }

            for (int i = 0; i < preferredNames.Length; i++)
            {
                Transform direct = mutfakRoot.Find(preferredNames[i]);
                if (direct != null)
                {
                    return direct;
                }
            }

            Transform[] all = mutfakRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < preferredNames.Length; i++)
            {
                string preferred = preferredNames[i];
                for (int j = 0; j < all.Length; j++)
                {
                    Transform candidate = all[j];
                    if (candidate != null && candidate.name == preferred)
                    {
                        return candidate;
                    }
                }
            }

            for (int i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate == null || candidate == mutfakRoot)
                {
                    continue;
                }

                if (candidate.name.IndexOf("alarm", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void EnsureControllerComponents(
            KitchenCafeScenarioController controller,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            OfficeFireSceneHierarchyBuilder.TryAddComponent<OfficeFireKitchenFireExtinguishBridge>(
                controller.gameObject,
                "OfficeFireKitchenFireExtinguishBridge",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            OfficeFireSceneHierarchyBuilder.TryAddComponent<OfficeFireKitchenExtinguisherGrabScenarioBridge>(
                controller.gameObject,
                "OfficeFireKitchenExtinguisherGrabScenarioBridge",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            OfficeFireSceneHierarchyBuilder.TryAddComponent<OfficeFireKitchenExtinguisherHudBridge>(
                controller.gameObject,
                "OfficeFireKitchenExtinguisherHudBridge",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            OfficeFireKitchenFireExtinguishBridge extinguishBridge =
                controller.GetComponent<OfficeFireKitchenFireExtinguishBridge>();
            if (extinguishBridge != null)
            {
                Undo.RecordObject(extinguishBridge, "Office Fire: Wire Kitchen FireExtinguishBridge");
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

        private static void WireKitchenScenario(
            KitchenCafeScenarioController controller,
            Transform serverRoot,
            Transform evacuationRoot,
            List<string> created,
            List<string> reused,
            List<string> componentWarnings)
        {
            Undo.RecordObject(controller, "Office Fire: Wire KitchenCafeScenarioController");
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

            ScenarioFireGrowthController fireGrowth = controller.GetComponent<ScenarioFireGrowthController>();
            if (fireGrowth == null)
            {
                fireGrowth = Undo.AddComponent<ScenarioFireGrowthController>(controller.gameObject);
            }

            if (fireGrowth == null)
            {
                fireGrowth = serverRoot.GetComponentInChildren<ScenarioFireGrowthController>(true);
            }

            SerializedProperty fireGrowthProp = so.FindProperty("fireGrowthController");
            if (fireGrowthProp != null && fireGrowth != null)
            {
                fireGrowthProp.objectReferenceValue = fireGrowth;
            }

            GameObject evacuationNpcsRoot = EnsureEvacuationNpcsRoot(evacuationRoot, created, reused);
            WireEvacuationStarted(controller, evacuationNpcsRoot, componentWarnings);
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
                Undo.RecordObject(npcs.gameObject, "Office Fire: Deactivate Kitchen evacuation Npcs");
                npcs.gameObject.SetActive(false);
            }

            return npcs.gameObject;
        }

        private static void WireEvacuationStarted(
            KitchenCafeScenarioController controller,
            GameObject evacuationNpcsRoot,
            List<string> componentWarnings)
        {
            if (controller == null)
            {
                return;
            }

            if (evacuationNpcsRoot == null)
            {
                componentWarnings.Add("Kitchen evacuation Npcs root missing — onEvacuationStarted not wired.");
                return;
            }

            Undo.RecordObject(controller, "Office Fire: Wire Kitchen onEvacuationStarted");
            SerializedObject so = new SerializedObject(controller);
            SerializedProperty eventProp = so.FindProperty("onEvacuationStarted");
            if (eventProp == null)
            {
                componentWarnings.Add("KitchenCafeScenarioController: onEvacuationStarted not found.");
                return;
            }

            SerializedProperty callsProp = eventProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (callsProp == null)
            {
                componentWarnings.Add("KitchenCafeScenarioController: onEvacuationStarted calls not found.");
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

        private static void EnsureKitchenVoiceLineContentPresenter(
            Scene scene,
            KitchenCafeScenarioController controller,
            List<string> componentsAdded,
            List<string> componentWarnings)
        {
            Transform contentRoot = FindSceneChildByName(scene, "02_Content");
            if (contentRoot == null)
            {
                componentWarnings.Add("02_Content not found — Kitchen voice presenter not wired.");
                return;
            }

            Transform presenterHost = contentRoot.Find("KitchenCafeContentPresenter");
            if (presenterHost == null)
            {
                componentWarnings.Add("KitchenCafeContentPresenter not found under 02_Content.");
                return;
            }

            OfficeFireVoiceLineContentPresenter presenter =
                presenterHost.GetComponent<OfficeFireVoiceLineContentPresenter>();
            if (presenter == null)
            {
                presenter = Undo.AddComponent<OfficeFireVoiceLineContentPresenter>(presenterHost.gameObject);
                componentsAdded.Add(
                    "OfficeFireVoiceLineContentPresenter on '" +
                    OfficeFireSceneHierarchyBuilder.FullPath(presenterHost) + "'");
            }

            OfficeFireVoiceLineContentDatabase database =
                AssetDatabase.LoadAssetAtPath<OfficeFireVoiceLineContentDatabase>(KitchenVoiceLineAssetPath);
            if (database == null)
            {
                componentWarnings.Add(
                    "Kitchen voice line database missing at " + KitchenVoiceLineAssetPath +
                    " — run Woi/Office Fire/Sync Kitchen Content Database From Server.");
                return;
            }

            Undo.RecordObject(presenter, "Office Fire: Wire Kitchen voice line database");
            SerializedObject presenterSo = new SerializedObject(presenter);
            SerializedProperty databaseProp = presenterSo.FindProperty("database");
            if (databaseProp != null)
            {
                databaseProp.objectReferenceValue = database;
            }

            SerializedProperty adapterProp = presenterSo.FindProperty("announcementAudioAdapter");
            if (adapterProp != null && adapterProp.objectReferenceValue == null)
            {
                Woi.UI.Announcements.WoiAnnouncementAudioAdapter adapter =
                    presenterHost.GetComponent<Woi.UI.Announcements.WoiAnnouncementAudioAdapter>();
                if (adapter == null)
                {
                    adapter = Undo.AddComponent<Woi.UI.Announcements.WoiAnnouncementAudioAdapter>(presenterHost.gameObject);
                }

                adapterProp.objectReferenceValue = adapter;
            }

            presenterSo.ApplyModifiedProperties();
            WireVoicePresenter(controller, presenter, componentWarnings);
        }

        private static void WireVoicePresenter(
            KitchenCafeScenarioController controller,
            OfficeFireVoiceLineContentPresenter presenter,
            List<string> componentWarnings)
        {
            if (controller == null || presenter == null)
            {
                return;
            }

            Undo.RecordObject(controller, "Office Fire: Wire Kitchen voice presenter");
            SerializedObject so = new SerializedObject(controller);
            SerializedProperty announcementProp = so.FindProperty("onAnnouncementRequested");
            if (announcementProp == null)
            {
                componentWarnings.Add("KitchenCafeScenarioController: onAnnouncementRequested not found.");
                return;
            }

            SerializedProperty callsProp = announcementProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (callsProp == null)
            {
                componentWarnings.Add("KitchenCafeScenarioController: announcement UnityEvent calls not found.");
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

        private static Transform FindSceneChildByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root.transform;
                }

                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name == name)
                    {
                        return all[i];
                    }
                }
            }

            return null;
        }

        private static void EnsureKitchenTriggers(
            Transform triggers,
            KitchenCafeScenarioController controller,
            List<string> created,
            List<string> reused,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            EnsureTrigger(triggers, "Trigger_RoomProximity", KitchenCafeScenarioController.Actions.NoticeSmoke, controller, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
            EnsureTrigger(triggers, "Trigger_RoomEntered", KitchenCafeScenarioController.Actions.EnterKitchenCafe, controller, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
            EnsureTrigger(triggers, "Trigger_LeaveKitchenCafe", KitchenCafeScenarioController.Actions.LeaveKitchenCafe, controller, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
            EnsureTrigger(triggers, "Trigger_AssemblyAreaDoor", KitchenCafeScenarioController.Actions.ReachedAssemblyAreaDoor, controller, created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
        }

        private static void EnsureKitchenInteractables(
            Transform interactables,
            KitchenCafeScenarioController controller,
            List<string> created,
            List<string> reused,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            WireSelectable(
                EnsureChild(interactables, "ExtinguisherPickup", created, reused),
                controller,
                KitchenCafeScenarioController.Actions.GrabExtinguisher,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            WireSelectable(
                EnsureChild(interactables, "WaterSource", created, reused),
                controller,
                KitchenCafeScenarioController.Actions.UseWater,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            WireSelectable(
                EnsureChild(interactables, "ExtinguisherUse", created, reused),
                controller,
                KitchenCafeScenarioController.Actions.UseExtinguisher,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
        }

        private static void FixSuppressionAlarms(
            Transform serverRoot,
            KitchenCafeScenarioController controller,
            List<string> componentWarnings)
        {
            Alarm[] alarms = serverRoot.GetComponentsInChildren<Alarm>(true);
            for (int i = 0; i < alarms.Length; i++)
            {
                Alarm alarm = alarms[i];
                Undo.RecordObject(alarm, "Office Fire: Wire Kitchen suppression alarm");
                SerializedObject so = new SerializedObject(alarm);
                SerializedProperty actionIdProp = so.FindProperty("actionId");
                if (actionIdProp != null)
                {
                    actionIdProp.stringValue = KitchenCafeScenarioController.Actions.PressSuppressionButton;
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
                    "No ExtinguisherPickupItem under KitchenCafe — add FireExtinguisherE Variant to KitchenCafe prefab.");
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

        private static void RewireKitchenInteractions(
            Transform serverRoot,
            KitchenCafeScenarioController controller,
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
            KitchenCafeScenarioController controller,
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
            KitchenCafeScenarioController controller,
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
            KitchenCafeScenarioController controller,
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
            KitchenCafeScenarioController controller,
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
            KitchenCafeScenarioController controller,
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
            KitchenCafeScenarioController controller,
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
            sb.AppendLine("[Office Fire Scene] Kitchen Cafe setup — " + scenePath);
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
        /// -executeMethod Woi.OfficeFire.Editor.OfficeFireKitchenSceneHierarchyBuilder.BatchEnsureKitchenCafeSetup
        /// </summary>
        public static void BatchEnsureKitchenCafeSetup()
        {
            const string scenePath = "Assets/Project/Scenes/FireModule/FireModule_Office.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EnsureKitchenCafeSetupInScene(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
