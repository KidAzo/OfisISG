#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Woi.OfficeFire;

namespace Woi.Editor.OfficeFire
{
    /// <summary>
    /// Ensures evacuation NPCs keep a locomotion controller reference after Addressables/Android builds.
    /// </summary>
    public static class OfficeFireNpcAnimationFix
    {
        const string AllAnimsPath = "Assets/Project/Characters/AnimationControllers/AllAnims.controller";
        const string CharacterPrefabPath = "Assets/Project/Ch/ChracterMaleUpdt.prefab";
        const string CharacterFbxPath = "Assets/Project/Ch/ChracterMaleUpdt.fbx";
        const string MainScenePath = "Assets/Project/Scenes/FireModule/FireModule_Office.unity";

        [MenuItem("Woi/Office Fire/Fix NPC Animation References", priority = 35)]
        public static void FixNpcAnimationReferences()
        {
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AllAnimsPath);
            Avatar humanoidAvatar = LoadHumanoidAvatar();
            if (controller == null)
            {
                EditorUtility.DisplayDialog(
                    "Office Fire",
                    $"AllAnims controller not found at:\n{AllAnimsPath}",
                    "OK");
                return;
            }

            int prefabFixed = FixPrefabAnimatorAndOverride(controller, humanoidAvatar);
            int sceneFixed = FixSceneSplineNpcControllers(controller, humanoidAvatar);

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Office Fire",
                $"NPC animation references updated.\n\n" +
                $"Prefab animator/override: {prefabFixed} change(s)\n" +
                $"Scene SplineNpcController override: {sceneFixed} instance(s)\n\n" +
                "Next: Woi → Addressables → Configure Office Safety Module, then rebuild Android Addressables.",
                "OK");
        }

        static Avatar LoadHumanoidAvatar()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(CharacterFbxPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Avatar avatar)
                    return avatar;
            }

            return null;
        }

        static int FixPrefabAnimatorAndOverride(RuntimeAnimatorController controller, Avatar humanoidAvatar)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CharacterPrefabPath);
            if (prefabRoot == null)
                return 0;

            int changes = 0;
            Animator[] animators = prefabRoot.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator.runtimeAnimatorController != controller)
                {
                    animator.runtimeAnimatorController = controller;
                    changes++;
                }

                if (humanoidAvatar != null && animator.avatar != humanoidAvatar)
                {
                    animator.avatar = humanoidAvatar;
                    changes++;
                }
            }

            SplineNpcController[] controllers = prefabRoot.GetComponentsInChildren<SplineNpcController>(true);
            for (int i = 0; i < controllers.Length; i++)
            {
                if (ApplyOverride(controllers[i], controller, humanoidAvatar))
                    changes++;
            }

            if (changes > 0)
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, CharacterPrefabPath);

            PrefabUtility.UnloadPrefabContents(prefabRoot);
            return changes;
        }

        static int FixSceneSplineNpcControllers(RuntimeAnimatorController controller, Avatar humanoidAvatar)
        {
            if (!System.IO.File.Exists(MainScenePath))
                return 0;

            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            SplineNpcController[] controllers =
                Object.FindObjectsByType<SplineNpcController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int fixedCount = 0;
            for (int i = 0; i < controllers.Length; i++)
            {
                if (ApplyOverride(controllers[i], controller, humanoidAvatar))
                    fixedCount++;
            }

            if (fixedCount > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            return fixedCount;
        }

        static bool ApplyOverride(SplineNpcController npc, RuntimeAnimatorController controller, Avatar humanoidAvatar)
        {
            SerializedObject so = new SerializedObject(npc);
            SerializedProperty overrideProp = so.FindProperty("locomotionControllerOverride");
            SerializedProperty avatarProp = so.FindProperty("humanoidAvatarOverride");
            bool changed = false;

            if (overrideProp != null && overrideProp.objectReferenceValue != controller)
            {
                overrideProp.objectReferenceValue = controller;
                changed = true;
            }

            if (avatarProp != null && humanoidAvatar != null && avatarProp.objectReferenceValue != humanoidAvatar)
            {
                avatarProp.objectReferenceValue = humanoidAvatar;
                changed = true;
            }

            if (!changed)
                return false;

            Undo.RecordObject(npc, "Fix NPC locomotion/avatar overrides");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(npc);
            return true;
        }
    }
}
#endif
