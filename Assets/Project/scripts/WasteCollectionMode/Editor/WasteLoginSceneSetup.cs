#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;
using Woi.OfficeFire;
using Woi.Settings;

namespace Woi.WasteCollectionMode.Editor
{
    public static class WasteLoginSceneSetup
    {
        private const string LoginUxmlPath = "Assets/Project/WasteCollection/UI/WasteLoginScreen.uxml";
        private const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";
        private const string SceneLoaderPrefabPath = "Assets/Project/Reflex/Resources_moved/SceneLoader.prefab";
        private const string HostObjectName = "WasteLoginUI";
        private const string WasteLoginScenePath = "Assets/Project/Scenes/WasteLogin.unity";

        [MenuItem("Waste Collection/Setup Login Scene")]
        public static void SetupLoginScene()
        {
            if (!System.IO.File.Exists(WasteLoginScenePath))
            {
                Debug.LogError($"[WasteLoginSceneSetup] Scene not found at {WasteLoginScenePath}");
                return;
            }

            if (EditorSceneManager.GetActiveScene().path != WasteLoginScenePath)
                EditorSceneManager.OpenScene(WasteLoginScenePath);

            EnsureSceneLoader();
            EnsureEventSystem();
            CreateOrUpdateLoginUi();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[WasteLoginSceneSetup] WasteLogin scene is ready.");
        }

        private static void EnsureSceneLoader()
        {
            SceneLoader existing = Object.FindFirstObjectByType<SceneLoader>();
            if (existing != null)
            {
                EnsureSceneLoaderBinder(existing.gameObject);
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SceneLoaderPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[WasteLoginSceneSetup] SceneLoader prefab not found at {SceneLoaderPrefabPath}");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Add SceneLoader");
            EnsureSceneLoaderBinder(instance);
        }

        private static void EnsureSceneLoaderBinder(GameObject sceneLoaderObject)
        {
            if (sceneLoaderObject.GetComponent<OfficeFireSceneLoaderServiceBinder>() == null)
                Undo.AddComponent<OfficeFireSceneLoaderServiceBinder>(sceneLoaderObject);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystemObject = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");
            Undo.AddComponent<EventSystem>(eventSystemObject);
            Undo.AddComponent<InputSystemUIInputModule>(eventSystemObject);
        }

        private static void CreateOrUpdateLoginUi()
        {
            VisualTreeAsset loginAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LoginUxmlPath);
            if (loginAsset == null)
            {
                Debug.LogError($"[WasteLoginSceneSetup] UXML not found at {LoginUxmlPath}");
                return;
            }

            WasteLoginScreenController controller = Object.FindFirstObjectByType<WasteLoginScreenController>();
            GameObject host;

            if (controller != null)
            {
                host = controller.gameObject;
            }
            else
            {
                host = new GameObject(HostObjectName);
                Undo.RegisterCreatedObjectUndo(host, "Create WasteLoginUI");
                controller = Undo.AddComponent<WasteLoginScreenController>(host);
            }

            UIDocument document = host.GetComponent<UIDocument>();
            if (document == null)
                document = Undo.AddComponent<UIDocument>(host);

            document.visualTreeAsset = loginAsset;
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("uiDocument").objectReferenceValue = document;
            serializedController.FindProperty("loginIcon").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Project/WasteCollection/UI/IconsPng/trash-2.png");
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(host);
        }
    }
}
#endif
