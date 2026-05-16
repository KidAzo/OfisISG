using UnityEngine;
// ============================================================================
// CORE 4: UI INPUT CONTEXT
// ============================================================================

namespace Woi.InputSystem
{

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
}
