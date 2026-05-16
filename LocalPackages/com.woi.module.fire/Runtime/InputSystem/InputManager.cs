using System;
using UnityEngine;
using Obvious.Soap;

namespace Woi.InputSystem
{

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

        var contexts = GetInputContexts(portingVariable.CurrentValue);
        PushContexts(contexts);
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

    /*private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            inputActions.Disable();
        }
        else
        {
            inputActions.Enable();
        }
    }*/

    private InputContext[] GetInputContexts(AppMode mode)
    {
        bool isVrMode = portingVariable.CurrentValue == AppMode.XR;
        var currentSet = !isVrMode ? inputSets.PCContexts : inputSets.XRContexts;
        
        return currentSet;
    } 

    [Serializable]
    public struct InputSets
    {
        public InputContext[] PCContexts;
        public InputContext[] XRContexts;
    }     
}

public interface IInputProvider
{
    PlayerInputActions InputActions { get; }
    void PushContexts(InputContext[] contexts);
    void PopContext(InputContext context);
    void ClearAllContexts();
}

}