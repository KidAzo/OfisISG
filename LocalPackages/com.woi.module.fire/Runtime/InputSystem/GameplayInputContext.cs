using UnityEngine;
using UnityEngine.InputSystem;
using Obvious.Soap;

namespace Woi.InputSystem
{
        [CreateAssetMenu(menuName = "Input System/Contexts/Gameplay Context")]
        public class GameplayInputContext : InputContext, IFireInputReader
        {
            [Header("Movement Events")]
            [SerializeField] private ScriptableEventVector2 onMoveInput;
            [SerializeField] private ScriptableEventVector2 onLookInput;
            
            [Header("Action Events")]
            [SerializeField] private ScriptableEventBool onJumpInput;
            [SerializeField] private ScriptableEventBool onFireInput;
            [SerializeField] private ScriptableEventBool onSprintInput;
            [SerializeField] private ScriptableEventFloat onLeanInput;
            [SerializeField] private ScriptableEventNoParam onInteractInput;
            [SerializeField] private ScriptableEventNoParam onGameplayFinishedInput;
            [SerializeField] private ScriptableEventNoParam preOnGameplayFinishedInput;
            [SerializeField] private ScriptableEventNoParam onEquipInput;
            [SerializeField] private ScriptableEventNoParam onDropInput;
            [SerializeField] private ScriptableEventNoParam onPinPullingInput;
            [SerializeField] private ScriptableEnumPortingVariable portingVariable;

            public ScriptableEventVector2 MoveInputEvent => onMoveInput;
            public ScriptableEventVector2 LookInputEvent => onLookInput;
            public ScriptableEventBool SprintInputEvent => onSprintInput;
            public ScriptableEventFloat LeanInputEvent => onLeanInput;

            public ScriptableEventNoParam EquipEvent => onEquipInput;
            public ScriptableEventNoParam DropEvent     => onDropInput;
            public ScriptableEventNoParam PinPulling     => onPinPullingInput;
            public ScriptableEventNoParam InteractEvent => onInteractInput;

            // Runtime input control
            private bool moveEnabled = true;
            private bool lookEnabled = true;
            private bool jumpEnabled = true;
            private bool fireEnabled = true;
            private bool sprintEnabled = true;
            private bool leanEnabled = true;
            private bool interactEnabled = true;
            private bool equipEnabled = true;
            private bool dropEnabled = true;
            private bool pinPullingEnabled = true;

            public bool IsFireHolding =>
                inputActions != null && inputActions.Gameplay.Fire.IsPressed();

            public bool IsFireStartedThisFrame =>
                inputActions != null && inputActions.Gameplay.Fire.WasPressedThisFrame();

            public bool IsFireStoppedThisFrame =>
                inputActions != null && inputActions.Gameplay.Fire.WasReleasedThisFrame();
            
            public override void OnEnter()
            {
                if (inputActions == null)
                {
                    Debug.LogError("[GameplayContext] InputActions null!");
                    return;
                }
                
                inputActions.Gameplay.Enable();

                if (portingVariable != null && portingVariable.CurrentValue == AppMode.XR)
                {
                    SetMoveEnabled(false);
                    SetLookEnabled(false);
                    SetSprintEnabled(false);
                    SetLeanEnabled(false);
                    SetInteractEnabled(false);
                }
                
                inputActions.Gameplay.Move.performed += OnMove;
                inputActions.Gameplay.Move.canceled += OnMove;
                
                inputActions.Gameplay.Look.started += OnLook;
                inputActions.Gameplay.Look.performed += OnLook;
                inputActions.Gameplay.Look.canceled += OnLook;
                
                inputActions.Gameplay.Sprint.performed += ctx => OnSprint(true);
                inputActions.Gameplay.Sprint.canceled += ctx => OnSprint(false);

                inputActions.Gameplay.Lean.started += OnLean;
                inputActions.Gameplay.Lean.performed += OnLean;
                inputActions.Gameplay.Lean.canceled += OnLean;
                
                inputActions.Gameplay.Interact.performed += OnInteract;
                
                inputActions.Gameplay.Fire.performed += ctx => OnFire(true);
                inputActions.Gameplay.Fire.canceled += ctx => OnFire(false);

                inputActions.Gameplay.Equip.performed += OnEquip;
                inputActions.Gameplay.Drop.performed += OnDrop;

                // Use started (not performed) so OS key-repeat / hold does not re-raise pin pull for every repeat tick.
                inputActions.Gameplay.PinPulling.started += OnPinPulling;

                inputActions.Gameplay.GameplayFinished.performed += OnGameplayFinished;
            }
            
            public override void OnExit()
            {
                if (inputActions == null) return;
                
                inputActions.Gameplay.Move.performed -= OnMove;
                inputActions.Gameplay.Move.canceled -= OnMove;
                inputActions.Gameplay.Look.started -= OnLook;
                inputActions.Gameplay.Look.performed -= OnLook;
                inputActions.Gameplay.Look.canceled -= OnLook;
                inputActions.Gameplay.Interact.performed -= OnInteract;
                inputActions.Gameplay.GameplayFinished.performed -= OnGameplayFinished;

                inputActions.Gameplay.Sprint.performed -= ctx => OnSprint(true);
                inputActions.Gameplay.Sprint.canceled -= ctx => OnSprint(false);

                inputActions.Gameplay.Lean.started -= OnLean;
                inputActions.Gameplay.Lean.performed -= OnLean;
                inputActions.Gameplay.Lean.canceled -= OnLean;

                inputActions.Gameplay.Fire.performed -= ctx => OnFire(true);
                inputActions.Gameplay.Fire.canceled -= ctx => OnFire(false);

                inputActions.Gameplay.Equip.performed -= OnEquip;
                inputActions.Gameplay.Drop.performed -= OnDrop;

                inputActions.Gameplay.PinPulling.started -= OnPinPulling;

                inputActions.Gameplay.Disable();
            }
            
            private void OnMove(InputAction.CallbackContext ctx)
            {
                if (moveEnabled) onMoveInput?.Raise(ctx.ReadValue<Vector2>());
            }
            
            private void OnLook(InputAction.CallbackContext ctx)
            {
                if (lookEnabled) onLookInput?.Raise(ctx.ReadValue<Vector2>());
            }
            
            private void OnJump(bool isPressed)
            {
                if (jumpEnabled) onJumpInput?.Raise(isPressed);
            }
            
            private void OnFire(bool isPressed)
            {
                if (fireEnabled) onFireInput?.Raise(isPressed);
            }
            
            private void OnSprint(bool isPressed)
            {
                if (sprintEnabled) onSprintInput?.Raise(isPressed);
            }

            private void OnLean(InputAction.CallbackContext ctx)
            {
                if (!leanEnabled)
                {
                    onLeanInput?.Raise(0f);
                    return;
                }

                float value = 0f;
                if (ctx.started || ctx.performed)
                {
                    value = ctx.ReadValueAsButton() ? 1f : ctx.ReadValue<float>();
                }

                onLeanInput?.Raise(value);
            }
            
            private void OnInteract(InputAction.CallbackContext ctx)
            {
                if (interactEnabled) onInteractInput?.Raise();
            }

            private void OnEquip(InputAction.CallbackContext ctx)
            {
                if (equipEnabled) onEquipInput?.Raise();
            }

            private void OnDrop(InputAction.CallbackContext ctx)
            {
                if (dropEnabled) onDropInput?.Raise();
            }

            private void OnPinPulling(InputAction.CallbackContext ctx)
            {
                if (pinPullingEnabled) onPinPullingInput?.Raise();
            }

            private void OnGameplayFinished(InputAction.CallbackContext ctx)
            {
                if (portingVariable != null && portingVariable.CurrentValue == AppMode.XR)
                {
                    preOnGameplayFinishedInput?.Raise();
                    return;
                }

                onGameplayFinishedInput?.Raise();
            }
            
            // Runtime input control methods
            public void SetMoveEnabled(bool enabled)
            {
                moveEnabled = enabled;
                if (!enabled) onMoveInput?.Raise(Vector2.zero);
            }
            
            public void SetLookEnabled(bool enabled)
            {
                lookEnabled = enabled;
                if (!enabled) onLookInput?.Raise(Vector2.zero);
            }
            
            public void SetJumpEnabled(bool enabled) => jumpEnabled = enabled;
            public void SetFireEnabled(bool enabled) => fireEnabled = enabled;
            public void SetSprintEnabled(bool enabled) => sprintEnabled = enabled;
            public void SetLeanEnabled(bool enabled)
            {
                leanEnabled = enabled;
                if (!enabled)
                {
                    onLeanInput?.Raise(0f);
                }
            }

            public void SetInteractEnabled(bool enabled) => interactEnabled = enabled;
            public void SetEquipEnabled(bool enabled) => equipEnabled = enabled;
            public void SetDropEnabled(bool enabled) => dropEnabled = enabled;
            public void SetPinPullingEnabled(bool enabled) => pinPullingEnabled = enabled;
            
            public void EnableAllInputs()
            {
                moveEnabled = true;
                lookEnabled = true;
                jumpEnabled = true;
                fireEnabled = true;
                sprintEnabled = true;
                leanEnabled = true;
                interactEnabled = true;
                equipEnabled = true;
                dropEnabled = true;
                pinPullingEnabled = true;
            }
            
            public void DisableAllInputs()
            {
                moveEnabled = false;
                lookEnabled = false;
                jumpEnabled = false;
                fireEnabled = false;
                sprintEnabled = false;
                leanEnabled = false;
                interactEnabled = false;
                equipEnabled = false;
                dropEnabled = false;
                pinPullingEnabled = false;
                onMoveInput?.Raise(Vector2.zero);
                onLookInput?.Raise(Vector2.zero);
                onLeanInput?.Raise(0f);
            }
        }


        public interface IFireInputReader
        {
            bool IsFireHolding { get; }
            bool IsFireStartedThisFrame { get; }
            bool IsFireStoppedThisFrame { get; }
        }


}



