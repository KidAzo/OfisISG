using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Woi.InputSystem;
using WOI.Modules.SDK;

namespace Woi.HazardSystem
{
    /// <summary>
    /// VR confirm for Hazard Hunt. Uses <see cref="PlayerInputActions.XR.Interact"/>,
    /// which is bound to the right controller trigger. Does not read mouse or keyboard.
    /// </summary>
    public sealed class VrTriggerInteractionInput : InteractionInputBase
    {
        public const string ActionMapName = "XR";
        public const string ActionName = "Interact";
        public const string BindingPath = "<XRController>{RightHand}/{TriggerButton}";

        public override event Action OnInteractPressed;

        PlayerInputActions _actions;
        bool _bound;
        bool _logged;

        void OnEnable()
        {
            if (!_bound)
                StartCoroutine(BindWhenReady());
        }

        void OnDisable()
        {
            Unbind();
        }

        IEnumerator BindWhenReady()
        {
            const int maxFrames = 300;
            for (int frame = 0; frame < maxFrames && !_bound; frame++)
            {
                if (TryBind())
                    yield break;

                yield return null;
            }

            if (!_bound)
            {
                Debug.LogWarning(
                    "[HazardVrInput] " + ActionMapName + "/" + ActionName +
                    " was not available. Right-trigger confirm will not fire.",
                    this);
            }
        }

        bool TryBind()
        {
            if (_bound)
                return true;

            InputManager inputManager = ResolveInputManager();
            if (inputManager == null)
                return false;

            inputManager.EnsureVrGameplayInputEnabled();

            PlayerInputActions actions = inputManager.InputActions;
            if (actions == null)
                return false;

            actions.Enable();
            if (!actions.XR.enabled)
                actions.XR.Enable();

            _actions = actions;
            _actions.XR.Interact.performed += OnTriggerPerformed;
            _bound = true;

            if (!_logged)
            {
                _logged = true;
                Debug.Log(
                    "[HazardVrInput] confirm=" + ActionMapName + "/" + ActionName +
                    " binding=" + BindingPath,
                    this);
            }

            return true;
        }

        void Unbind()
        {
            if (_actions != null)
                _actions.XR.Interact.performed -= OnTriggerPerformed;

            _actions = null;
            _bound = false;
        }

        void OnTriggerPerformed(InputAction.CallbackContext context)
        {
            if (!context.performed)
                return;

            OnInteractPressed?.Invoke();
        }

        static InputManager ResolveInputManager()
        {
            if (ServiceLocator.TryGet(out InputManager registered) && registered != null)
                return registered;

            return FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);
        }
    }
}
