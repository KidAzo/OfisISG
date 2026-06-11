using UnityEngine;

namespace Woi.InputSystem
{
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

    /// <summary>True after <see cref="InputManager"/> pushed this context onto the input stack.</summary>
    public bool HasInitializedInputActions => inputActions != null;
    
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
}
