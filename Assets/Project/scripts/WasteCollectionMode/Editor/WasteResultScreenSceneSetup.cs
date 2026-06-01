#if UNITY_EDITOR
using Obvious.Soap;
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
        private const string GripInputEventPath =
            "Packages/com.woi.module.fire/Runtime/InputSystem/InputsSO/InputEvents/preOnGameFinishEvent.asset";
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

            EnsureExplanationPopup(host);

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
            WireSelectionSystem(host, controller);
            EnsureVrComponents(host, controller, flowController);

            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(host.scene);
            Debug.Log("[WasteResultScreenSceneSetup] Result screen uses shared WasteSelectionMenu UIDocument.");
        }

        /// <summary>
        /// Non-destructive: only adds/configures the explanation popup component on the existing
        /// WasteCollectionUI and fills the (currently empty) explanationPopup reference on the flow,
        /// VR session, input gate and exit input. Touches nothing else, so existing manual wiring is
        /// preserved. (The runtime also auto-resolves the popup, so this is mostly for clarity.)
        /// </summary>
        [MenuItem("Waste Collection/Add Explanation Popup (Safe)")]
        public static void AddExplanationPopupSafe()
        {
            WasteSelectionMenu selectionMenu = FindSelectionMenu();
            if (selectionMenu == null)
            {
                EditorUtility.DisplayDialog(
                    "Waste Collection",
                    "WasteCollectionUI (WasteSelectionMenu) açık sahnede bulunamadı.",
                    "OK");
                return;
            }

            GameObject host = selectionMenu.gameObject;
            WasteExplanationPopup popup = EnsureExplanationPopup(host);

            SetReferenceIfNull(host.GetComponent<WasteCollectionResultController>(), "explanationPopup", popup);
            SetReferenceIfNull(host.GetComponent<WasteVrUiSessionController>(), "explanationPopup", popup);
            SetReferenceIfNull(host.GetComponent<WasteSelectionInputGate>(), "explanationPopup", popup);
            SetReferenceIfNull(host.GetComponent<WasteVrExitInput>(), "explanationPopup", popup);

            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(host.scene);
            Debug.Log("[WasteResultScreenSceneSetup] Explanation popup added/updated (safe). Sahneyi kaydetmeyi unutma (Ctrl+S).");
        }

        private static void SetReferenceIfNull(Component target, string propertyName, Object value)
        {
            if (target == null)
                return;

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                return;

            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }
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
            selectionDocument.panelSettings = LoadScreenPanelSettings();
            selectionDocument.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Dynamic;

            WasteSelectionMenu selectionMenu = Undo.AddComponent<WasteSelectionMenu>(host);
            Undo.AddComponent<WasteCollectionResultController>(host);
            Undo.AddComponent<WasteCollectionCounterUI>(host);
            EnsureExplanationPopup(host);
            EnsureVrComponents(host, null, host.GetComponent<WasteCollectionResultController>());

            SerializedObject serializedMenu = new SerializedObject(selectionMenu);
            serializedMenu.FindProperty("uiDocument").objectReferenceValue = selectionDocument;
            AssignIconLibrary(serializedMenu);
            serializedMenu.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(host.scene);
            return selectionMenu;
        }

        private static WasteExplanationPopup EnsureExplanationPopup(GameObject host)
        {
            WasteExplanationPopup popup = host.GetComponent<WasteExplanationPopup>();
            if (popup == null)
                popup = Undo.AddComponent<WasteExplanationPopup>(host);

            SerializedObject serializedPopup = new SerializedObject(popup);
            serializedPopup.FindProperty("uiDocument").objectReferenceValue = host.GetComponent<UIDocument>();
            serializedPopup.FindProperty("correctIcon").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Project/WasteCollection/UI/IconsPng/circle-check.png");
            serializedPopup.FindProperty("incorrectIcon").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Project/WasteCollection/UI/IconsPng/circle-x.png");
            serializedPopup.ApplyModifiedPropertiesWithoutUndo();
            return popup;
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

        private static PanelSettings LoadScreenPanelSettings()
        {
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        }

        private static PanelSettings LoadWorldPanelSettings()
        {
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(
                "Assets/Project/OfficeFire/UI/InteractHoverWorldPanelSettings.asset");
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

            serializedFlow.FindProperty("explanationPopup").objectReferenceValue =
                flowController.GetComponent<WasteExplanationPopup>();

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

            WirePlayerRigController(rigController);
            EnsureSelectionVrRay();
        }

        private static void WireSelectionSystem(
            GameObject wasteUiHost,
            WasteResultScreenController resultController)
        {
            SelectionSystemManager selectionSystem = Object.FindFirstObjectByType<SelectionSystemManager>();
            if (selectionSystem == null)
                return;

            if (wasteUiHost.GetComponent<WasteSelectionInputGate>() == null)
                Undo.AddComponent<WasteSelectionInputGate>(wasteUiHost);

            WasteSelectionInputGate gate = wasteUiHost.GetComponent<WasteSelectionInputGate>();
            SerializedObject serializedGate = new SerializedObject(gate);
            serializedGate.FindProperty("selectionMenu").objectReferenceValue =
                wasteUiHost.GetComponent<WasteSelectionMenu>();
            serializedGate.FindProperty("resultScreen").objectReferenceValue = resultController;
            serializedGate.FindProperty("explanationPopup").objectReferenceValue =
                wasteUiHost.GetComponent<WasteExplanationPopup>();
            serializedGate.ApplyModifiedPropertiesWithoutUndo();

            SelectionVrInteractionRay vrRay = Object.FindFirstObjectByType<SelectionVrInteractionRay>(FindObjectsInactive.Include);

            SerializedObject serializedSelection = new SerializedObject(selectionSystem);
            SerializedProperty gates = serializedSelection.FindProperty("selectionGates");
            gates.ClearArray();
            gates.InsertArrayElementAtIndex(0);
            gates.GetArrayElementAtIndex(0).objectReferenceValue = gate;

            if (vrRay != null)
                serializedSelection.FindProperty("vrInteractionRay").objectReferenceValue = vrRay;

            ScriptableEventNoParam interact = AssetDatabase.LoadAssetAtPath<ScriptableEventNoParam>(
                "Packages/com.woi.module.fire/Runtime/InputSystem/InputsSO/InputEvents/onInteractInput.asset");
            if (interact != null)
                serializedSelection.FindProperty("interactInputEvent").objectReferenceValue = interact;

            serializedSelection.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureSelectionVrRay()
        {
            SelectionVrInteractionRay existing =
                Object.FindFirstObjectByType<SelectionVrInteractionRay>(FindObjectsInactive.Include);
            if (existing != null)
                return;

            Transform rightController = SelectionVrInteractionRay.FindRightControllerTransform("Right");
            if (rightController == null)
            {
                Debug.LogWarning(
                    "[WasteResultScreenSceneSetup] Right Controller not found — add SelectionVrInteractionRay manually under XR rig.");
                return;
            }

            SelectionVrInteractionRay raycaster = rightController.GetComponent<SelectionVrInteractionRay>();
            if (raycaster == null)
                raycaster = Undo.AddComponent<SelectionVrInteractionRay>(rightController.gameObject);

            SerializedObject serializedRay = new SerializedObject(raycaster);
            serializedRay.FindProperty("rayOrigin").objectReferenceValue = rightController;
            serializedRay.FindProperty("drawWorldRayLine").boolValue = true;
            serializedRay.ApplyModifiedPropertiesWithoutUndo();
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

        private static Transform FindActiveXrOriginTransform()
        {
            System.Type xrOriginType = System.Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (xrOriginType == null)
                return null;

            System.Array found = Resources.FindObjectsOfTypeAll(xrOriginType);
            Transform fallback = null;
            for (int i = 0; i < found.Length; i++)
            {
                if (found.GetValue(i) is not Component origin || origin == null)
                    continue;

                GameObject go = origin.gameObject;
                if (!go.scene.IsValid())
                    continue;

                if (fallback == null)
                    fallback = origin.transform;

                if (go.activeInHierarchy)
                    return origin.transform;
            }

            return fallback;
        }

        private static void WireVrUiSession(
            GameObject host,
            WasteVrUiSessionController uiSession,
            WasteResultScreenController resultController,
            WasteCollectionResultController flowController)
        {
            if (uiSession == null)
                return;

            SerializedObject serialized = new SerializedObject(uiSession);
            serialized.FindProperty("selectionMenu").objectReferenceValue = host.GetComponent<WasteSelectionMenu>();
            serialized.FindProperty("resultScreen").objectReferenceValue = resultController;
            serialized.FindProperty("explanationPopup").objectReferenceValue = host.GetComponent<WasteExplanationPopup>();
            serialized.FindProperty("worldUiPresenter").objectReferenceValue = host.GetComponent<WasteWorldUiPresenter>();

            WasteVrLocomotionGate locomotionGate = host.GetComponent<WasteVrLocomotionGate>();
            if (locomotionGate == null)
                locomotionGate = host.GetComponentInChildren<WasteVrLocomotionGate>(true);

            serialized.FindProperty("locomotionGate").objectReferenceValue = locomotionGate;
            serialized.FindProperty("selectionSystemManager").objectReferenceValue =
                Object.FindFirstObjectByType<SelectionSystemManager>();

            SelectionVrInteractionRay selectionRay =
                Object.FindFirstObjectByType<SelectionVrInteractionRay>(FindObjectsInactive.Include);
            serialized.FindProperty("selectionRay").objectReferenceValue = selectionRay;
            serialized.FindProperty("uiDistanceInFrontOfHmd").floatValue = 1.35f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureVrComponents(
            GameObject host,
            WasteResultScreenController resultController,
            WasteCollectionResultController flowController)
        {
            WasteWorldUiPresenter presenter = host.GetComponent<WasteWorldUiPresenter>();
            if (presenter == null)
                presenter = Undo.AddComponent<WasteWorldUiPresenter>(host);

            WasteVrLocomotionGate locomotionGateForPresenter = host.GetComponent<WasteVrLocomotionGate>();
            if (locomotionGateForPresenter == null)
                locomotionGateForPresenter = host.GetComponentInChildren<WasteVrLocomotionGate>(true);

            Transform activeXrRig = FindActiveXrOriginTransform();
            if (locomotionGateForPresenter != null && activeXrRig != null)
            {
                SerializedObject serializedLocomotion = new SerializedObject(locomotionGateForPresenter);
                serializedLocomotion.FindProperty("xrRigRoot").objectReferenceValue = activeXrRig;
                serializedLocomotion.ApplyModifiedPropertiesWithoutUndo();
            }

            SerializedObject serializedPresenter = new SerializedObject(presenter);
            PanelSettings worldPanel = LoadWorldPanelSettings();
            if (worldPanel != null)
                serializedPresenter.FindProperty("worldPanelSettingsSource").objectReferenceValue = worldPanel;
            serializedPresenter.FindProperty("worldDocumentScale").floatValue = 0.005f;
            serializedPresenter.FindProperty("cameraOverride").objectReferenceValue = null;
            serializedPresenter.FindProperty("billboardYawOffsetDegrees").floatValue = 0f;
            serializedPresenter.FindProperty("localOffsetFromEye").vector3Value = new Vector3(0f, 0f, 1.35f);
            if (activeXrRig != null)
                serializedPresenter.FindProperty("xrRigRoot").objectReferenceValue = activeXrRig;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
            presenter.ApplyEditorScenePreview();

            WasteVrUiSessionController uiSession = host.GetComponent<WasteVrUiSessionController>();
            if (uiSession == null)
                uiSession = Undo.AddComponent<WasteVrUiSessionController>(host);

            WireVrUiSession(host, uiSession, resultController, flowController);

            WasteVrExitInput exitInput = host.GetComponent<WasteVrExitInput>();
            if (exitInput == null)
                exitInput = Undo.AddComponent<WasteVrExitInput>(host);

            ScriptableEventNoParam gripEvent =
                AssetDatabase.LoadAssetAtPath<ScriptableEventNoParam>(GripInputEventPath);
            {
                SerializedObject serializedExit = new SerializedObject(exitInput);
                if (gripEvent != null)
                    serializedExit.FindProperty("gripInputEvent").objectReferenceValue = gripEvent;
                serializedExit.FindProperty("explanationPopup").objectReferenceValue =
                    host.GetComponent<WasteExplanationPopup>();
                serializedExit.ApplyModifiedPropertiesWithoutUndo();
            }

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
