using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Obvious.Soap;

public abstract class InputContext : ScriptableObject
{
    [Header("Context Info")]
    [SerializeField] protected string contextName = "Context";
    [SerializeField] protected int priority = 0;
    [SerializeField] protected bool blockLowerContexts = true;
    
    public string ContextName => contextName;
    public int Priority => priority;
    public bool BlockLowerContexts => blockLowerContexts;
    
    protected PlayerInputActions inputActions;
    
    public void Initialize(PlayerInputActions actions)
    {
        inputActions = actions;
        OnInitialize();
    }
    
    protected virtual void OnInitialize() { }
    
    public abstract void OnEnter();
    public abstract void OnExit();
    public virtual void OnUpdate() { }
}

// ============================================================================
// CORE 3: GAMEPLAY INPUT CONTEXT
// ============================================================================

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
        
        inputActions.Gameplay.Jump.performed += ctx => OnJump(true);
        inputActions.Gameplay.Jump.canceled += ctx => OnJump(false);
        
        inputActions.Gameplay.Sprint.performed += ctx => OnSprint(true);
        inputActions.Gameplay.Sprint.canceled += ctx => OnSprint(false);
        
        inputActions.Gameplay.Interact.performed += OnInteract;
        
        Debug.Log($"[{contextName}] Entered");
    }
    
    public override void OnExit()
    {
        if (inputActions == null) return;
        
        inputActions.Gameplay.Move.performed -= OnMove;
        inputActions.Gameplay.Move.canceled -= OnMove;
        inputActions.Gameplay.Look.performed -= OnLook;
        inputActions.Gameplay.Look.canceled -= OnLook;
        inputActions.Gameplay.Interact.performed -= OnInteract;
        
        inputActions.Gameplay.Disable();
        
        Debug.Log($"[{contextName}] Exited");
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

// ============================================================================
// CORE 4: UI INPUT CONTEXT
// ============================================================================


public class InputContextStack
{
    private System.Collections.Generic.List<InputContext> contextStack = 
        new System.Collections.Generic.List<InputContext>();
    
    public void PushContext(InputContext context)
    {
        if (context == null || contextStack.Contains(context))
        {
            Debug.LogWarning("Cannot push null or duplicate context");
            return;
        }
        
        if (contextStack.Count > 0)
        {
            var topContext = contextStack[contextStack.Count - 1];
            if (context.BlockLowerContexts)
            {
                topContext.OnExit();
            }
        }
        
        contextStack.Add(context);
        contextStack.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        
        context.OnEnter();
    }
    
    public void PopContext(InputContext context)
    {
        if (!contextStack.Contains(context)) return;
        
        context.OnExit();
        contextStack.Remove(context);
        
        if (contextStack.Count > 0)
        {
            var topContext = contextStack[contextStack.Count - 1];
            topContext.OnEnter();
        }
    }
    
    public void Update()
    {
        if (contextStack.Count > 0)
        {
            contextStack[contextStack.Count - 1].OnUpdate();
        }
    }
    
    public void Clear()
    {
        foreach (var context in contextStack)
        {
            context.OnExit();
        }
        contextStack.Clear();
    }
}

public class InputManager : MonoBehaviour
{
    [SerializeField] private bool dontDestroyOnLoad = true;
    
    private PlayerInputActions inputActions;
    private InputContextStack contextStack;
    
    public PlayerInputActions InputActions => inputActions;
    
    private void Initialize()
    {
        inputActions = new PlayerInputActions();
        contextStack = new InputContextStack();
        Debug.Log("[InputManager] Initialized");
    }
    
    private void Update()
    {
        contextStack?.Update();
    }
    
    private void OnDestroy()
    {
        contextStack?.Clear();
        inputActions?.Dispose();
    }
    
    public void PushContext(InputContext context)
    {
        if (context == null)
        {
            Debug.LogError("Cannot push null context");
            return;
        }
        
        context.Initialize(inputActions);
        contextStack.PushContext(context);
    }
    
    public void PopContext(InputContext context)
    {
        contextStack.PopContext(context);
    }
    
    public void ClearAllContexts()
    {
        contextStack.Clear();
    }
}