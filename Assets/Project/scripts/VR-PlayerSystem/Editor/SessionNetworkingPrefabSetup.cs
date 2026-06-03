#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Woi.WasteCollectionMode;

namespace Woi.DataHandler.Editor
{
    public static class SessionNetworkingPrefabSetup
    {
        private const string NetworkingPrefabPath = "Assets/Project/Prefabs/NetworkingSystem.prefab";
        private const string OverlayUxmlPath = "Assets/Project/VR-Networking/UI/SessionProfileOverlay.uxml";
        private const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";
        private const string WorldPanelSettingsPath =
            "Assets/Project/OfficeFire/UI/InteractHoverWorldPanelSettings.asset";
        private const string BootstrapperScenePath = "Assets/Project/Scenes/FireModule/FireModule_Bootstrapper.unity";
        private const string ProfileUiTemplateName = "SessionProfileUI_Template";

        [MenuItem("Woi/VR Networking/Setup Session UI On Networking Prefab")]
        public static void SetupNetworkingPrefab()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(NetworkingPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[SessionNetworkingPrefabSetup] Prefab not found: {NetworkingPrefabPath}");
                return;
            }

            try
            {
                EnsureSessionUi(prefabRoot);
                EnsureGameplayGate(prefabRoot);
                EnsureVrLocomotionGate(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, NetworkingPrefabPath);
                Debug.Log("[SessionNetworkingPrefabSetup] NetworkingSystem prefab updated.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [MenuItem("Woi/VR Networking/Add Networking Prefab To Bootstrapper Scene")]
        public static void AddNetworkingToBootstrapper()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(BootstrapperScenePath);
            if (Object.FindFirstObjectByType<SessionManager>() != null)
            {
                Debug.Log("[SessionNetworkingPrefabSetup] SessionManager already present in bootstrapper scene.");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkingPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[SessionNetworkingPrefabSetup] Missing prefab: {NetworkingPrefabPath}");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Add NetworkingSystem");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[SessionNetworkingPrefabSetup] NetworkingSystem added to FireModule_Bootstrapper.");
        }

        private static void EnsureSessionUi(GameObject networkingRoot)
        {
            Transform existing = networkingRoot.transform.Find(ProfileUiTemplateName);
            if (existing == null)
                existing = networkingRoot.transform.Find("SessionProfileUI");

            GameObject host = existing != null ? existing.gameObject : new GameObject(ProfileUiTemplateName);
            host.name = ProfileUiTemplateName;
            if (existing == null)
                host.transform.SetParent(networkingRoot.transform, false);

            UIDocument document = host.GetComponent<UIDocument>();
            if (document == null)
                document = host.AddComponent<UIDocument>();

            SessionProfileOverlayController overlay = host.GetComponent<SessionProfileOverlayController>();
            if (overlay == null)
                overlay = host.AddComponent<SessionProfileOverlayController>();

            SessionProfileWorldUiPresenter worldPresenter = host.GetComponent<SessionProfileWorldUiPresenter>();
            if (worldPresenter == null)
                worldPresenter = host.AddComponent<SessionProfileWorldUiPresenter>();

            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            PanelSettings worldPanelSettings =
                AssetDatabase.LoadAssetAtPath<PanelSettings>(WorldPanelSettingsPath);
            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(OverlayUxmlPath);

            if (worldPanelSettings != null)
                document.panelSettings = worldPanelSettings;
            else if (panelSettings != null)
                document.panelSettings = panelSettings;

            if (uxml != null)
                document.visualTreeAsset = uxml;

            document.sortingOrder = 200;
            host.SetActive(true);
        }

        private static void EnsureGameplayGate(GameObject networkingRoot)
        {
            SessionGameplayGate gate = networkingRoot.GetComponent<SessionGameplayGate>();
            if (gate == null)
                gate = networkingRoot.AddComponent<SessionGameplayGate>();

            SessionProfileOverlayController overlay =
                networkingRoot.GetComponentInChildren<SessionProfileOverlayController>(true);
            Transform uiRoot = networkingRoot.transform.Find(ProfileUiTemplateName);
            if (uiRoot == null)
                uiRoot = networkingRoot.transform.Find("SessionProfileUI");

            GameObject uiTemplate = uiRoot != null ? uiRoot.gameObject : overlay != null ? overlay.gameObject : null;

            SerializedObject so = new SerializedObject(gate);
            so.FindProperty("sessionProfileUiRoot").objectReferenceValue = uiTemplate;
            so.FindProperty("profileOverlay").objectReferenceValue = null;
            SerializedProperty blocked = so.FindProperty("blockedSceneNames");
            blocked.arraySize = 3;
            blocked.GetArrayElementAtIndex(0).stringValue = "FireModule_Bootstrapper";
            blocked.GetArrayElementAtIndex(1).stringValue = "Bootstrapper";
            blocked.GetArrayElementAtIndex(2).stringValue = "WasteLogin";
            SerializedProperty overlayScenes = so.FindProperty("sessionOverlaySceneNames");
            overlayScenes.arraySize = 1;
            overlayScenes.GetArrayElementAtIndex(0).stringValue = "FireModule_Office";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureVrLocomotionGate(GameObject networkingRoot)
        {
            WasteVrLocomotionGate gate = networkingRoot.GetComponent<WasteVrLocomotionGate>();
            if (gate == null)
                gate = networkingRoot.AddComponent<WasteVrLocomotionGate>();

            SessionGameplayGate sessionGate = networkingRoot.GetComponent<SessionGameplayGate>();
            if (sessionGate == null)
                return;

            SerializedObject so = new SerializedObject(sessionGate);
            so.FindProperty("vrLocomotionGate").objectReferenceValue = gate;
            so.FindProperty("teleportPlayerOnScenarioStart").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
