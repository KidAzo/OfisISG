using FireExtinguisher.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Woi.Equipment;
using Woi.InputSystem;
using Woi.OfficeFire;

namespace Woi.OfficeFire.Editor
{
    public static class OfficeFireServerBlanketSetup
    {
        private const string GameplayInputContextAssetPath =
            "Packages/com.woi.module.fire/Runtime/InputSystem/InputsSO/PC-GameplayContext.asset";

        private const string MenuPath = "Tools/Woi/Office Fire/Scene/Wire Server Fire Blanket";

        [MenuItem(MenuPath, false, 25)]
        private static void WireServerFireBlanketActiveScene()
        {
            WireServerFireBlanketInScene(SceneManager.GetActiveScene());
        }

        [MenuItem(MenuPath, true, 25)]
        private static bool WireServerFireBlanketActiveSceneValidate()
        {
            return !Application.isPlaying;
        }

        public static int WireBlanketHoverOutlinesUnder(Transform root)
        {
            if (root == null)
            {
                return 0;
            }

            FireBlanketPickupItem[] items = root.GetComponentsInChildren<FireBlanketPickupItem>(true);
            int wired = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (TryWireItem(items[i]))
                {
                    wired++;
                }
            }

            return wired;
        }

        public static void WireServerFireBlanketInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Office Fire Scene] Scene is not valid or not loaded: " + scene.path);
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Wire Server Fire Blanket");
            int undoGroup = Undo.GetCurrentGroup();

            ServerRoomScenarioController controller =
                Object.FindFirstObjectByType<ServerRoomScenarioController>(FindObjectsInactive.Include);

            FireBlanketPickupItem[] items = Object.FindObjectsByType<FireBlanketPickupItem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int hoverWired = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (TryWireItem(items[i]))
                {
                    hoverWired++;
                }
            }

            WirePlayerBlanketEquipment(controller);
            WireScenarioBridge(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                $"[Office Fire Scene] Server fire blanket wiring complete ({scene.path}). Hover wired on {hoverWired} blanket(s).");
        }

        public static bool TryWireItem(FireBlanketPickupItem item)
        {
            if (item == null)
            {
                return false;
            }

            GameObject root = item.gameObject;

            RemoveLegacyHoverComponents(root);

            Outline outline = root.GetComponent<Outline>();
            if (outline == null)
            {
                outline = Undo.AddComponent<Outline>(root);
            }

            Undo.RecordObject(outline, "Configure fire blanket outline");
            outline.OutlineColor = new Color(1f, 0.92f, 0f, 1f);
            outline.OutlineWidth = 2f;
            outline.enabled = false;

            SerializedObject pickupSo = new SerializedObject(item);
            SerializedProperty pickupOutlineProp = pickupSo.FindProperty("outline");
            SerializedProperty pickupUseWidthProp = pickupSo.FindProperty("useOutlineWidth");
            SerializedProperty pickupWidthProp = pickupSo.FindProperty("hoverOutlineWidth");
            if (pickupOutlineProp != null)
            {
                pickupOutlineProp.objectReferenceValue = outline;
            }

            if (pickupUseWidthProp != null)
            {
                pickupUseWidthProp.boolValue = true;
            }

            if (pickupWidthProp != null)
            {
                pickupWidthProp.floatValue = 5f;
            }

            pickupSo.ApplyModifiedPropertiesWithoutUndo();

            WireDropAnchor(item);
            EnsurePickupCollider(root);

            if (root.GetComponent<FireBlanketPickupItem>() == null)
            {
                Undo.AddComponent<FireBlanketPickupItem>(root);
            }

            return true;
        }

        private static void RemoveLegacyHoverComponents(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                if (typeName == "HoverableOutline" || typeName == "HoverOutline" || typeName == "SelectableInstructionPrompt")
                {
                    Undo.DestroyObjectImmediate(behaviour);
                }
            }
        }

        private static void EnsurePickupCollider(GameObject root)
        {
            BoxCollider box = root.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(root);
            }

            Undo.RecordObject(box, "Configure fire blanket collider");
            box.isTrigger = false;
            box.enabled = true;

            if (TryFitBoxColliderToRenderers(root.transform, box))
            {
                return;
            }

            box.center = Vector3.zero;
            box.size = new Vector3(0.5f, 0.08f, 0.5f);
        }

        private static bool TryFitBoxColliderToRenderers(Transform root, BoxCollider box)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return false;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            Vector3 localCenter = root.InverseTransformPoint(bounds.center);
            Vector3 localSize = root.InverseTransformVector(bounds.size);
            localSize.x = Mathf.Abs(localSize.x);
            localSize.y = Mathf.Abs(localSize.y);
            localSize.z = Mathf.Abs(localSize.z);

            if (localSize.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            box.center = localCenter;
            box.size = localSize;
            return true;
        }

        public static void WirePlayerBlanketEquipment(ServerRoomScenarioController controller)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[Office Fire Scene] Player tag not found — wire PlayerFireBlanketEquipment manually.");
                return;
            }

            PlayerFireBlanketEquipment equipment = player.GetComponent<PlayerFireBlanketEquipment>();
            if (equipment == null)
            {
                equipment = Undo.AddComponent<PlayerFireBlanketEquipment>(player);
            }

            FireBlanketUseController useController = player.GetComponent<FireBlanketUseController>();
            if (useController == null)
            {
                useController = Undo.AddComponent<FireBlanketUseController>(player);
            }

            PlayerExtinguisherEquipment extinguisherEquipment =
                player.GetComponent<PlayerExtinguisherEquipment>();

            FireSource fireSource = controller != null && controller.ScenarioRoot != null
                ? controller.ScenarioRoot.GetComponentInChildren<FireSource>(true)
                : null;
            if (fireSource == null)
            {
                fireSource = Object.FindFirstObjectByType<FireSource>(FindObjectsInactive.Include);
            }

            SerializedObject equipmentSo = new SerializedObject(equipment);
            SerializedProperty equipAnchorProp = equipmentSo.FindProperty("equipAnchor");
            SerializedProperty inputContextProp = equipmentSo.FindProperty("inputContext");
            SerializedProperty playerCameraProp = equipmentSo.FindProperty("playerCamera");
            SerializedProperty pickupLayerMaskProp = equipmentSo.FindProperty("pickupLayerMask");
            if (equipAnchorProp != null && extinguisherEquipment != null)
            {
                equipAnchorProp.objectReferenceValue = extinguisherEquipment.EquipAnchor;
            }

            if (inputContextProp != null)
            {
                GameplayInputContext inputContext = extinguisherEquipment != null
                    ? extinguisherEquipment.InputContext
                    : null;
                if (inputContext == null)
                {
                    inputContext = AssetDatabase.LoadAssetAtPath<GameplayInputContext>(GameplayInputContextAssetPath);
                }

                inputContextProp.objectReferenceValue = inputContext;
            }

            if (playerCameraProp != null && extinguisherEquipment != null && extinguisherEquipment.PlayerCamera != null)
            {
                playerCameraProp.objectReferenceValue = extinguisherEquipment.PlayerCamera;
            }

            if (pickupLayerMaskProp != null)
            {
                pickupLayerMaskProp.intValue = Physics.AllLayers;
            }

            equipmentSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject useSo = new SerializedObject(useController);
            SerializedProperty equipmentProp = useSo.FindProperty("blanketEquipment");
            SerializedProperty fireSourceProp = useSo.FindProperty("fireSource");
            if (equipmentProp != null)
            {
                equipmentProp.objectReferenceValue = equipment;
            }

            if (fireSourceProp != null)
            {
                fireSourceProp.objectReferenceValue = fireSource;
            }

            useSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireDropAnchor(FireBlanketPickupItem item)
        {
            SerializedObject pickupSo = new SerializedObject(item);
            SerializedProperty dropAnchorProp = pickupSo.FindProperty("dropAnchor");
            if (dropAnchorProp == null || dropAnchorProp.objectReferenceValue != null)
            {
                return;
            }

            Transform blanketTransform = item.transform;
            Transform searchRoot = blanketTransform.root;
            Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate == blanketTransform)
                {
                    continue;
                }

                if (candidate.name == "DropAnchor" || candidate.name == "FireBlanket")
                {
                    dropAnchorProp.objectReferenceValue = candidate;
                    pickupSo.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }
        }

        public static void WireScenarioBridge(ServerRoomScenarioController controller)
        {
            if (controller == null)
            {
                return;
            }

            OfficeFireServerBlanketScenarioBridge bridge =
                controller.GetComponent<OfficeFireServerBlanketScenarioBridge>();
            if (bridge == null)
            {
                bridge = Undo.AddComponent<OfficeFireServerBlanketScenarioBridge>(controller.gameObject);
            }

            PlayerFireBlanketEquipment equipment =
                Object.FindFirstObjectByType<PlayerFireBlanketEquipment>(FindObjectsInactive.Include);
            FireBlanketUseController useController =
                Object.FindFirstObjectByType<FireBlanketUseController>(FindObjectsInactive.Include);

            SerializedObject bridgeSo = new SerializedObject(bridge);
            SerializedProperty scenarioProp = bridgeSo.FindProperty("scenario");
            SerializedProperty equipmentProp = bridgeSo.FindProperty("blanketEquipment");
            SerializedProperty useProp = bridgeSo.FindProperty("blanketUseController");
            if (scenarioProp != null)
            {
                scenarioProp.objectReferenceValue = controller;
            }

            if (equipmentProp != null)
            {
                equipmentProp.objectReferenceValue = equipment;
            }

            if (useProp != null)
            {
                useProp.objectReferenceValue = useController;
            }

            bridgeSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
