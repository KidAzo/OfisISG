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
    /// Builds a minimal Kitchen/Cafe-focused hierarchy under <c>======FireModules======</c> in the active scene.
    /// </summary>
    public static class OfficeFireSceneHierarchyBuilder
    {
        private const string RootName = "======FireModules======";
        private const string MenuPath = "Tools/Woi/Office Fire/Scene/Create Minimal Kitchen Hierarchy";

        [MenuItem(MenuPath, false, 20)]
        private static void CreateMinimalKitchenHierarchy()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Office Fire Scene] Active scene is not valid or not loaded.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Minimal Kitchen Hierarchy");
            int undoGroup = Undo.GetCurrentGroup();

            var created = new List<string>();
            var reused = new List<string>();
            var componentsAdded = new List<string>();
            var componentsAlreadyPresent = new List<string>();
            var componentWarnings = new List<string>();

            Transform root = EnsureFireModulesRoot(scene, created, reused);
            if (root == null)
            {
                LogSummary(created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
                return;
            }

            Transform t00 = EnsureChild(root, "00_Runtime", created, reused);
            Transform t01 = EnsureChild(root, "01_Player", created, reused);
            Transform t02 = EnsureChild(root, "02_Content", created, reused);
            Transform t03 = EnsureChild(root, "03_Scenarios", created, reused);
            Transform t04 = EnsureChild(root, "04_SharedInteractions", created, reused);
            Transform t05 = EnsureChild(root, "05_UI", created, reused);

            Transform spawnPoints = EnsureChild(t01, "SpawnPoints", created, reused);
            Transform spawnKitchen = EnsureChild(spawnPoints, "Spawn_KitchenCafe", created, reused);

            Transform kitchenRoot = EnsureChild(t03, "KitchenCafe", created, reused);
            Transform controllerFolder = EnsureChild(kitchenRoot, "Controller", created, reused);
            Transform interactables = EnsureChild(kitchenRoot, "Interactables", created, reused);
            Transform triggers = EnsureChild(kitchenRoot, "Triggers", created, reused);
            Transform evacuation = EnsureChild(kitchenRoot, "Evacuation", created, reused);

            EnsureChild(interactables, "ExtinguisherPickup", created, reused);
            EnsureChild(interactables, "WaterSource", created, reused);
            EnsureChild(interactables, "ExtinguisherUse", created, reused);

            EnsureChild(triggers, "Trigger_RoomProximity", created, reused);
            EnsureChild(triggers, "Trigger_RoomEntered", created, reused);
            EnsureChild(triggers, "Trigger_LeaveKitchenCafe", created, reused);
            EnsureChild(triggers, "Trigger_AssemblyAreaDoor", created, reused);

            Transform tBootstrap = EnsureChild(t00, "OfficeFireScenarioBootstrapper", created, reused);
            Transform tInit = EnsureChild(t00, "OfficeFirePlayerInitializer", created, reused);
            Transform tPresenter = EnsureChild(t02, "KitchenCafeContentPresenter", created, reused);
            Transform tKitchenController = EnsureChild(controllerFolder, "KitchenCafeScenarioController", created, reused);
            Transform tPcInteractor = EnsureChild(t04, "PCSelectableInteractor", created, reused);

            EnsureChild(t05, "Popup", created, reused);
            EnsureChild(t05, "Objective", created, reused);
            EnsureChild(t05, "Report", created, reused);

            TryAddComponent<OfficeFireScenarioBootstrapper>(
                tBootstrap.gameObject,
                "OfficeFireScenarioBootstrapper",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            TryAddComponent<OfficeFirePlayerInitializer>(
                tInit.gameObject,
                "OfficeFirePlayerInitializer",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            TryAddComponent<OfficeFireVoiceLineContentPresenter>(
                tPresenter.gameObject,
                "OfficeFireVoiceLineContentPresenter",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            KitchenCafeScenarioController kitchenController = TryAddComponent<KitchenCafeScenarioController>(
                tKitchenController.gameObject,
                "KitchenCafeScenarioController",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
            TryAddComponent<PCSelectableInteractor>(
                tPcInteractor.gameObject,
                "PCSelectableInteractor",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            WireKitchenScenario(kitchenController, kitchenRoot, componentWarnings);
            WireSpawnPoint(tInit.gameObject, spawnKitchen, OfficeFireScenarioId.KitchenCafe, componentWarnings);
            WireBootstrapperController(
                tBootstrap.gameObject,
                tInit.gameObject,
                kitchenController != null ? kitchenController.gameObject : null,
                componentWarnings,
                setStartScenario: OfficeFireScenarioId.KitchenCafe);

            OfficeFireSharedEvacuationTriggersBuilder.EnsureSharedEvacuationTriggersInScene(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);

            LogSummary(created, reused, componentsAdded, componentsAlreadyPresent, componentWarnings);
        }

        internal static Transform EnsureFireModulesRoot(Scene scene, List<string> created, List<string> reused)
        {
            GameObject rootGo = FindSceneRootByName(scene, RootName);
            if (rootGo == null)
            {
                rootGo = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(rootGo, "Office Fire: Create module root");
                created.Add(FullPath(rootGo.transform));
            }
            else
            {
                reused.Add(FullPath(rootGo.transform));
            }

            Transform root = rootGo.transform;
            ResetLocalTransform(root);
            return root;
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

        internal static Transform EnsureChild(Transform parent, string name, List<string> created, List<string> reused)
        {
            Transform existing = FindDirectChild(parent, name);
            if (existing != null)
            {
                ResetLocalTransform(existing);
                reused.Add(FullPath(existing));
                return existing;
            }

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Office Fire: Hierarchy node");
            go.transform.SetParent(parent, false);
            ResetLocalTransform(go.transform);
            created.Add(FullPath(go.transform));
            return go.transform;
        }

        private static void ResetLocalTransform(Transform t)
        {
            if (t == null)
            {
                return;
            }

            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c != null && c.name == name)
                {
                    return c;
                }
            }

            return null;
        }

        internal static string FullPath(Transform t)
        {
            if (t == null)
            {
                return "(null)";
            }

            var parts = new List<string>();
            Transform walk = t;
            while (walk != null)
            {
                parts.Add(walk.name);
                walk = walk.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        internal static T TryAddComponent<T>(
            GameObject host,
            string label,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
            where T : Component
        {
            if (host == null)
            {
                componentWarnings.Add($"{label}: host GameObject was null.");
                return null;
            }

            T existing = host.GetComponent<T>();
            if (existing != null)
            {
                componentsAlreadyPresent.Add($"{typeof(T).Name} on '{FullPath(host.transform)}'");
                return existing;
            }

            T added = Undo.AddComponent<T>(host);
            if (added == null)
            {
                componentWarnings.Add($"{label}: Undo.AddComponent<{typeof(T).Name}> returned null (type missing or not allowed on this object?).");
                return null;
            }

            componentsAdded.Add($"{typeof(T).Name} on '{FullPath(host.transform)}'");
            return added;
        }

        private static void WireKitchenScenario(
            KitchenCafeScenarioController kitchenController,
            Transform kitchenRoot,
            List<string> componentWarnings)
        {
            if (kitchenController == null || kitchenRoot == null)
            {
                return;
            }

            Undo.RecordObject(kitchenController, "Office Fire: Wire KitchenCafeScenarioController");
            SerializedObject so = new SerializedObject(kitchenController);
            SerializedProperty scenarioRootProp = so.FindProperty("scenarioRoot");
            if (scenarioRootProp != null)
            {
                scenarioRootProp.objectReferenceValue = kitchenRoot.gameObject;
            }
            else
            {
                componentWarnings.Add("KitchenCafeScenarioController: serialized field 'scenarioRoot' not found.");
            }

            so.ApplyModifiedProperties();
        }

        internal static void WireSpawnPoint(
            GameObject initializerGo,
            Transform spawnPoint,
            OfficeFireScenarioId scenarioId,
            List<string> componentWarnings)
        {
            if (initializerGo == null || spawnPoint == null)
            {
                return;
            }

            OfficeFirePlayerInitializer init = initializerGo.GetComponent<OfficeFirePlayerInitializer>();
            if (init == null)
            {
                return;
            }

            Undo.RecordObject(init, "Office Fire: Wire OfficeFirePlayerInitializer");
            SerializedObject so = new SerializedObject(init);
            SerializedProperty spawns = so.FindProperty("spawnPoints");
            if (spawns == null || !spawns.isArray)
            {
                componentWarnings.Add("OfficeFirePlayerInitializer: serialized field 'spawnPoints' not found or not an array.");
                so.ApplyModifiedProperties();
                return;
            }

            int scenarioEnumIndex = (int)scenarioId;
            int index = -1;
            for (int i = 0; i < spawns.arraySize; i++)
            {
                SerializedProperty el = spawns.GetArrayElementAtIndex(i);
                SerializedProperty idProp = el.FindPropertyRelative("ScenarioId");
                if (idProp != null && idProp.propertyType == SerializedPropertyType.Enum &&
                    idProp.enumValueIndex == scenarioEnumIndex)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                index = spawns.arraySize;
                spawns.arraySize++;
            }

            SerializedProperty entry = spawns.GetArrayElementAtIndex(index);
            SerializedProperty scenarioIdProp = entry.FindPropertyRelative("ScenarioId");
            SerializedProperty spawnPointProp = entry.FindPropertyRelative("SpawnPoint");
            if (scenarioIdProp != null)
            {
                scenarioIdProp.enumValueIndex = scenarioEnumIndex;
            }

            if (spawnPointProp != null)
            {
                spawnPointProp.objectReferenceValue = spawnPoint;
            }
            else
            {
                componentWarnings.Add("OfficeFirePlayerInitializer: spawnPoints entry has no 'SpawnPoint' field.");
            }

            so.ApplyModifiedProperties();
        }

        internal static void WireBootstrapperController(
            GameObject bootstrapperGo,
            GameObject initializerGo,
            GameObject scenarioControllerGo,
            List<string> componentWarnings,
            OfficeFireScenarioId? setStartScenario = null)
        {
            if (bootstrapperGo == null)
            {
                return;
            }

            OfficeFireScenarioBootstrapper bootstrapper = bootstrapperGo.GetComponent<OfficeFireScenarioBootstrapper>();
            if (bootstrapper == null)
            {
                return;
            }

            Undo.RecordObject(bootstrapper, "Office Fire: Wire OfficeFireScenarioBootstrapper");
            SerializedObject so = new SerializedObject(bootstrapper);

            if (setStartScenario.HasValue)
            {
                SerializedProperty startProp = so.FindProperty("startScenario");
                if (startProp != null)
                {
                    startProp.enumValueIndex = (int)setStartScenario.Value;
                }
                else
                {
                    componentWarnings.Add("OfficeFireScenarioBootstrapper: serialized field 'startScenario' not found.");
                }
            }

            SerializedProperty initProp = so.FindProperty("playerInitializer");
            if (initProp != null && initializerGo != null)
            {
                initProp.objectReferenceValue = initializerGo.GetComponent<OfficeFirePlayerInitializer>();
            }

            SerializedProperty listProp = so.FindProperty("scenarioControllers");
            if (listProp != null && listProp.isArray && scenarioControllerGo != null)
            {
                OfficeFireScenarioController controller = scenarioControllerGo.GetComponent<OfficeFireScenarioController>();
                if (controller != null)
                {
                    bool already = false;
                    for (int i = 0; i < listProp.arraySize; i++)
                    {
                        if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == controller)
                        {
                            already = true;
                            break;
                        }
                    }

                    if (!already)
                    {
                        int newIndex = listProp.arraySize;
                        listProp.arraySize++;
                        listProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = controller;
                    }
                }
            }
            else if (listProp == null)
            {
                componentWarnings.Add("OfficeFireScenarioBootstrapper: serialized field 'scenarioControllers' not found.");
            }

            so.ApplyModifiedProperties();
        }

        private static void LogSummary(
            List<string> created,
            List<string> reused,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Office Fire Scene] Minimal Kitchen hierarchy pass complete.");
            sb.AppendLine("--- Created GameObjects ---");
            AppendLines(sb, created);
            sb.AppendLine("--- Reused GameObjects ---");
            AppendLines(sb, reused);
            sb.AppendLine("--- Components added (this run) ---");
            AppendLines(sb, componentsAdded);
            sb.AppendLine("--- Components already present (skipped add) ---");
            AppendLines(sb, componentsAlreadyPresent);

            Debug.Log(sb.ToString());

            foreach (string w in componentWarnings)
            {
                if (!string.IsNullOrEmpty(w))
                {
                    Debug.LogWarning("[Office Fire Scene] " + w);
                }
            }
        }

        internal static void AppendLines(StringBuilder sb, List<string> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                sb.AppendLine("(none)");
                return;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                sb.AppendLine("  " + lines[i]);
            }
        }
    }
}
