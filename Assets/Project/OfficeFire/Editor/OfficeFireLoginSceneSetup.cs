#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using Woi.OfficeFire;

namespace Woi.OfficeFire.Editor
{
    public static class OfficeFireLoginSceneSetup
    {
        private const string LoginUxmlPath = "Assets/Project/OfficeFire/UI/OfficeFireLoginScreen.uxml";
        private const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";
        private const string WorldPanelSettingsPath =
            "Assets/Project/OfficeFire/UI/InteractHoverWorldPanelSettings.asset";
        private const string GaussImagePath = "Assets/Project/Sprites/gaussImage.jpg";
        private const string HostObjectName = "OfficeFireLoginUI";
        private const string LegacyRigObjectName = "OfficeFireLoginRig";
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

            RemoveLegacyLoginRigObject();
            EnsureEventSystem();
            CreateOrUpdateLoginUi();
            EnsureLoginWorldUiPresenter();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[OfficeFireLoginSceneSetup] OfficeFireModule_Login: 3D login UI wired (SceneLoader not added — use bootstrap DDOL instance).");
        }

        private static void RemoveLegacyLoginRigObject()
        {
            GameObject legacy = GameObject.Find(LegacyRigObjectName);
            if (legacy != null)
                Undo.DestroyObjectImmediate(legacy);
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

        private static void EnsureLoginWorldUiPresenter()
        {
            OfficeFireLoginScreenController controller =
                Object.FindFirstObjectByType<OfficeFireLoginScreenController>(FindObjectsInactive.Include);

            if (controller == null)
            {
                Debug.LogWarning("[OfficeFireLoginSceneSetup] No OfficeFireLoginScreenController — skip VR world UI wiring.");
                return;
            }

            GameObject host = controller.gameObject;
            OfficeFireLoginWorldUiPresenter presenter = host.GetComponent<OfficeFireLoginWorldUiPresenter>();
            if (presenter == null)
                presenter = Undo.AddComponent<OfficeFireLoginWorldUiPresenter>(host);

            PanelSettings worldPanel = AssetDatabase.LoadAssetAtPath<PanelSettings>(WorldPanelSettingsPath);
            PanelSettings screenPanel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            SerializedObject presenterSo = new SerializedObject(presenter);
            presenterSo.FindProperty("uiDocument").objectReferenceValue = host.GetComponent<UIDocument>();
            presenterSo.FindProperty("worldPanelSettingsSource").objectReferenceValue = worldPanel;
            presenterSo.FindProperty("screenPanelSettingsSource").objectReferenceValue = screenPanel;

            GameObject xrOrigin = FindXrOriginRoot();
            if (xrOrigin != null)
                presenterSo.FindProperty("xrRigRoot").objectReferenceValue = xrOrigin.transform;

            presenterSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(host);
        }

        private static GameObject FindXrOriginRoot()
        {
            System.Type originType = System.Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (originType == null)
                return null;

            Object[] found = Resources.FindObjectsOfTypeAll(originType);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] is not Component origin || origin == null)
                    continue;

                GameObject go = origin.gameObject;
                if (!go.scene.IsValid())
                    continue;

                return go;
            }

            return null;
        }
    }
}
#endif
