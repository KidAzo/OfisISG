using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Woi.OfficeFire.Editor
{
    /// <summary>
    /// Editor utilities to strip missing MonoBehaviour script slots from the active scene or selection hierarchies.
    /// </summary>
    public static class RemoveMissingScriptsCleanupMenu
    {
        private const string MenuRoot = "Tools/Woi/Office Fire/Cleanup/";

        [MenuItem(MenuRoot + "Remove Missing Scripts In Active Scene", false, 10)]
        private static void RemoveMissingScriptsInActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Office Fire Cleanup] Active scene is not valid or not loaded.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Remove Missing Scripts (Active Scene)");
            int undoGroup = Undo.GetCurrentGroup();

            int totalRemoved = 0;
            int gameObjectsAffected = 0;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null)
                {
                    continue;
                }

                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    if (transforms[t] != null)
                    {
                        ProcessGameObject(transforms[t].gameObject, ref totalRemoved, ref gameObjectsAffected, null);
                    }
                }
            }

            if (totalRemoved > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                $"[Office Fire Cleanup] Active scene '{scene.name}': removed {totalRemoved} missing script(s) on {gameObjectsAffected} GameObject(s).");
        }

        [MenuItem(MenuRoot + "Remove Missing Scripts In Selected Objects", false, 11)]
        private static void RemoveMissingScriptsInSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogWarning("[Office Fire Cleanup] No GameObjects selected.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Remove Missing Scripts (Selection)");
            int undoGroup = Undo.GetCurrentGroup();

            int totalRemoved = 0;
            int gameObjectsAffected = 0;
            var scenesToDirty = new HashSet<Scene>();
            var uniqueTargets = new HashSet<GameObject>();

            for (int i = 0; i < selection.Length; i++)
            {
                GameObject root = selection[i];
                if (root == null)
                {
                    continue;
                }

                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    if (transforms[t] != null)
                    {
                        uniqueTargets.Add(transforms[t].gameObject);
                    }
                }
            }

            foreach (GameObject go in uniqueTargets)
            {
                ProcessGameObject(go, ref totalRemoved, ref gameObjectsAffected, scenesToDirty);
            }

            foreach (Scene s in scenesToDirty)
            {
                if (s.IsValid() && s.isLoaded)
                {
                    EditorSceneManager.MarkSceneDirty(s);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                $"[Office Fire Cleanup] Selection ({selection.Length} root object(s)): removed {totalRemoved} missing script(s) on {gameObjectsAffected} GameObject(s).");
        }

        [MenuItem(MenuRoot + "Remove Missing Scripts In Selected Objects", true, 11)]
        private static bool RemoveMissingScriptsInSelectionValidate()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        private static void ProcessGameObject(
            GameObject go,
            ref int totalRemoved,
            ref int gameObjectsAffected,
            HashSet<Scene> scenesToDirty)
        {
            if (go == null)
            {
                return;
            }

            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missingCount == 0)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(go, "Office Fire: Remove Missing Scripts");
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (removed > 0)
            {
                totalRemoved += removed;
                gameObjectsAffected++;
                EditorUtility.SetDirty(go);

                if (scenesToDirty != null && go.scene.IsValid())
                {
                    scenesToDirty.Add(go.scene);
                }
            }
        }
    }
}
