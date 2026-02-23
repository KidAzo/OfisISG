using UnityEngine;
using UnityEngine.InputSystem;
using Obvious.Soap;
using Woi.Porting;

[CreateAssetMenu(menuName = "Input System/Contexts/Gameplay Context")]
public class GameplayInputContext : InputContext
{
    [Header("Movement Events")]
    [SerializeField] private ScriptableEventVector2 onMoveInput;
    [SerializeField] private ScriptableEventVector2 onLookInput;
    
    [Header("Action Events")]
    [SerializeField] private ScriptableEventBool onJumpInput;
    [SerializeField] private ScriptableEventBool onFireInput;
    [SerializeField] private ScriptableEventBool onSprintInput;
    [SerializeField] private ScriptableEventNoParam onInteractInput;
    [SerializeField] private ScriptableEventNoParam onGameplayFinishedInput;
    [SerializeField] private ScriptableEventNoParam preOnGameplayFinishedInput;
    [SerializeField] private ScriptableEnumPortingVariable portingVariable;
    
    // Runtime input control
    private bool moveEnabled = true;
    private bool lookEnabled = true;
    private bool jumpEnabled = true;
    private bool fireEnabled = true;
    private bool sprintEnabled = true;
    private bool interactEnabled = true;
    
    public override void OnEnter()
    {
        if (inputActions == null)
        {
            Debug.LogError("[GameplayContext] InputActions null!");
            return;
        }
        
        inputActions.Gameplay.Enable();
        
        inputActions.Gameplay.Move.performed += OnMove;
        inputActions.Gameplay.Move.canceled += OnMove;
        
        inputActions.Gameplay.Look.performed += OnLook;
        inputActions.Gameplay.Look.canceled += OnLook;
        
        inputActions.Gameplay.Sprint.performed += ctx => OnSprint(true);
        inputActions.Gameplay.Sprint.canceled += ctx => OnSprint(false);
        
        inputActions.Gameplay.Interact.performed += OnInteract;
        inputActions.Gameplay.GameplayFinished.performed += OnGameplayFinished;
    }
    
    public override void OnExit()
    {
        if (inputActions == null) return;
        
        inputActions.Gameplay.Move.performed -= OnMove;
        inputActions.Gameplay.Move.canceled -= OnMove;
        inputActions.Gameplay.Look.performed -= OnLook;
        inputActions.Gameplay.Look.canceled -= OnLook;
        inputActions.Gameplay.Interact.performed -= OnInteract;
        inputActions.Gameplay.GameplayFinished.performed -= OnGameplayFinished;
        
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
    
    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (interactEnabled) onInteractInput?.Raise();
    }

    private void OnGameplayFinished(InputAction.CallbackContext ctx)
    {
        if(portingVariable.Value == AppMode.XR)
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
    public void SetInteractEnabled(bool enabled) => interactEnabled = enabled;
    
    public void EnableAllInputs()
    {
        moveEnabled = true;
        lookEnabled = true;
        jumpEnabled = true;
        fireEnabled = true;
        sprintEnabled = true;
        interactEnabled = true;
    }
    
    public void DisableAllInputs()
    {
        moveEnabled = false;
        lookEnabled = false;
        jumpEnabled = false;
        fireEnabled = false;
        sprintEnabled = false;
        interactEnabled = false;
        onMoveInput?.Raise(Vector2.zero);
        onLookInput?.Raise(Vector2.zero);
    }
}


