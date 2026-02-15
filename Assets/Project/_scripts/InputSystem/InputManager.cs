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
    
    [SerializeField] private PortingController portingService;
    private InputContext currentContext;    

    private void Awake()
    {
        inputActions = new PlayerInputActions();
        inputActions.Enable();  
        contextStack = new InputContextStack();

        Debug.Log(portingService);

        var contexts = GetInputContexts(portingService.CurrentMode);
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

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            inputActions.Disable();
        }
        else
        {
            inputActions.Enable();
        }
    }

    private InputContext[] GetInputContexts(AppMode mode)
    {
        bool isVrMode = portingService.CurrentMode == AppMode.VR;
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