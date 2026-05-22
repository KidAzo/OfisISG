using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using Woi.OfficeFire;

namespace Woi.OfficeFire.Editor
{
    /// <summary>
    /// Creates one spline path + <see cref="SplineNpcController"/> pair per evacuation NPC.
    /// Each NPC starts at t=0 on its own path; the director starts them together at runtime.
    /// </summary>
    public static class OfficeFireArchiveEvacuationNpcBuilder
    {
        public const int DefaultNpcCount = 3;

        private const string MenuPath = "Woi/Office Fire/Archive/Setup Evacuation NPCs";
        private const string BatchMenuPath = "Woi/Office Fire/Archive/Setup Evacuation NPCs In All Scenario Scenes";
        private const string LegacyToolsMenuPath = "Tools/Woi/Office Fire/Scene/Setup Archive Evacuation NPCs";
        private const string LegacyToolsBatchMenuPath =
            "Tools/Woi/Office Fire/Scene/Setup Archive Evacuation NPCs In All Scenario Scenes";
        private const float DefaultPathLength = 8f;
        private const float PathHorizontalSpacing = 3f;
        private const float StaggeredStartDelaySeconds = 0.35f;

        private static readonly string[] CharacterPrefabPaths =
        {
            "Assets/Project/Ch/ChracterMaleUpdt.prefab",
            "Assets/Project/Ch/ChracterFemaleUpdt.prefab",
        };

        private static readonly string[] ScenarioScenePaths =
        {
            "Assets/Project/Scenes/Office.unity",
            "Assets/Project/Scenes/FireModule/FireModule_Office.unity",
        };

        [MenuItem(MenuPath, false, 23)]
        [MenuItem(LegacyToolsMenuPath, false, 23)]
        private static void SetupActiveScene()
        {
            SetupEvacuationNpcsInScene(SceneManager.GetActiveScene(), DefaultNpcCount);
        }

        [MenuItem(BatchMenuPath, false, 24)]
        [MenuItem(LegacyToolsBatchMenuPath, false, 24)]
        private static void SetupAllScenarioScenes()
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
                SetupEvacuationNpcsInScene(scene, DefaultNpcCount);
                EditorSceneManager.SaveScene(scene);
            }

            if (!string.IsNullOrEmpty(previousScene))
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }
        }

        /// <summary>
        /// Invoked from Unity batch mode:
        /// -executeMethod Woi.OfficeFire.Editor.OfficeFireArchiveEvacuationNpcBuilder.BatchSetupEvacuationNpcs
        /// </summary>
        public static void BatchSetupEvacuationNpcs()
        {
            SetupAllScenarioScenes();
        }

        public static int SetupEvacuationNpcsInScene(Scene scene, int npcCount)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Office Fire Scene] Scene is not valid or not loaded: " + scene.path);
                return 0;
            }

            if (npcCount < 1)
            {
                Debug.LogWarning("[Office Fire Scene] NPC count must be at least 1.");
                return 0;
            }

            Transform archiveRoot = FindArchiveRoomRoot(scene);
            if (archiveRoot == null)
            {
                Debug.LogWarning(
                    "[Office Fire Scene] ArchiveRoom not found. Run 'Create Archive Room Hierarchy' first.");
                return 0;
            }

            Transform evacuationRoot = OfficeFireSceneHierarchyBuilder.EnsureChild(
                archiveRoot,
                "Evacuation",
                new List<string>(),
                new List<string>());
            Transform pathsFolder = OfficeFireSceneHierarchyBuilder.EnsureChild(
                evacuationRoot,
                "Paths",
                new List<string>(),
                new List<string>());
            Transform npcsFolder = OfficeFireSceneHierarchyBuilder.EnsureChild(
                evacuationRoot,
                "Npcs",
                new List<string>(),
                new List<string>());

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Archive Evacuation NPCs");
            int undoGroup = Undo.GetCurrentGroup();

            var created = new List<string>();
            var reused = new List<string>();
            var componentsAdded = new List<string>();
            var componentsAlreadyPresent = new List<string>();
            var warnings = new List<string>();

            OfficeFireSceneHierarchyBuilder.TryAddComponent<EvacuationNpcDirector>(
                evacuationRoot.gameObject,
                "EvacuationNpcDirector",
                componentsAdded,
                componentsAlreadyPresent,
                warnings);

            int createdPairs = 0;
            for (int i = 0; i < npcCount; i++)
            {
                if (EnsureNpcPair(
                        i,
                        pathsFolder,
                        npcsFolder,
                        created,
                        reused,
                        componentsAdded,
                        componentsAlreadyPresent,
                        warnings))
                {
                    createdPairs++;
                }
            }

            EvacuationNpcDirector director = evacuationRoot.GetComponent<EvacuationNpcDirector>();
            if (director != null)
            {
                Undo.RecordObject(director, "Office Fire: Refresh evacuation NPC list");
                director.RefreshControllerList();
                EditorUtility.SetDirty(director);
            }

            WireArchiveScenarioDirector(scene, director, warnings);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                $"[Office Fire Scene] Archive evacuation NPC setup complete in '{scene.path}'. " +
                $"Configured {createdPairs} NPC/path pair(s). " +
                "Move each Path_* object in the Scene view so knot 0 is the NPC start and knot 1 is the door.");

            foreach (string warning in warnings)
            {
                if (!string.IsNullOrEmpty(warning))
                {
                    Debug.LogWarning("[Office Fire Scene] " + warning);
                }
            }

            return createdPairs;
        }

        private static bool EnsureNpcPair(
            int index,
            Transform pathsFolder,
            Transform npcsFolder,
            List<string> created,
            List<string> reused,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> warnings)
        {
            string suffix = (index + 1).ToString("00");
            string pathName = "Path_Npc_" + suffix;
            string npcName = "Npc_" + suffix;

            Transform pathTransform = OfficeFireSceneHierarchyBuilder.EnsureChild(
                pathsFolder,
                pathName,
                created,
                reused);
            if (pathTransform == null || npcsFolder == null)
            {
                return false;
            }

            float horizontalOffset = (index - (DefaultNpcCount - 1) * 0.5f) * PathHorizontalSpacing;
            pathTransform.localPosition = new Vector3(horizontalOffset, 0f, 0f);

            SplineContainer splineContainer = OfficeFireSceneHierarchyBuilder.TryAddComponent<SplineContainer>(
                pathTransform.gameObject,
                "SplineContainer",
                componentsAdded,
                componentsAlreadyPresent,
                warnings);
            EvacuationPath evacuationPath = OfficeFireSceneHierarchyBuilder.TryAddComponent<EvacuationPath>(
                pathTransform.gameObject,
                "EvacuationPath",
                componentsAdded,
                componentsAlreadyPresent,
                warnings);

            EnsureDefaultSpline(splineContainer, DefaultPathLength);

            Transform npcTransform = FindDirectChild(npcsFolder, npcName);
            GameObject npcObject;
            if (npcTransform != null)
            {
                npcObject = npcTransform.gameObject;
                reused.Add(OfficeFireSceneHierarchyBuilder.FullPath(npcTransform));
            }
            else
            {
                GameObject prefab = LoadCharacterPrefab(index);
                if (prefab == null)
                {
                    warnings.Add($"Could not load character prefab for {npcName}.");
                    return false;
                }

                npcObject = PrefabUtility.InstantiatePrefab(prefab, npcsFolder) as GameObject;
                if (npcObject == null)
                {
                    warnings.Add($"Failed to instantiate character prefab for {npcName}.");
                    return false;
                }

                Undo.RegisterCreatedObjectUndo(npcObject, "Office Fire: Create evacuation NPC");
                npcObject.name = npcName;
                created.Add(OfficeFireSceneHierarchyBuilder.FullPath(npcObject.transform));
            }

            SplineNpcController controller = OfficeFireSceneHierarchyBuilder.TryAddComponent<SplineNpcController>(
                npcObject,
                "SplineNpcController",
                componentsAdded,
                componentsAlreadyPresent,
                warnings);
            if (controller == null || evacuationPath == null)
            {
                return false;
            }

            WireNpcController(controller, evacuationPath, index);
            controller.SnapToPathStart(storeAsResetPose: true);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(npcObject);

            return true;
        }

        private static void WireNpcController(SplineNpcController controller, EvacuationPath evacuationPath, int index)
        {
            Undo.RecordObject(controller, "Office Fire: Wire SplineNpcController");

            SerializedObject so = new SerializedObject(controller);
            SerializedProperty pathProp = so.FindProperty("path");
            if (pathProp != null)
            {
                pathProp.objectReferenceValue = evacuationPath;
            }

            SerializedProperty modeProp = so.FindProperty("locomotionMode");
            if (modeProp != null)
            {
                modeProp.enumValueIndex = (int)NpcLocomotionMode.Walk;
            }

            SerializedProperty startTProp = so.FindProperty("startNormalizedT");
            if (startTProp != null)
            {
                startTProp.floatValue = 0f;
            }

            SerializedProperty delayProp = so.FindProperty("startDelay");
            if (delayProp != null)
            {
                delayProp.floatValue = index * StaggeredStartDelaySeconds;
            }

            SerializedProperty animatorProp = so.FindProperty("animator");
            if (animatorProp != null && animatorProp.objectReferenceValue == null)
            {
                Animator animator = controller.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    animatorProp.objectReferenceValue = animator;
                }
            }

            SerializedProperty endBehaviourProp = so.FindProperty("endBehaviour");
            if (endBehaviourProp != null)
            {
                endBehaviourProp.enumValueIndex = 2;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureDefaultSpline(SplineContainer container, float length)
        {
            if (container == null)
            {
                return;
            }

            Spline spline = container.Spline;
            if (spline != null && spline.Count >= 2)
            {
                return;
            }

            Undo.RecordObject(container, "Office Fire: Create default evacuation spline");

            if (spline == null)
            {
                spline = new Spline();
                container.AddSpline(spline);
            }
            else
            {
                spline.Clear();
            }

            spline.Add(new BezierKnot(new float3(0f, 0f, 0f)), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(new float3(0f, 0f, length)), TangentMode.AutoSmooth);
            EditorUtility.SetDirty(container);
        }

        private static void WireArchiveScenarioDirector(
            Scene scene,
            EvacuationNpcDirector director,
            List<string> warnings)
        {
            if (director == null)
            {
                return;
            }

            ArchiveRoomScenarioController controller = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length && controller == null; i++)
            {
                controller = roots[i].GetComponentInChildren<ArchiveRoomScenarioController>(true);
            }

            if (controller == null)
            {
                warnings.Add("ArchiveRoomScenarioController not found — wire EvacuationNpcDirector manually.");
                return;
            }

            Undo.RecordObject(controller, "Office Fire: Wire evacuation NPC director");
            SerializedObject so = new SerializedObject(controller);
            SerializedProperty directorProp = so.FindProperty("evacuationNpcDirector");
            if (directorProp != null)
            {
                directorProp.objectReferenceValue = director;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }
        }

        private static GameObject LoadCharacterPrefab(int index)
        {
            for (int attempt = 0; attempt < CharacterPrefabPaths.Length; attempt++)
            {
                string path = CharacterPrefabPaths[(index + attempt) % CharacterPrefabPaths.Length];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    return prefab;
                }
            }

            return null;
        }

        public static Transform FindArchiveRoomRoot(Scene scene)
        {
            Transform modulesRoot = FindSceneTransform(scene, "======FireModules======");
            if (modulesRoot == null)
            {
                return null;
            }

            Transform scenarios = FindDirectChild(modulesRoot, "03_Scenarios");
            return scenarios != null ? FindDirectChild(scenarios, "ArchiveRoom") : null;
        }

        private static Transform FindSceneTransform(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == name)
                {
                    return roots[i].transform;
                }
            }

            return null;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private const string RestoreSplinesMenuPath = "Woi/Office Fire/Archive/Restore Saved Evacuation Splines";
        private const string LegacyRestoreSplinesMenuPath =
            "Tools/Woi/Office Fire/Scene/Restore Saved Evacuation Splines";

        private readonly struct KnotSnapshot
        {
            public readonly float3 Position;
            public readonly float3 TangentIn;
            public readonly float3 TangentOut;
            public readonly quaternion Rotation;

            public KnotSnapshot(float3 position, float3 tangentIn, float3 tangentOut, quaternion rotation)
            {
                Position = position;
                TangentIn = tangentIn;
                TangentOut = tangentOut;
                Rotation = rotation;
            }
        }

        private readonly struct PathSnapshot
        {
            public readonly string PathObjectName;
            public readonly Vector3 LocalPosition;
            public readonly KnotSnapshot[] Knots;

            public PathSnapshot(string pathObjectName, Vector3 localPosition, KnotSnapshot[] knots)
            {
                PathObjectName = pathObjectName;
                LocalPosition = localPosition;
                Knots = knots;
            }
        }

        private static readonly PathSnapshot[] SavedEvacuationPaths =
        {
            new PathSnapshot(
                "Path_Npc_02",
                new Vector3(-54.45668f, 0f, -21.626102f),
                new[]
                {
                    K(0f, 0f, 0f, 0f, 0f, -0.20251334f, 0f, 0f, 0.20251334f, 2.6554148e-15f, 0.7140221f, 2.603725e-15f, 0.7001231f),
                    K(2.0247421f, 0f, -0.03980446f, -0.00000011920929f, -2.6645353e-15f, -0.98812646f, 0.00000023841858f, 8.881784e-16f, 2.0642517f, 0.0000000031047107f, 0.71402204f, -0.0000000031663432f, 0.70012325f),
                    K(7.5075264f, 0f, -0.14758873f, 0f, 0f, 0f, 0f, 0f, 0f, 0.70710665f, 0f, 0f, 0.7071069f),
                    K(2.0247421f, 0f, -0.03980446f, 0f, 0f, -0.015535113f, 0f, 0f, 0.024327323f, -0.000000001915003f, 0.9999115f, 2.5483065e-11f, -0.013305884f),
                    K(15.464764f, 0f, -0.49131775f, 0f, 0f, -4.2626867f, 0f, 0f, 3.8655329f, -8.101132e-12f, 0.71200407f, -7.989301e-12f, 0.7021754f),
                    K(26.523092f, 0f, -0.4273777f, 0f, 0f, -3.57993f, 0f, 0f, 3.386433f, -4.7114066e-12f, 0.688426f, -4.963807e-12f, 0.7253066f),
                    K(36.37047f, 0f, 0.5461731f, 0f, 0f, -2.036068f, 0f, 0f, 0.908953f, 0.36484084f, 0.56229806f, 0.40392748f, 0.622539f),
                    K(38.331787f, 0f, 0.7521858f, 0f, 0f, -0.9625849f, 0f, 0f, 2.6318202f, 0.000007991918f, 0.67351377f, 0.0000087710505f, 0.73917466f),
                    K(53.025597f, 0f, 1.9469147f, 0f, 0f, -2.1624053f, 0f, 0f, 0.6100249f, 0.4796355f, 0.48438692f, 0.51480204f, 0.5199018f),
                    K(54.196705f, 0f, 2.0175762f, 0f, 0f, -0.47521412f, 0f, 0f, 0.8549422f, 0.0009469509f, 0.7964715f, 0.0007189177f, 0.6046749f),
                    K(57.321278f, 0f, -0.14041519f, 0f, 0f, -0.37973523f, 0f, 0f, 0.37973523f, 0.0000000024623907f, 0.8855192f, 0.0000000012919354f, 0.4646028f),
                }),
            new PathSnapshot(
                "Path_Npc_03",
                Vector3.zero,
                new[]
                {
                    K(-38.2f, 0f, 1.5f, 0f, 0f, -1.5110831f, 0f, 0f, 1.5110831f, -5.282459e-16f, 0.688299f, -5.5674046e-16f, 0.72542715f),
                    K(-23.11f, 0f, 2.2931533f, 0f, 0f, -3.5344067f, 0f, 0f, 1.9106256f, 7.8590676e-12f, 0.6824694f, 8.416939e-12f, 0.7309141f),
                    K(-18.71f, 0f, 2.666f, 0f, 0f, -1.9841026f, 0f, 0f, 4.1169095f, 0.48373327f, 0.49820998f, 0.50126076f, 0.516262f),
                    K(0.3f, 0f, 2.4119084f, 0f, 0f, -3.4353373f, 0f, 0f, 1.4121189f, -0.46403766f, 0.6885555f, -0.3114427f, 0.46212965f),
                    K(2.62f, 0f, 0.19f, 0f, 0f, -0.3212363f, 0f, 0f, 0.3212363f, -0.6396132f, 0.660857f, -0.27306375f, 0.2821332f),
                }),
        };

        [MenuItem(RestoreSplinesMenuPath, false, 25)]
        [MenuItem(LegacyRestoreSplinesMenuPath, false, 25)]
        private static void RestoreSavedSplinesInActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Office Fire Scene] Active scene is not valid or not loaded.");
                return;
            }

            Transform pathsFolder = FindEvacuationPathsFolder(scene);
            if (pathsFolder == null)
            {
                Debug.LogWarning(
                    "[Office Fire Scene] Evacuation/Paths folder not found. Run 'Setup Evacuation NPCs' first.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Restore Saved Evacuation Splines");
            int undoGroup = Undo.GetCurrentGroup();

            int restored = 0;
            for (int i = 0; i < SavedEvacuationPaths.Length; i++)
            {
                if (TryRestoreSavedPath(pathsFolder, SavedEvacuationPaths[i]))
                {
                    restored++;
                }
            }

            SnapNpcsToRestoredPaths(pathsFolder.parent);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                $"[Office Fire Scene] Restored {restored}/{SavedEvacuationPaths.Length} saved evacuation spline(s) in '{scene.path}'. " +
                "Path_Npc_01 was not found in backup — reposition manually if needed.");
        }

        private static bool TryRestoreSavedPath(Transform pathsFolder, PathSnapshot snapshot)
        {
            Transform pathTransform = FindDirectChild(pathsFolder, snapshot.PathObjectName);
            if (pathTransform == null)
            {
                Debug.LogWarning(
                    $"[Office Fire Scene] Missing '{snapshot.PathObjectName}'. Run 'Setup Evacuation NPCs' first.");
                return false;
            }

            SplineContainer container = pathTransform.GetComponent<SplineContainer>();
            if (container == null)
            {
                Debug.LogWarning($"[Office Fire Scene] '{snapshot.PathObjectName}' has no SplineContainer.");
                return false;
            }

            Undo.RecordObject(pathTransform, "Restore evacuation path transform");
            pathTransform.localPosition = snapshot.LocalPosition;

            Undo.RecordObject(container, "Restore evacuation spline");
            ApplySavedKnots(container, snapshot.Knots);
            EditorUtility.SetDirty(container);
            EditorUtility.SetDirty(pathTransform);

            return true;
        }

        private static void ApplySavedKnots(SplineContainer container, KnotSnapshot[] knots)
        {
            Spline spline = container.Spline;
            if (spline == null)
            {
                spline = new Spline();
                container.AddSpline(spline);
            }
            else
            {
                spline.Clear();
            }

            for (int i = 0; i < knots.Length; i++)
            {
                KnotSnapshot knot = knots[i];
                spline.Add(
                    new BezierKnot(knot.Position, knot.TangentIn, knot.TangentOut, knot.Rotation),
                    TangentMode.Broken);
            }
        }

        private static void SnapNpcsToRestoredPaths(Transform evacuationRoot)
        {
            if (evacuationRoot == null)
            {
                return;
            }

            SplineNpcController[] controllers = evacuationRoot.GetComponentsInChildren<SplineNpcController>(true);
            for (int i = 0; i < controllers.Length; i++)
            {
                SplineNpcController controller = controllers[i];
                if (controller == null || controller.Path == null)
                {
                    continue;
                }

                Undo.RecordObject(controller.transform, "Snap NPC to restored path start");
                controller.SnapToPathStart(storeAsResetPose: true);
                EditorUtility.SetDirty(controller);
            }
        }

        private static Transform FindEvacuationPathsFolder(Scene scene)
        {
            Transform archiveRoot = FindArchiveRoomRoot(scene);
            if (archiveRoot == null)
            {
                return null;
            }

            Transform evacuation = FindDirectChild(archiveRoot, "Evacuation");
            return evacuation != null ? FindDirectChild(evacuation, "Paths") : null;
        }

        private static KnotSnapshot K(
            float px, float py, float pz,
            float tix, float tiy, float tiz,
            float tox, float toy, float toz,
            float rx, float ry, float rz, float rw)
        {
            return new KnotSnapshot(
                new float3(px, py, pz),
                new float3(tix, tiy, tiz),
                new float3(tox, toy, toz),
                new quaternion(rx, ry, rz, rw));
        }
    }
}
