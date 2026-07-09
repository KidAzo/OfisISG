using System.Collections.Generic;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Woi.Events.Data;
using Woi.InputSystem;

namespace FireExtinguisher.PC
{
    /// <summary>
    /// PC/VR spray input via live <see cref="GameplayInputContext"/> (Gameplay/Fire).
    /// VR grabbers can set <see cref="OverrideVrHandNode"/> to read the controller trigger directly.
    /// </summary>
    [AddComponentMenu("Fire Extinguisher/PC/PC Spray Input Provider")]
    public sealed class PCSprayInputProvider : MonoBehaviour, ISprayInputProvider, ISoapGameplayInputContextListener
    {
        [SerializeField]
        private GameplayInputContext inputContext;

        private int _lastFrameCount = -1;
        private bool _prevSprayHeld;
        private bool _currentSprayHeld;

        static InputAction _leftTriggerAction;
        static InputAction _rightTriggerAction;

        public XRNode? OverrideVrHandNode { get; set; }

        private void OnEnable()
        {
            TryBindLiveInputContext();
        }

        private void Start()
        {
            TryBindLiveInputContext();
        }

        private void OnDisable()
        {
            OverrideVrHandNode = null;
        }

        public bool IsUsingDifferentGameplayInputContext(GameplayInputContext liveContext) =>
            inputContext != null
            && liveContext != null
            && !ReferenceEquals(inputContext, liveContext);

        public void RebindGameplayInputContext(GameplayInputContext liveContext)
        {
            if (liveContext != null)
                inputContext = liveContext;
        }

        private void TryBindLiveInputContext()
        {
            InputManager inputManager = FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);
            GameplayInputContext liveContext = inputManager?.GetPcGameplayContext();
            if (liveContext == null)
                return;

            if (inputContext == null || IsUsingDifferentGameplayInputContext(liveContext))
                RebindGameplayInputContext(liveContext);
        }

        private IFireInputReader ResolveFireInputReader()
        {
            if (inputContext != null && inputContext.HasInitializedInputActions)
                return inputContext;

            InputManager inputManager = FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);
            GameplayInputContext liveContext = inputManager?.GetPcGameplayContext();
            if (liveContext != null)
                return liveContext;

            return inputContext;
        }

        private void EnsureUpdated()
        {
            if (Time.frameCount == _lastFrameCount)
                return;

            _prevSprayHeld = _currentSprayHeld;

            if (OverrideVrHandNode.HasValue)
            {
                _currentSprayHeld = GetVrTrigger(OverrideVrHandNode.Value);
            }
            else
            {
                if (TrainingGameplayBlockState.IsBlocked)
                    _currentSprayHeld = false;
                else
                    _currentSprayHeld = ResolveFireInputReader()?.IsFireHolding ?? false;
            }

            _lastFrameCount = Time.frameCount;
        }

        private static bool GetVrTrigger(XRNode node)
        {
            if (TryGetVrTriggerFromInputSystem(node, out bool pressed))
                return pressed;

            return GetVrTriggerFromLegacyDevices(node);
        }

        static bool TryGetVrTriggerFromInputSystem(XRNode node, out bool pressed)
        {
            pressed = false;
            InputAction action = GetOrCreateVrTriggerAction(node);
            if (action == null)
                return false;

            if (!action.enabled)
                action.Enable();

            float axis = action.ReadValue<float>();
            if (axis > 0.45f)
            {
                pressed = true;
                return true;
            }

            pressed = action.IsPressed();
            return true;
        }

        static InputAction GetOrCreateVrTriggerAction(XRNode node)
        {
            ref InputAction action = ref (node == XRNode.LeftHand ? ref _leftTriggerAction : ref _rightTriggerAction);
            if (action != null)
                return action;

            string hand = node == XRNode.LeftHand ? "LeftHand" : "RightHand";
            action = new InputAction(
                $"VrExtinguisherSprayTrigger_{hand}",
                InputActionType.Value,
                $"<XRController>{{{hand}}}/{{trigger}}");
            action.Enable();
            return action;
        }

        private static bool GetVrTriggerFromLegacyDevices(XRNode node)
        {
            var devices = new List<UnityEngine.XR.InputDevice>();
            InputDevices.GetDevicesAtXRNode(node, devices);
            if (devices.Count == 0)
                return false;

            UnityEngine.XR.InputDevice device = devices[0];
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float triggerVal))
                return triggerVal > 0.45f;
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool triggerBool))
                return triggerBool;

            return false;
        }

        public bool IsSprayHeld
        {
            get
            {
                EnsureUpdated();
                return _currentSprayHeld;
            }
        }

        public bool IsSprayStartedThisFrame
        {
            get
            {
                EnsureUpdated();
                return _currentSprayHeld && !_prevSprayHeld;
            }
        }

        public bool IsSprayStoppedThisFrame
        {
            get
            {
                EnsureUpdated();
                return !_currentSprayHeld && _prevSprayHeld;
            }
        }
    }
}
