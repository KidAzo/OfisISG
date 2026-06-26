#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Woi.OfficeFire;

namespace Woi.OfficeFire.Editor
{
    /// <summary>
    /// Wires VR extinguisher grab/pin/hover on XR Origin — mirrors WOI.Shared.Global XR Rig setup.
    /// </summary>
    public static class OfficeFireVrExtinguisherSetup
    {
        private const string MenuPath = "Tools/Woi/Office Fire/Scene/Wire VR Extinguisher";

        [MenuItem(MenuPath, false, 25)]
        private static void WireVrExtinguisherActiveScene()
        {
            WireVrExtinguisherInScene(SceneManager.GetActiveScene());
        }

        [MenuItem(MenuPath, true, 25)]
        private static bool WireVrExtinguisherActiveSceneValidate()
        {
            return !Application.isPlaying;
        }

        public static void WireVrExtinguisherInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Office Fire VR Extinguisher] Scene is not valid or not loaded.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Wire VR Extinguisher");
            int undoGroup = Undo.GetCurrentGroup();

            OfficeFireVrExtinguisherRigWiring.EnsureWired(logResult: true, ignoreVrModeCheck: true);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
        }
    }
}
#endif
