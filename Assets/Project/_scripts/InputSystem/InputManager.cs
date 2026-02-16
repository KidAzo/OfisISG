using System;
using Reflex.Attributes;
using UnityEngine;
using Woi.Porting;

public class InputManager : MonoBehaviour, IInputProvider
{
    [SerializeField] private InputSets inputSets;
    private PlayerInputActions inputActions;
    private InputContextStack contextStack;
    
    public PlayerInputActions InputActions => inputActions;
    
    [SerializeField] private ScriptableEnumPortingVariable portingVariable;

    private void Start()
    {
        inputActions = new PlayerInputActions();
        inputActions.Enable();  
        contextStack = new InputContextStack();

        Debug.Log(portingVariable.Value);

        var contexts = GetInputContexts(portingVariable.Value);
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
            Debug.Log($"[InputManager] Pushed context: {ctx.name}");
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

    // private void OnApplicationFocus(bool hasFocus)
    // {
    //     if (!hasFocus)
    //     {
    //         inputActions.Disable();
    //     }
    //     else
    //     {
    //         inputActions.Enable();
    //     }
    // }

    private InputContext[] GetInputContexts(AppMode mode)
    {
        bool isVrMode = portingVariable.Value == AppMode.XR;
        var currentSet = !isVrMode ? inputSets.GameplayContexts : inputSets.VrContexts;
        
        return currentSet;
    }  

    [Serializable]
    public struct InputSets
    {
        public InputContext[] GameplayContexts;
        public InputContext[] VrContexts;
    }     
}

public interface IInputProvider
{
    PlayerInputActions InputActions { get; }
    void PushContexts(InputContext[] contexts);
    void PopContext(InputContext context);
    void ClearAllContexts();
}