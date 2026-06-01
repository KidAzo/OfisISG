#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Woi.SelectionSystem;

namespace Woi.WasteCollectionMode.Editor
{
    public static class WasteResultScreenSceneSetup
    {
        private const string SelectionUxmlPath = "Assets/Project/WasteCollection/UI/WasteSelectionMenu.uxml";
        private const string IconLibraryPath = "Assets/Project/WasteCollection/UI/WasteBinIconLibrary.asset";
        private const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";
        private const string HostObjectName = "WasteCollectionUI";
        private const string LegacyResultObjectName = "WasteResultScreenUI";

        [MenuItem("Waste Collection/Setup Result Screen In Scene")]
        public static void SetupResultScreenInScene()
        {
            WasteSelectionMenu selectionMenu = FindSelectionMenu();
            if (selectionMenu == null)
            {
                bool create = EditorUtility.DisplayDialog(
                    "Waste Collection UI Missing",
                    "WasteSelectionMenu was not found in the open scene.\n\nCreate WasteCollectionUI now?",
                    "Create UI",
                    "Cancel");

                if (!create)
                    return;

                selectionMenu = CreateWasteCollectionUiRoot();
                if (selectionMenu == null)
                    return;
            }

            GameObject host = selectionMenu.gameObject;
            RemoveLegacyResultChild(host.transform);

            WasteResultScreenController controller = host.GetComponent<WasteResultScreenController>();
            if (controller == null)
                controller = Undo.AddComponent<WasteResultScreenController>(host);

            WasteCollectionCounterUI counterUi = host.GetComponent<WasteCollectionCounterUI>();
            if (counterUi == null)
                counterUi = Undo.AddComponent<WasteCollectionCounterUI>(host);

            WasteCollectTracker tracker = FindTracker();
            WasteCollectionResultController flowController = host.GetComponent<WasteCollectionResultController>();
            UIDocument sharedDocument = selectionMenu.GetComponent<UIDocument>();
            Transform player = FindPlayerTransform();

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("uiDocument").objectReferenceValue = sharedDocument;
            serializedController.FindProperty("correctStatusIcon").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Project/WasteCollection/UI/IconsPng/circle-check.png");
            serializedController.FindProperty("incorrectStatusIcon").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Project/WasteCollection/UI/IconsPng/circle-x.png");
            serializedController.FindProperty("exitAlertIcon").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Project/WasteCollection/UI/IconsPng/triangle-alert.png");
            serializedController.FindProperty("collectTracker").objectReferenceValue = tracker;
            serializedController.FindProperty("wasteSelectionMenu").objectReferenceValue = selectionMenu;
            if (player != null)
                serializedController.FindProperty("playerRoot").objectReferenceValue = player;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedCounter = new SerializedObject(counterUi);
            serializedCounter.FindProperty("uiDocument").objectReferenceValue = sharedDocument;
            serializedCounter.FindProperty("counterIcon").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Project/WasteCollection/UI/IconsPng/trash-2.png");
            serializedCounter.ApplyModifiedPropertiesWithoutUndo();

            WireFlowController(flowController, tracker, player, selectionMenu);
            EnsureVrComponents(host, controller, flowController);

            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(host.scene);
            Debug.Log("[WasteResultScreenSceneSetup] Result screen uses shared WasteSelectionMenu UIDocument.");
        }

        [MenuItem("Waste Collection/Create Waste Collection UI In Scene")]
        public static void CreateWasteCollectionUiInSceneMenu()
        {
            if (FindSelectionMenu() != null)
            {
                SetupResultScreenInScene();
                return;
            }

            if (CreateWasteCollectionUiRoot() != null)
                SetupResultScreenInScene();
        }

        private static WasteSelectionMenu CreateWasteCollectionUiRoot()
        {
            VisualTreeAsset selectionAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SelectionUxmlPath);
            if (selectionAsset == null)
            {
                Debug.LogError($"[WasteResultScreenSceneSetup] UXML not found at {SelectionUxmlPath}");
                return null;
            }

            var host = new GameObject(HostObjectName);
            Undo.RegisterCreatedObjectUndo(host, "Create WasteCollectionUI");

            UIDocument selectionDocument = Undo.AddComponent<UIDocument>(host);
            selectionDocument.visualTreeAsset = selectionAsset;
            selectionDocument.panelSettings = LoadPanelSettings();

            WasteSelectionMenu selectionMenu = Undo.AddComponent<WasteSelectionMenu>(host);
            Undo.AddComponent<WasteCollectionResultController>(host);
            Undo.AddComponent<WasteCollectionCounterUI>(host);
            EnsureVrComponents(host, null, host.GetComponent<WasteCollectionResultController>());

            SerializedObject serializedMenu = new SerializedObject(selectionMenu);
            serializedMenu.FindProperty("uiDocument").objectReferenceValue = selectionDocument;
            AssignIconLibrary(serializedMenu);
            serializedMenu.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(host.scene);
            return selectionMenu;
        }

        private static void RemoveLegacyResultChild(Transform host)
        {
            Transform legacy = host.Find(LegacyResultObjectName);
            if (legacy == null)
                return;

            Undo.DestroyObjectImmediate(legacy.gameObject);
        }

        private static void AssignIconLibrary(SerializedObject serializedMenu)
        {
            WasteBinIconLibrary library = AssetDatabase.LoadAssetAtPath<WasteBinIconLibrary>(IconLibraryPath);
            if (library != null)
                serializedMenu.FindProperty("iconLibrary").objectReferenceValue = library;
        }

        private static WasteSelectionMenu FindSelectionMenu()
        {
            WasteSelectionMenu[] menus = Resources.FindObjectsOfTypeAll<WasteSelectionMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                WasteSelectionMenu menu = menus[i];
                if (menu == null || EditorUtility.IsPersistent(menu))
                    continue;

                if (!menu.gameObject.scene.IsValid())
                    continue;

                return menu;
            }

            return null;
        }

        private static WasteCollectTracker FindTracker()
        {
            WasteCollectTracker[] trackers = Resources.FindObjectsOfTypeAll<WasteCollectTracker>();
            for (int i = 0; i < trackers.Length; i++)
            {
                WasteCollectTracker tracker = trackers[i];
                if (tracker == null || EditorUtility.IsPersistent(tracker))
                    continue;

                if (!tracker.gameObject.scene.IsValid())
                    continue;

                return tracker;
            }

            return null;
        }

        private static Transform FindPlayerTransform()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform : null;
        }

        private static PanelSettings LoadPanelSettings()
        {
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        }

        private static void WireFlowController(
            WasteCollectionResultController flowController,
            WasteCollectTracker tracker,
            Transform player,
            WasteSelectionMenu selectionMenu)
        {
            if (flowController == null)
                return;

            SerializedObject serializedFlow = new SerializedObject(flowController);
            if (tracker != null)
                serializedFlow.FindProperty("collectTracker").objectReferenceValue = tracker;
            if (player != null)
                serializedFlow.FindProperty("playerRoot").objectReferenceValue = player;
            if (selectionMenu != null)
                serializedFlow.FindProperty("wasteSelectionMenu").objectReferenceValue = selectionMenu;

            SelectionSystemManager selectionSystem = Object.FindFirstObjectByType<SelectionSystemManager>();
            if (selectionSystem != null)
                serializedFlow.FindProperty("selectionSystemManager").objectReferenceValue = selectionSystem;

            serializedFlow.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureWasteCollectionBootstrap()
        {
            GameObject bootstrapObject = null;
            WasteCollectionVrBootstrap existingBootstrap = Object.FindFirstObjectByType<WasteCollectionVrBootstrap>();
            if (existingBootstrap != null)
                bootstrapObject = existingBootstrap.gameObject;

            WasteCollectionPlayerRigController existingRig =
                Object.FindFirstObjectByType<WasteCollectionPlayerRigController>();
            if (bootstrapObject == null && existingRig != null)
                bootstrapObject = existingRig.gameObject;

            if (bootstrapObject == null)
            {
                bootstrapObject = new GameObject("WasteCollectionBootstrap");
                Undo.RegisterCreatedObjectUndo(bootstrapObject, "Create WasteCollectionBootstrap");
            }

            if (bootstrapObject.GetComponent<WasteCollectionVrBootstrap>() == null)
                Undo.AddComponent<WasteCollectionVrBootstrap>(bootstrapObject);

            WasteCollectionPlayerRigController rigController =
                bootstrapObject.GetComponent<WasteCollectionPlayerRigController>();
            if (rigController == null)
                rigController = Undo.AddComponent<WasteCollectionPlayerRigController>(bootstrapObject);

            SerializedObject serializedBootstrap = new SerializedObject(
                bootstrapObject.GetComponent<WasteCollectionVrBootstrap>());
            SelectionSystemManager selectionSystem = Object.FindFirstObjectByType<SelectionSystemManager>();
            if (selectionSystem != null)
            {
                serializedBootstrap.FindProperty("selectionSystemManager").objectReferenceValue =
                    selectionSystem;
            }

            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();
            WirePlayerRigController(rigController);
        }

        private static void WirePlayerRigController(WasteCollectionPlayerRigController rigController)
        {
            if (rigController == null)
                return;

            SerializedObject serializedRig = new SerializedObject(rigController);
            SerializedProperty pcRoots = serializedRig.FindProperty("pcPlayerRoots");
            SerializedProperty xrRoots = serializedRig.FindProperty("xrOriginRoots");

            pcRoots.ClearArray();
            xrRoots.ClearArray();

            System.Type xrOriginType = System.Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");

            int pcIndex = 0;
            int xrIndex = 0;
            GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go == null || !go.scene.IsValid())
                    continue;

                if (go.name == "PC-Player")
                {
                    pcRoots.InsertArrayElementAtIndex(pcIndex++);
                    pcRoots.GetArrayElementAtIndex(pcIndex - 1).objectReferenceValue = go;
                    continue;
                }

                if (xrOriginType != null && go.GetComponent(xrOriginType) != null)
                {
                    xrRoots.InsertArrayElementAtIndex(xrIndex++);
                    xrRoots.GetArrayElementAtIndex(xrIndex - 1).objectReferenceValue = go;
                }
            }

            ScriptableEnumPortingVariable porting = AssetDatabase.LoadAssetAtPath<ScriptableEnumPortingVariable>(
                "Packages/com.woi.module.fire/Runtime/Porting/PortingVariable.asset");
            if (porting != null)
                serializedRig.FindProperty("portingVariable").objectReferenceValue = porting;

            serializedRig.FindProperty("logAppliedState").boolValue = true;
            serializedRig.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureVrComponents(
            GameObject host,
            WasteResultScreenController resultController,
            WasteCollectionResultController flowController)
        {
            if (host.GetComponent<WasteWorldUiPresenter>() == null)
                Undo.AddComponent<WasteWorldUiPresenter>(host);

            if (host.GetComponent<WasteVrWastePicker>() == null)
                Undo.AddComponent<WasteVrWastePicker>(host);

            if (host.GetComponent<WasteVrExitInput>() == null)
                Undo.AddComponent<WasteVrExitInput>(host);

            WasteVrLocomotionGate locomotionGate = host.GetComponent<WasteVrLocomotionGate>();
            if (locomotionGate == null)
                locomotionGate = Undo.AddComponent<WasteVrLocomotionGate>(host);

            EnsureWasteCollectionBootstrap();

            if (resultController != null)
            {
                SerializedObject serializedResult = new SerializedObject(resultController);
                serializedResult.FindProperty("vrLocomotionGate").objectReferenceValue = locomotionGate;
                serializedResult.ApplyModifiedPropertiesWithoutUndo();
            }

            if (flowController != null)
            {
                SerializedObject serializedFlow = new SerializedObject(flowController);
                serializedFlow.FindProperty("vrLocomotionGate").objectReferenceValue = locomotionGate;
                serializedFlow.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
#endif
