using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using Woi.Equipment;
using Woi.UI.Announcements;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Mirrors WOI.Shared.Global XR Origin extinguisher wiring (grabbers, pin pullers, equipment, hover ray).
    /// </summary>
    public static class OfficeFireVrExtinguisherRigWiring
    {
        const string PlayerInputActionsPath =
            "Packages/com.woi.module.fire/Runtime/InputSystem/PlayerInputActions.inputactions";

        const string AnchorName = "AnchorEst";
        const int DetectionLayerMask = 2039;

        static readonly Vector3 HolderLocalOffset = new(0f, -0.5f, 0f);
        static readonly Vector3 PinPullerNozzleEuler = new(0f, 90f, -90f);
        const float NozzleSnapProximityRadius = 0.45f;

        static FieldInfo _isLeftHandField;
        static FieldInfo _nozzleSnapHandAnchorField;
        static FieldInfo _nozzleSnapProximityRadiusField;
        static FieldInfo[] _equipmentCopyFields;

        public static bool EnsureWired(bool logResult = true, bool ignoreVrModeCheck = false)
        {
            if (!ignoreVrModeCheck && !FirePlatformRuntime.IsVR)
                return false;

            EnsurePlayerTriggerCompatibility(ignoreVrModeCheck);

            GameObject xrRoot = FindXrOriginRoot();
            if (xrRoot == null)
            {
                if (logResult)
                {
                    Debug.LogWarning("[OfficeFireVrExtinguisherRigWiring] XR Origin not found.");
                }

                return false;
            }

            PlayerExtinguisherEquipment primaryEquipment = FindPrimaryPcExtinguisherEquipment();
            Transform leftController = FindControllerTransform(xrRoot, leftHand: true);
            Transform rightController = FindControllerTransform(xrRoot, leftHand: false);
            Transform xrCamera = FindXrCameraTransform(xrRoot);

            PlayerExtinguisherEquipment vrEquipment =
                EnsureVrExtinguisherEquipment(xrRoot, primaryEquipment, leftController, rightController, xrCamera);

            int grabbers = 0;
            if (leftController != null)
            {
                WireControllerHand(leftController, VRHandType.Left, vrEquipment, primaryEquipment);
                grabbers++;
            }

            if (rightController != null)
            {
                WireControllerHand(rightController, VRHandType.Right, vrEquipment, primaryEquipment);
                EnsureHoverRaycaster(rightController);
                grabbers++;
            }

            RebindScenarioBridges(vrEquipment);
            EnsureJumpSuppressedOnRoot(xrRoot);

            if (logResult)
            {
                Debug.Log(
                    $"[OfficeFireVrExtinguisherRigWiring] XR rig '{xrRoot.name}': grabbers={grabbers}, " +
                    $"equipment={(vrEquipment != null ? vrEquipment.gameObject.name : "null")}.",
                    xrRoot);
            }

            return grabbers > 0;
        }

        static void WireControllerHand(
            Transform controller,
            VRHandType handType,
            PlayerExtinguisherEquipment vrEquipment,
            PlayerExtinguisherEquipment primaryEquipment)
        {
            if (controller == null)
                return;

            Transform holder = EnsureHolderAnchor(controller);
            bool leftHand = handType == VRHandType.Left;

            InputActionReference grabAction = LoadInputActionReference(
                leftHand ? "LeftControllerGrab" : "RightControllerGrab");
            InputActionReference pullAction = LoadInputActionReference(
                leftHand ? "LeftControllerPinPulling" : "RightControllerPinPulling");

            VRHandExtinguisherGrabber grabber = controller.GetComponent<VRHandExtinguisherGrabber>();
            if (grabber == null)
                grabber = controller.gameObject.AddComponent<VRHandExtinguisherGrabber>();

            grabber.ApplyHandConfiguration(
                handType,
                holder,
                grabAction,
                vrEquipment != null ? vrEquipment : primaryEquipment);

            SerializedGrabberDefaults(grabber);

            VRExtinguisherPinPuller pinPuller = controller.GetComponent<VRExtinguisherPinPuller>();
            if (pinPuller == null)
                pinPuller = controller.gameObject.AddComponent<VRExtinguisherPinPuller>();

            ConfigurePinPuller(pinPuller, grabber, holder, leftHand, pullAction);
        }

        static void SerializedGrabberDefaults(VRHandExtinguisherGrabber grabber)
        {
            if (grabber == null)
                return;

            grabber.localPositionOffset = HolderLocalOffset;
            grabber.grabRadius = 0.25f;
            grabber.detectionLayerMask = DetectionLayerMask;
        }

        static void ConfigurePinPuller(
            VRExtinguisherPinPuller pinPuller,
            VRHandExtinguisherGrabber grabber,
            Transform nozzleSnapAnchor,
            bool leftHand,
            InputActionReference pullAction)
        {
            if (pinPuller == null)
                return;

            bool wasEnabled = pinPuller.enabled;
            if (wasEnabled)
                pinPuller.enabled = false;

            pinPuller.myGrabber = grabber;
            pinPuller.pullInput = pullAction;
            pinPuller.pullRadius = 0.15f;
            pinPuller.detectionLayerMask = DetectionLayerMask;
            pinPuller.pinTag = "Pin";
            pinPuller.vrSprayDetectionTransformName = "Nozzle_low";
            pinPuller.nozzleLocalEulerRotationOffset = PinPullerNozzleEuler;

            _isLeftHandField ??= typeof(VRExtinguisherPinPuller).GetField(
                "_isLeftHand",
                BindingFlags.Instance | BindingFlags.NonPublic);
            _isLeftHandField?.SetValue(pinPuller, leftHand);

            _nozzleSnapHandAnchorField ??= typeof(VRExtinguisherPinPuller).GetField(
                "_nozzleSnapHandAnchor",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (nozzleSnapAnchor != null)
                _nozzleSnapHandAnchorField?.SetValue(pinPuller, nozzleSnapAnchor);

            _nozzleSnapProximityRadiusField ??= typeof(VRExtinguisherPinPuller).GetField(
                "_nozzleSnapProximityRadius",
                BindingFlags.Instance | BindingFlags.NonPublic);
            _nozzleSnapProximityRadiusField?.SetValue(pinPuller, NozzleSnapProximityRadius);

            if (wasEnabled)
                pinPuller.enabled = true;
        }

        public static void EnsureJumpSuppressed()
        {
            if (!FirePlatformRuntime.IsVR)
                return;

            GameObject[] roots = FindAllLoadedXrOriginRoots();
            for (int i = 0; i < roots.Length; i++)
                EnsureJumpSuppressedOnRoot(roots[i]);
        }

        /// <summary>
        /// ScenarioTriggerVolume and PC systems expect colliders on the Player layer (6) + Player tag.
        /// Stock XRI 3.5 XR Origin uses layer 2 — mirrors WOI.Shared.Global customized XR Rig.
        /// </summary>
        public static void EnsurePlayerTriggerCompatibility(bool ignoreVrModeCheck = false)
        {
            if (!ignoreVrModeCheck && !FirePlatformRuntime.IsVR)
                return;

            GameObject[] roots = FindAllLoadedXrOriginRoots();
            for (int i = 0; i < roots.Length; i++)
            {
                ApplyPlayerLayerAndTag(roots[i]);
                EnsurePlayerTeleportWatcher(roots[i]);
            }

            try
            {
                GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
                for (int i = 0; i < taggedPlayers.Length; i++)
                    EnsurePlayerTeleportWatcher(taggedPlayers[i]);
            }
            catch (UnityException)
            {
                // Player tag may be undefined in some scenes.
            }
        }

        static void EnsurePlayerTeleportWatcher(GameObject xrRoot)
        {
            if (xrRoot == null)
                return;

            if (xrRoot.GetComponent<OfficeFirePlayerTeleportWatcher>() == null)
                xrRoot.AddComponent<OfficeFirePlayerTeleportWatcher>();
        }

        static void ApplyPlayerLayerAndTag(GameObject xrRoot)
        {
            if (xrRoot == null)
                return;

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer < 0)
                playerLayer = 6;

            Transform[] transforms = xrRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t != null)
                    t.gameObject.layer = playerLayer;
            }

            try
            {
                xrRoot.tag = "Player";
            }
            catch (UnityException ex)
            {
                Debug.LogWarning(
                    $"[OfficeFireVrExtinguisherRigWiring] Could not assign Player tag on '{xrRoot.name}': {ex.Message}",
                    xrRoot);
            }
        }

        static void EnsureJumpSuppressedOnRoot(GameObject xrRoot)
        {
            if (xrRoot == null)
                return;

            OfficeFireVrJumpSuppressor suppressor = xrRoot.GetComponent<OfficeFireVrJumpSuppressor>();
            if (suppressor == null)
                suppressor = xrRoot.AddComponent<OfficeFireVrJumpSuppressor>();

            suppressor.SuppressJumpProviders();
        }

        static Transform EnsureHolderAnchor(Transform controller)
        {
            Transform existing = controller.Find(AnchorName);
            if (existing != null)
                return existing;

            var anchor = new GameObject(AnchorName).transform;
            anchor.SetParent(controller, false);
            anchor.localPosition = Vector3.zero;
            anchor.localRotation = Quaternion.identity;
            anchor.localScale = Vector3.one;
            return anchor;
        }

        static PlayerExtinguisherEquipment EnsureVrExtinguisherEquipment(
            GameObject xrRoot,
            PlayerExtinguisherEquipment primary,
            Transform leftController,
            Transform rightController,
            Transform xrCamera)
        {
            PlayerExtinguisherEquipment equipment = xrRoot.GetComponent<PlayerExtinguisherEquipment>();
            if (equipment == null)
                equipment = xrRoot.AddComponent<PlayerExtinguisherEquipment>();

            CopyEquipmentFields(primary, equipment);

            SetPrivateField(equipment, "_equipAnchor", leftController);
            SetPrivateField(equipment, "_interactionRayOrigin", rightController);
            if (xrCamera != null)
            {
                Camera camera = xrCamera.GetComponent<Camera>();
                if (camera != null)
                    SetPrivateField(equipment, "_playerCamera", camera);
            }

            SetPrivateField(equipment, "_pickupLayerMask", (LayerMask)(1 << 7));

            return equipment;
        }

        static void CopyEquipmentFields(PlayerExtinguisherEquipment source, PlayerExtinguisherEquipment destination)
        {
            if (source == null || destination == null)
                return;

            _equipmentCopyFields ??= new[]
            {
                GetField(typeof(PlayerExtinguisherEquipment), "_inputContext"),
                GetField(typeof(PlayerExtinguisherEquipment), "_extinguisherChangedEvent"),
                GetField(typeof(PlayerExtinguisherEquipment), "_onEquipEvent"),
                GetField(typeof(PlayerExtinguisherEquipment), "_onDropEvent"),
                GetField(typeof(PlayerExtinguisherEquipment), "_onPinPullSucceeded"),
                GetField(typeof(PlayerExtinguisherEquipment), "_pickupRange"),
                GetField(typeof(PlayerExtinguisherEquipment), "_allowSwap"),
                GetField(typeof(PlayerExtinguisherEquipment), "_slotController"),
            };

            for (int i = 0; i < _equipmentCopyFields.Length; i++)
            {
                FieldInfo field = _equipmentCopyFields[i];
                if (field == null)
                    continue;

                object value = field.GetValue(source);
                field.SetValue(destination, value);
            }
        }

        static void EnsureHoverRaycaster(Transform rightController)
        {
            if (rightController == null)
                return;

            ExtinguisherHoverTransformRaycaster raycaster =
                rightController.GetComponent<ExtinguisherHoverTransformRaycaster>();
            if (raycaster == null)
                raycaster = rightController.gameObject.AddComponent<ExtinguisherHoverTransformRaycaster>();

            SetPrivateField(raycaster, "rayOrigin", rightController);
            SetPrivateField(raycaster, "rayStartInsetMeters", 0.08f);
            SetPrivateField(raycaster, "maxDistance", 12f);
            SetPrivateField(raycaster, "layerMask", (LayerMask)Physics.DefaultRaycastLayers);
            SetPrivateField(raycaster, "drawWorldRayLine", true);
        }

        static void RebindScenarioBridges(PlayerExtinguisherEquipment vrEquipment)
        {
            if (vrEquipment == null)
                return;

            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                Type type = behaviour.GetType();
                if (type.Namespace != "Woi.OfficeFire" || !type.Name.Contains("ExtinguishBridge"))
                    continue;

                SetPrivateField(behaviour, "xrExtinguisherEquipment", vrEquipment);
            }
        }

        static InputActionReference LoadInputActionReference(string actionName)
        {
#if UNITY_EDITOR
            UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(PlayerInputActionsPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is InputActionReference reference
                    && string.Equals(reference.name, actionName, StringComparison.Ordinal))
                {
                    return reference;
                }
            }
#endif
            return null;
        }

        static PlayerExtinguisherEquipment FindPrimaryPcExtinguisherEquipment()
        {
            Type originType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            PlayerExtinguisherEquipment[] found = UnityEngine.Object.FindObjectsByType<PlayerExtinguisherEquipment>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < found.Length; i++)
            {
                PlayerExtinguisherEquipment candidate = found[i];
                if (candidate == null)
                    continue;

                if (originType != null && candidate.GetComponentInParent(originType) != null)
                    continue;

                return candidate;
            }

            return found.Length > 0 ? found[0] : null;
        }

        static GameObject FindXrOriginRoot()
        {
            GameObject[] roots = FindAllLoadedXrOriginRoots();
            if (roots.Length == 0)
                return null;

            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].activeInHierarchy)
                    return roots[i];
            }

            return roots[0];
        }

        static GameObject[] FindAllLoadedXrOriginRoots()
        {
            Type originType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (originType == null)
                return Array.Empty<GameObject>();

            UnityEngine.Object[] found = Resources.FindObjectsOfTypeAll(originType);
            var loaded = new List<GameObject>(found.Length);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] is not Component origin || origin == null)
                    continue;

                GameObject go = origin.gameObject;
                if (!go.scene.IsValid() || !go.scene.isLoaded)
                    continue;

                loaded.Add(go);
            }

            return loaded.ToArray();
        }

        static Transform FindControllerTransform(GameObject xrRoot, bool leftHand)
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
                    continue;

                if (t.name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0
                    && t.name.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return t;
                }
            }

            return null;
        }

        static Transform FindXrCameraTransform(GameObject xrRoot)
        {
            Camera camera = xrRoot.GetComponentInChildren<Camera>(true);
            return camera != null ? camera.transform : null;
        }

        static FieldInfo GetField(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
                return;

            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }
    }
}
