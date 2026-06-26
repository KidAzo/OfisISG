using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Woi.Equipment;
using Woi.InputSystem;
using Woi.OfficeFire;

namespace Woi.OfficeFire.Editor
{
    /// <summary>
    /// Wires VR blanket grab (grip) on XR controllers and active-rig equipment — mirrors WOI.Shared.Global XR Origin setup.
    /// </summary>
    public static class OfficeFireVrBlanketSetup
    {
        private const string MenuPath = "Tools/Woi/Office Fire/Scene/Wire VR Fire Blanket";

        private const string GameplayInputContextAssetPath =
            "Packages/com.woi.module.fire/Runtime/InputSystem/InputsSO/PC-GameplayContext.asset";

        [MenuItem(MenuPath, false, 26)]
        private static void WireVrFireBlanketActiveScene()
        {
            WireVrFireBlanketInScene(SceneManager.GetActiveScene());
        }

        [MenuItem(MenuPath, true, 26)]
        private static bool WireVrFireBlanketActiveSceneValidate()
        {
            return !Application.isPlaying;
        }

        public static void WireVrFireBlanketInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Office Fire VR Blanket] Scene is not valid or not loaded.");
                return;
            }

            GameObject xrRoot = FindXrOriginRoot();
            if (xrRoot == null)
            {
                Debug.LogWarning(
                    "[Office Fire VR Blanket] No XR Origin found — add XR Origin (XR Rig) to the scene first.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Wire VR Fire Blanket");
            int undoGroup = Undo.GetCurrentGroup();

            FireBlanketUseController primaryUseController =
                FindPrimaryFireBlanketUseController(out PlayerFireBlanketEquipment primaryEquipment);

            PlayerFireBlanketEquipment vrEquipment = EnsureVrBlanketEquipment(xrRoot, primaryEquipment);
            FireBlanketUseController vrUseController =
                EnsureVrBlanketUseController(xrRoot, vrEquipment, primaryUseController);

            Transform leftController = FindControllerTransform(xrRoot, leftHand: true);
            Transform rightController = FindControllerTransform(xrRoot, leftHand: false);

            int grabbers = 0;
            if (leftController != null)
            {
                WireGrabberOnController(leftController, VRHandType.Left, vrEquipment);
                grabbers++;
            }

            if (rightController != null)
            {
                WireGrabberOnController(rightController, VRHandType.Right, vrEquipment);
                grabbers++;
            }

            RebindScenarioBridges(vrEquipment, vrUseController);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                $"[Office Fire VR Blanket] Wired XR rig '{xrRoot.name}': {grabbers} grabber(s), " +
                $"equipment on '{vrEquipment.gameObject.name}', use controller on '{vrUseController.gameObject.name}'.",
                xrRoot);
        }

        private static GameObject FindXrOriginRoot()
        {
            Type originType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (originType == null)
            {
                return null;
            }

            UnityEngine.Object[] found = Resources.FindObjectsOfTypeAll(originType);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] is not Component origin || origin == null)
                {
                    continue;
                }

                GameObject go = origin.gameObject;
                if (!go.scene.IsValid())
                {
                    continue;
                }

                return go;
            }

            return null;
        }

        private static Transform FindControllerTransform(GameObject xrRoot, bool leftHand)
        {
            string[] preferredNames =
            {
                leftHand ? "Left Controller" : "Right Controller",
                leftHand ? "LeftHand Controller" : "RightHand Controller",
            };

            Transform[] transforms = xrRoot.GetComponentsInChildren<Transform>(true);
            for (int n = 0; n < preferredNames.Length; n++)
            {
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i] != null
                        && string.Equals(transforms[i].name, preferredNames[n], StringComparison.Ordinal))
                    {
                        return transforms[i];
                    }
                }
            }

            string contains = leftHand ? "Left" : "Right";
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null)
                {
                    continue;
                }

                if (t.name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0
                    && t.name.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return t;
                }
            }

            return null;
        }

        private static FireBlanketUseController FindPrimaryFireBlanketUseController(
            out PlayerFireBlanketEquipment primaryEquipment)
        {
            primaryEquipment = null;
            FireBlanketUseController[] controllers = UnityEngine.Object.FindObjectsByType<FireBlanketUseController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            FireBlanketUseController best = null;
            for (int i = 0; i < controllers.Length; i++)
            {
                FireBlanketUseController candidate = controllers[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.gameObject.scene.IsValid()
                    && Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils") != null
                    && candidate.GetComponentInParent(
                        Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils")) != null)
                {
                    continue;
                }

                best = candidate;
                break;
            }

            if (best == null && controllers.Length > 0)
            {
                best = controllers[0];
            }

            if (best != null)
            {
                SerializedObject useSo = new SerializedObject(best);
                SerializedProperty equipmentProp = useSo.FindProperty("blanketEquipment");
                if (equipmentProp != null)
                {
                    primaryEquipment = equipmentProp.objectReferenceValue as PlayerFireBlanketEquipment;
                }
            }

            return best;
        }

        private static PlayerFireBlanketEquipment EnsureVrBlanketEquipment(
            GameObject xrRoot,
            PlayerFireBlanketEquipment primaryEquipment)
        {
            PlayerFireBlanketEquipment equipment = xrRoot.GetComponent<PlayerFireBlanketEquipment>();
            if (equipment == null)
            {
                equipment = Undo.AddComponent<PlayerFireBlanketEquipment>(xrRoot);
            }

            SerializedObject equipmentSo = new SerializedObject(equipment);
            SerializedProperty pickupLayerMaskProp = equipmentSo.FindProperty("pickupLayerMask");
            if (pickupLayerMaskProp != null)
            {
                pickupLayerMaskProp.intValue = Physics.AllLayers;
            }

            SerializedProperty inputContextProp = equipmentSo.FindProperty("inputContext");
            if (inputContextProp != null && inputContextProp.objectReferenceValue == null)
            {
                GameplayInputContext context = primaryEquipment != null
                    ? GetEquipmentInputContext(primaryEquipment)
                    : AssetDatabase.LoadAssetAtPath<GameplayInputContext>(GameplayInputContextAssetPath);
                inputContextProp.objectReferenceValue = context;
            }

            equipmentSo.ApplyModifiedPropertiesWithoutUndo();
            return equipment;
        }

        private static GameplayInputContext GetEquipmentInputContext(PlayerFireBlanketEquipment equipment)
        {
            if (equipment == null)
            {
                return null;
            }

            SerializedObject so = new SerializedObject(equipment);
            SerializedProperty prop = so.FindProperty("inputContext");
            return prop?.objectReferenceValue as GameplayInputContext;
        }

        private static FireBlanketUseController EnsureVrBlanketUseController(
            GameObject xrRoot,
            PlayerFireBlanketEquipment vrEquipment,
            FireBlanketUseController primaryUseController)
        {
            FireBlanketUseController useController = xrRoot.GetComponent<FireBlanketUseController>();
            if (useController == null)
            {
                useController = Undo.AddComponent<FireBlanketUseController>(xrRoot);
            }

            Transform distanceReference = FindXrCameraTransform(xrRoot) ?? xrRoot.transform;

            SerializedObject useSo = new SerializedObject(useController);
            SetObjectReference(useSo, "blanketEquipment", vrEquipment);
            SetObjectReference(useSo, "distanceReference", distanceReference);

            if (primaryUseController != null)
            {
                SerializedObject primarySo = new SerializedObject(primaryUseController);
                CopyObjectReference(primarySo, useSo, "fireSource");
                CopyFloat(primarySo, useSo, "fireZoneProbeRadius");
                CopyFloat(primarySo, useSo, "fireZoneRaycastDistance");
                CopyFloat(primarySo, useSo, "extinguishDuration");
                CopyBool(primarySo, useSo, "useCrosshairRayForFireZone");
                CopyInt(primarySo, useSo, "fireZoneLayerMask");
                CopyString(primarySo, useSo, "useInstructionText");
                CopyString(primarySo, useSo, "useInstructionTextTurkish");
                CopySerializedUnityEvent(primarySo, useSo, "onBlanketUsedOnFire");
                CopySerializedUnityEvent(primarySo, useSo, "onBlanketFireExtinguished");
            }

            useSo.ApplyModifiedPropertiesWithoutUndo();
            return useController;
        }

        private static void CopySerializedUnityEvent(SerializedObject source, SerializedObject destination, string propertyName)
        {
            SerializedProperty srcProp = source.FindProperty(propertyName);
            SerializedProperty dstProp = destination.FindProperty(propertyName);
            if (srcProp == null || dstProp == null)
            {
                return;
            }

            SerializedProperty srcCalls = srcProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            SerializedProperty dstCalls = dstProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (srcCalls == null || dstCalls == null)
            {
                return;
            }

            dstCalls.arraySize = srcCalls.arraySize;
            for (int i = 0; i < srcCalls.arraySize; i++)
            {
                SerializedProperty srcCall = srcCalls.GetArrayElementAtIndex(i);
                SerializedProperty dstCall = dstCalls.GetArrayElementAtIndex(i);

                CopySerializedField(srcCall, dstCall, "m_Target");
                CopySerializedField(srcCall, dstCall, "m_MethodName");
                CopySerializedField(srcCall, dstCall, "m_Mode");
                CopySerializedField(srcCall, dstCall, "m_CallState");

                SerializedProperty srcArgs = srcCall.FindPropertyRelative("m_Arguments");
                SerializedProperty dstArgs = dstCall.FindPropertyRelative("m_Arguments");
                if (srcArgs == null || dstArgs == null)
                {
                    continue;
                }

                CopySerializedField(srcArgs, dstArgs, "m_ObjectArgument");
                CopySerializedField(srcArgs, dstArgs, "m_IntArgument");
                CopySerializedField(srcArgs, dstArgs, "m_FloatArgument");
                CopySerializedField(srcArgs, dstArgs, "m_StringArgument");
                CopySerializedField(srcArgs, dstArgs, "m_BoolArgument");
            }
        }

        private static void CopySerializedField(SerializedProperty source, SerializedProperty destination, string relativePath)
        {
            SerializedProperty src = source.FindPropertyRelative(relativePath);
            SerializedProperty dst = destination.FindPropertyRelative(relativePath);
            if (src == null || dst == null)
            {
                return;
            }

            switch (src.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    dst.objectReferenceValue = src.objectReferenceValue;
                    break;
                case SerializedPropertyType.String:
                    dst.stringValue = src.stringValue;
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    dst.intValue = src.intValue;
                    break;
                case SerializedPropertyType.Float:
                    dst.floatValue = src.floatValue;
                    break;
                case SerializedPropertyType.Boolean:
                    dst.boolValue = src.boolValue;
                    break;
            }
        }

        private static Transform FindXrCameraTransform(GameObject xrRoot)
        {
            Camera camera = xrRoot.GetComponentInChildren<Camera>(true);
            return camera != null ? camera.transform : null;
        }

        private static void WireGrabberOnController(
            Transform controller,
            VRHandType handType,
            PlayerFireBlanketEquipment equipment)
        {
            VRHandFireBlanketGrabber grabber = controller.GetComponent<VRHandFireBlanketGrabber>();
            if (grabber == null)
            {
                grabber = Undo.AddComponent<VRHandFireBlanketGrabber>(controller.gameObject);
            }

            SerializedObject grabberSo = new SerializedObject(grabber);
            SerializedProperty handProp = grabberSo.FindProperty("handType");
            if (handProp != null)
            {
                handProp.enumValueIndex = (int)handType;
            }

            SetObjectReference(grabberSo, "holderTransform", controller);
            SetObjectReference(grabberSo, "_trainingEquipmentNotify", equipment);
            grabberSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RebindScenarioBridges(
            PlayerFireBlanketEquipment equipment,
            FireBlanketUseController useController)
        {
            OfficeFireServerBlanketScenarioBridge[] serverBridges =
                UnityEngine.Object.FindObjectsByType<OfficeFireServerBlanketScenarioBridge>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < serverBridges.Length; i++)
            {
                SerializedObject bridgeSo = new SerializedObject(serverBridges[i]);
                SetObjectReference(bridgeSo, "blanketEquipment", equipment);
                SetObjectReference(bridgeSo, "blanketUseController", useController);
                bridgeSo.ApplyModifiedPropertiesWithoutUndo();
            }

            OfficeFireKitchenBlanketScenarioBridge[] kitchenBridges =
                UnityEngine.Object.FindObjectsByType<OfficeFireKitchenBlanketScenarioBridge>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < kitchenBridges.Length; i++)
            {
                SerializedObject bridgeSo = new SerializedObject(kitchenBridges[i]);
                SetObjectReference(bridgeSo, "blanketUseController", useController);
                bridgeSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void CopyObjectReference(SerializedObject source, SerializedObject destination, string propertyName)
        {
            SerializedProperty src = source.FindProperty(propertyName);
            if (src == null)
            {
                return;
            }

            SetObjectReference(destination, propertyName, src.objectReferenceValue);
        }

        private static void CopyFloat(SerializedObject source, SerializedObject destination, string propertyName)
        {
            SerializedProperty src = source.FindProperty(propertyName);
            SerializedProperty dst = destination.FindProperty(propertyName);
            if (src != null && dst != null)
            {
                dst.floatValue = src.floatValue;
            }
        }

        private static void CopyBool(SerializedObject source, SerializedObject destination, string propertyName)
        {
            SerializedProperty src = source.FindProperty(propertyName);
            SerializedProperty dst = destination.FindProperty(propertyName);
            if (src != null && dst != null)
            {
                dst.boolValue = src.boolValue;
            }
        }

        private static void CopyInt(SerializedObject source, SerializedObject destination, string propertyName)
        {
            SerializedProperty src = source.FindProperty(propertyName);
            SerializedProperty dst = destination.FindProperty(propertyName);
            if (src != null && dst != null)
            {
                dst.intValue = src.intValue;
            }
        }

        private static void CopyString(SerializedObject source, SerializedObject destination, string propertyName)
        {
            SerializedProperty src = source.FindProperty(propertyName);
            SerializedProperty dst = destination.FindProperty(propertyName);
            if (src != null && dst != null)
            {
                dst.stringValue = src.stringValue;
            }
        }
    }
}
