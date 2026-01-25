using System;
using UnityEngine;

public class InputManager : MonoBehaviour, IInputProvider
{
    [SerializeField] private InputContext[] contexts;
    private PlayerInputActions inputActions;
    private InputContextStack contextStack;
    
    public PlayerInputActions InputActions => inputActions;
    
    private void Awake()
    {
        inputActions = new PlayerInputActions();
        inputActions.Enable();  
        contextStack = new InputContextStack();
        PushContexts(contexts);
        Debug.Log("[InputManager] Initialized");
    }
    
    private void Update()
    {
        contextStack?.Update();
    }
    
    private void OnDestroy()
    {
        inputActions?.Disable();
        contextStack?.Clear();
        inputActions?.Dispose();
    }
    
    public void PushContexts(InputContext[] contexts)
    {
        foreach (var ctx in contexts)
        {
            ctx.Initialize(inputActions);
            contextStack.PushContext(ctx);
        }
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

public interface IInputProvider
{
    PlayerInputActions InputActions { get; }
    void PushContexts(InputContext[] contexts);
    void PopContext(InputContext context);
    void ClearAllContexts();
}