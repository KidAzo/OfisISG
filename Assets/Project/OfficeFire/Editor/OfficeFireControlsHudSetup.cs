using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Woi.OfficeFire.Editor
{
    public static class OfficeFireControlsHudSetup
    {
        private const string UxmlPath = "Assets/Project/OfficeFire/UI/KeyBindings/ControlsHUD.uxml";
        private const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";
        private const string PrefabPath = "Assets/Project/OfficeFire/Prefabs/OfficeFireControlsHUD.prefab";
        private const string OfficeScenePath = "Assets/Project/Scenes/FireModule/FireModule_Office.unity";

        [MenuItem("Woi/Office Fire/Setup Controls HUD In Active Scene")]
        public static void SetupInActiveScene()
        {
            if (Object.FindAnyObjectByType<OfficeFireControlsHUDController>(FindObjectsInactive.Include) != null)
            {
                Debug.Log("[OfficeFireControlsHudSetup] Controls HUD already exists in the active scene.");
                return;
            }

            GameObject hud = CreateHudObject();
            Undo.RegisterCreatedObjectUndo(hud, "Add Office Fire Controls HUD");
            EditorSceneManager.MarkSceneDirty(hud.scene);
            Selection.activeGameObject = hud;
            Debug.Log("[OfficeFireControlsHudSetup] Controls HUD added to the active scene.", hud);
        }

        [MenuItem("Woi/Office Fire/Add Controls HUD To Office Scene")]
        public static void SetupInOfficeScene()
        {
            if (!System.IO.File.Exists(OfficeScenePath))
            {
                Debug.LogError($"[OfficeFireControlsHudSetup] Scene not found: {OfficeScenePath}");
                return;
            }

            var scene = EditorSceneManager.OpenScene(OfficeScenePath, OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<OfficeFireControlsHUDController>(FindObjectsInactive.Include) != null)
            {
                Debug.Log("[OfficeFireControlsHudSetup] Controls HUD already exists in FireModule_Office.");
                return;
            }

            GameObject hud = CreateHudObject();
            Undo.RegisterCreatedObjectUndo(hud, "Add Office Fire Controls HUD");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = hud;
            Debug.Log("[OfficeFireControlsHudSetup] Controls HUD added to FireModule_Office.", hud);
        }

        [MenuItem("Woi/Office Fire/Create Controls HUD Prefab")]
        public static void CreatePrefab()
        {
            GameObject hud = CreateHudObject();
            EnsurePrefabDirectory();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(hud, PrefabPath);
            Object.DestroyImmediate(hud);

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                Debug.Log($"[OfficeFireControlsHudSetup] Prefab created at {PrefabPath}", prefab);
            }
        }

        private static GameObject CreateHudObject()
        {
            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            if (uxml == null)
            {
                throw new System.InvalidOperationException($"UXML not found: {UxmlPath}");
            }

            if (panelSettings == null)
            {
                throw new System.InvalidOperationException($"PanelSettings not found: {PanelSettingsPath}");
            }

            var go = new GameObject("OfficeFireControlsHUD");
            UIDocument document = go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = uxml;
            document.sortingOrder = 100;
            go.AddComponent<OfficeFireControlsHUDController>();
            return go;
        }

        private static void EnsurePrefabDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Project/OfficeFire/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/Project/OfficeFire", "Prefabs");
            }
        }
    }
}
