#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using Woi.OfficeFire;
using Woi.Settings;

namespace Woi.OfficeFire.Editor
{
    public static class OfficeFireLoginSceneSetup
    {
        private const string LoginUxmlPath = "Assets/Project/OfficeFire/UI/OfficeFireLoginScreen.uxml";
        private const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";
        private const string SceneLoaderPrefabPath = "Assets/Project/Reflex/Resources_moved/SceneLoader.prefab";
        private const string GaussImagePath = "Assets/Project/Sprites/gaussImage.jpg";
        private const string HostObjectName = "OfficeFireLoginUI";
        private const string LoginScenePath = "Assets/Project/Scenes/FireModule/OfficeFireModule_Login.unity";

        [MenuItem("Office Fire/Setup Login Scene")]
        public static void SetupLoginScene()
        {
            if (!System.IO.File.Exists(LoginScenePath))
            {
                Debug.LogError($"[OfficeFireLoginSceneSetup] Scene not found at {LoginScenePath}");
                return;
            }

            if (EditorSceneManager.GetActiveScene().path != LoginScenePath)
                EditorSceneManager.OpenScene(LoginScenePath);

            EnsureSceneLoader();
            EnsureEventSystem();
            CreateOrUpdateLoginUi();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[OfficeFireLoginSceneSetup] OfficeFireModule_Login scene is ready.");
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
                Debug.LogError($"[OfficeFireLoginSceneSetup] SceneLoader prefab not found at {SceneLoaderPrefabPath}");
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
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            GameObject eventSystemObject;

            if (eventSystem != null)
            {
                eventSystemObject = eventSystem.gameObject;
            }
            else
            {
                eventSystemObject = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");
                Undo.AddComponent<EventSystem>(eventSystemObject);
            }

            if (eventSystemObject.GetComponent(GetInputSystemUiInputModuleType()) == null)
            {
                System.Type inputModuleType = GetInputSystemUiInputModuleType();
                if (inputModuleType != null)
                {
                    Undo.AddComponent(eventSystemObject, inputModuleType);
                }
                else
                {
                    Debug.LogWarning(
                        "[OfficeFireLoginSceneSetup] InputSystemUIInputModule not found — add input module to EventSystem manually.");
                }
            }

            // Remove stray runtime panel helper objects accidentally parented under EventSystem.
            for (int i = eventSystemObject.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = eventSystemObject.transform.GetChild(i);
                if (child.name == "PanelSettings")
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static System.Type GetInputSystemUiInputModuleType()
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                "Packages/com.unity.inputsystem/InputSystem/Plugins/UI/InputSystemUIInputModule.cs");

            if (script != null)
                return script.GetClass();

            return System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        }

        private static void CreateOrUpdateLoginUi()
        {
            VisualTreeAsset loginAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LoginUxmlPath);
            if (loginAsset == null)
            {
                Debug.LogError($"[OfficeFireLoginSceneSetup] UXML not found at {LoginUxmlPath}");
                return;
            }

            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
                Debug.LogWarning($"[OfficeFireLoginSceneSetup] PanelSettings not found at {PanelSettingsPath} — assign manually.");

            OfficeFireLoginScreenController controller =
                Object.FindFirstObjectByType<OfficeFireLoginScreenController>();

            GameObject host;
            if (controller != null)
            {
                host = controller.gameObject;
            }
            else
            {
                host = new GameObject(HostObjectName);
                Undo.RegisterCreatedObjectUndo(host, "Create OfficeFireLoginUI");
                controller = Undo.AddComponent<OfficeFireLoginScreenController>(host);
            }

            UIDocument document = host.GetComponent<UIDocument>();
            if (document == null)
                document = Undo.AddComponent<UIDocument>(host);

            document.visualTreeAsset = loginAsset;
            if (panelSettings != null)
                document.panelSettings = panelSettings;

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("uiDocument").objectReferenceValue = document;
            serializedController.FindProperty("gaussBackgroundImage").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(GaussImagePath);
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(host);
        }
    }
}
#endif
