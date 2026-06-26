using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Woi.InputSystem;
using Woi.OfficeFire;

namespace Woi.OfficeFire.Editor
{
    /// <summary>
    /// Wires VR carafe grab (right-hand grip) on XR Origin — mirrors PC carafe use (CarafeAndVfx on fire pour).
    /// </summary>
    public static class OfficeFireVrCarafeSetup
    {
        private const string MenuPath = "Tools/Woi/Office Fire/Scene/Wire VR Carafe";

        private const string GameplayInputContextAssetPath =
            "Packages/com.woi.module.fire/Runtime/InputSystem/InputsSO/PC-GameplayContext.asset";

        [MenuItem(MenuPath, false, 27)]
        private static void WireVrCarafeActiveScene()
        {
            WireVrCarafeInScene(SceneManager.GetActiveScene());
        }

        [MenuItem(MenuPath, true, 27)]
        private static bool WireVrCarafeActiveSceneValidate()
        {
            return !Application.isPlaying;
        }

        public static void WireVrCarafeInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Office Fire VR Carafe] Scene is not valid or not loaded.");
                return;
            }

            GameObject xrRoot = FindXrOriginRoot();
            if (xrRoot == null)
            {
                Debug.LogWarning(
                    "[Office Fire VR Carafe] No XR Origin found — add XR Origin (XR Rig) to the scene first.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Wire VR Carafe");
            int undoGroup = Undo.GetCurrentGroup();

            CarafeUseController primaryUseController =
                FindPrimaryCarafeUseController(out PlayerCarafeEquipment primaryEquipment);

            PlayerCarafeEquipment vrEquipment = EnsureVrCarafeEquipment(xrRoot, primaryEquipment);
            CarafeUseController vrUseController =
                EnsureVrCarafeUseController(xrRoot, vrEquipment, primaryUseController);

            Transform rightController = FindControllerTransform(xrRoot, leftHand: false);
            bool grabberWired = false;
            if (rightController != null)
            {
                WireGrabberOnController(rightController, vrEquipment);
                grabberWired = true;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                $"[Office Fire VR Carafe] Wired XR rig '{xrRoot.name}': grabber={(grabberWired ? "right" : "none")}, " +
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

        private static CarafeUseController FindPrimaryCarafeUseController(
            out PlayerCarafeEquipment primaryEquipment)
        {
            primaryEquipment = null;
            CarafeUseController[] controllers = UnityEngine.Object.FindObjectsByType<CarafeUseController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            CarafeUseController best = null;
            Type originType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");

            for (int i = 0; i < controllers.Length; i++)
            {
                CarafeUseController candidate = controllers[i];
                if (candidate == null)
                {
                    continue;
                }

                if (originType != null
                    && candidate.GetComponentInParent(originType) != null)
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
                SerializedProperty equipmentProp = useSo.FindProperty("carafeEquipment");
                if (equipmentProp != null)
                {
                    primaryEquipment = equipmentProp.objectReferenceValue as PlayerCarafeEquipment;
                }
            }

            return best;
        }

        private static PlayerCarafeEquipment EnsureVrCarafeEquipment(
            GameObject xrRoot,
            PlayerCarafeEquipment primaryEquipment)
        {
            PlayerCarafeEquipment equipment = xrRoot.GetComponent<PlayerCarafeEquipment>();
            if (equipment == null)
            {
                equipment = Undo.AddComponent<PlayerCarafeEquipment>(xrRoot);
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

        private static GameplayInputContext GetEquipmentInputContext(PlayerCarafeEquipment equipment)
        {
            if (equipment == null)
            {
                return null;
            }

            SerializedObject so = new SerializedObject(equipment);
            SerializedProperty prop = so.FindProperty("inputContext");
            return prop?.objectReferenceValue as GameplayInputContext;
        }

        private static CarafeUseController EnsureVrCarafeUseController(
            GameObject xrRoot,
            PlayerCarafeEquipment vrEquipment,
            CarafeUseController primaryUseController)
        {
            CarafeUseController useController = xrRoot.GetComponent<CarafeUseController>();
            if (useController == null)
            {
                useController = Undo.AddComponent<CarafeUseController>(xrRoot);
            }

            Transform distanceReference = FindXrCameraTransform(xrRoot) ?? xrRoot.transform;

            SerializedObject useSo = new SerializedObject(useController);
            SetObjectReference(useSo, "carafeEquipment", vrEquipment);
            SetObjectReference(useSo, "distanceReference", distanceReference);

            if (primaryUseController != null)
            {
                SerializedObject primarySo = new SerializedObject(primaryUseController);
                CopyObjectReference(primarySo, useSo, "fireSource");
                CopyFloat(primarySo, useSo, "fireZoneProbeRadius");
                CopyFloat(primarySo, useSo, "fireZoneRaycastDistance");
                CopyBool(primarySo, useSo, "useCrosshairRayForFireZone");
                CopyInt(primarySo, useSo, "fireZoneLayerMask");
                CopyFloat(primarySo, useSo, "fireGrowMultiplier");
                CopyFloat(primarySo, useSo, "growDuration");
                CopyFloat(primarySo, useSo, "vfxResetDelaySeconds");
                CopyString(primarySo, useSo, "useInstructionText");
                CopyString(primarySo, useSo, "useInstructionTextTurkish");
                CopySerializedUnityEvent(primarySo, useSo, "onCarafeUsedOnFire");
                CopySerializedUnityEvent(primarySo, useSo, "onCarafeFireGrown");
                CopySerializedUnityEvent(primarySo, useSo, "onCarafeReset");
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

        private static void WireGrabberOnController(Transform controller, PlayerCarafeEquipment equipment)
        {
            VRHandCarafeGrabber grabber = controller.GetComponent<VRHandCarafeGrabber>();
            if (grabber == null)
            {
                grabber = Undo.AddComponent<VRHandCarafeGrabber>(controller.gameObject);
            }

            SerializedObject grabberSo = new SerializedObject(grabber);
            SetObjectReference(grabberSo, "holderTransform", controller);
            SetObjectReference(grabberSo, "_trainingEquipmentNotify", equipment);
            grabberSo.ApplyModifiedPropertiesWithoutUndo();
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
