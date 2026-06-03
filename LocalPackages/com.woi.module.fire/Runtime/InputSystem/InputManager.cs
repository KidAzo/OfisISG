using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Obvious.Soap;
using Woi.Player;

namespace Woi.InputSystem
{

public class InputManager : MonoBehaviour, IInputProvider
{
    [SerializeField] private InputSets inputSets;
    private PlayerInputActions inputActions;
    private InputContextStack contextStack;
    
    public PlayerInputActions InputActions => inputActions;
    [SerializeField] private ScriptableEnumPortingVariable portingVariable;

    private void Awake()
    {
        InitializePortingRuntime();
    }

    /// <summary>
    /// Registers <see cref="FirePlatformRuntime"/> from the assigned porting asset (PC vs XR).
    /// Callable from bootstrap installers without referencing <c>Woi.Porting</c> types.
    /// </summary>
    public void InitializePortingRuntime()
    {
        if (portingVariable == null)
        {
            Debug.LogError(
                "[InputManager] portingVariable is not assigned — PC/XR input contexts and FirePlatformRuntime will be wrong. " +
                "Assign PortingVariable.asset on the InputManager prefab and rebuild Addressables.");
            return;
        }

        FirePlatformRuntime.TryInitialize(portingVariable);
    }

    private void Start()
    {
        if (portingVariable == null)
        {
            Debug.LogError(
                "[InputManager] portingVariable is not assigned — cannot choose PC/XR input contexts.");
            return;
        }

        // FireServiceInstaller may call EnsurePcGameplayInputEnabled() before Start — do not create a second instance.
        EnsureInputSystemInitialized();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsurePcGameplayInputEnabled();
    }

    private void Update()
    {
        contextStack?.Update();
    }
    
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ShutdownInputSystem();
    }

    /// <summary>
    /// Creates <see cref="PlayerInputActions"/> and pushes input contexts once.
    /// Safe if bootstrap called <see cref="EnsurePcGameplayInputEnabled"/> before <see cref="Start"/>.
    /// </summary>
    private void EnsureInputSystemInitialized()
    {
        if (inputActions != null)
        {
            return;
        }

        inputActions = new PlayerInputActions();
        inputActions.Enable();
        contextStack ??= new InputContextStack();

        var contexts = GetInputContexts(portingVariable.CurrentValue);
        PushContexts(contexts);
    }

    private void ShutdownInputSystem()
    {
        contextStack?.Clear();
        contextStack = null;

        if (inputActions == null)
        {
            return;
        }

        inputActions.Disable();
        inputActions.Dispose();
        inputActions = null;
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

    /// <summary>
    /// Ensures PC gameplay actions and Soap move/look events are active (e.g. after additive office scene load).
    /// Safe to call multiple times; does not re-subscribe input callbacks.
    /// </summary>
    public void EnsurePcGameplayInputEnabled()
    {
        if (portingVariable != null && portingVariable.CurrentValue != AppMode.PC)
            return;

        if (inputActions == null)
        {
            if (portingVariable == null)
            {
                Debug.LogError("[InputManager] portingVariable is not assigned — cannot initialize input.");
                return;
            }

            EnsureInputSystemInitialized();
            SyncPcPlayerSoapEvents();
            return;
        }

        inputActions.Enable();
        if (!inputActions.Gameplay.enabled)
        {
            inputActions.Gameplay.Enable();
        }

        if (inputSets.PCContexts == null)
            return;

        for (int i = 0; i < inputSets.PCContexts.Length; i++)
        {
            if (inputSets.PCContexts[i] is GameplayInputContext gameplay)
                gameplay.EnableAllInputs();
        }

        SyncPcPlayerSoapEvents();
    }

    /// <summary>
    /// Addressables can load separate instances of Soap event assets. GameplayContext raises one instance;
    /// PlayerController may still listen to another from the scene prefab — this forces a single chain.
    /// </summary>
    public void SyncPcPlayerSoapEvents()
    {
        GameplayInputContext gameplay = GetPcGameplayContext();
        if (gameplay == null)
        {
            Debug.LogError(
                "[InputManager] PC GameplayInputContext missing — cannot sync player Soap events.");
            return;
        }

        ScriptableEventVector2 move = gameplay.MoveInputEvent;
        ScriptableEventVector2 look = gameplay.LookInputEvent;
        ScriptableEventBool sprint = gameplay.SprintInputEvent;

        ScriptableEventNoParam interact = gameplay.InteractEvent;

        if (move == null || look == null || sprint == null || interact == null)
        {
            Debug.LogError("[InputManager] PC GameplayInputContext has null move/look/sprint/interact Soap events.");
            return;
        }

        PlayerController[] players = UnityEngine.Object.FindObjectsByType<PlayerController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int rebound = 0;
        for (int i = 0; i < players.Length; i++)
        {
            PlayerController player = players[i];
            if (player == null || !player.gameObject.activeInHierarchy)
                continue;

            bool wasSplit =
                player.IsListeningToDifferentMoveEvent(move);

            player.RebindSoapInputEvents(move, look, sprint);
            rebound++;

            if (wasSplit)
            {
                Debug.LogWarning(
                    $"[InputManager] Rebound Soap input on '{player.name}' — Addressables had split event instances (WASD fix).",
                    player);
            }
        }

        int interactRebound = 0;
        MonoBehaviour[] allBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < allBehaviours.Length; i++)
        {
            if (allBehaviours[i] is not ISoapInteractInputListener listener)
            {
                continue;
            }

            if (!allBehaviours[i].gameObject.activeInHierarchy)
            {
                continue;
            }

            bool interactSplit = listener.IsListeningToDifferentInteractEvent(interact);
            listener.RebindInteractInputEvent(interact);
            interactRebound++;

            if (interactSplit)
            {
                Debug.LogWarning(
                    $"[InputManager] Rebound interact (E) on '{allBehaviours[i].name}' — split onInteractInput instance.",
                    allBehaviours[i]);
            }
        }

        if (rebound == 0)
        {
            Debug.LogWarning(
                "[InputManager] SyncPcPlayerSoapEvents: no active PlayerController found yet.");
        }
        else
        {
            Debug.Log(
                $"[InputManager] SyncPcPlayerSoapEvents: rebound {rebound} player(s). " +
                $"moveEvent='{move.name}' gameplayEnabled={inputActions != null && inputActions.Gameplay.enabled}");
        }

        if (interactRebound == 0)
        {
            Debug.LogWarning(
                "[InputManager] SyncPcPlayerSoapEvents: no active ISoapInteractInputListener (E/doors) found yet.");
        }
        else
        {
            Debug.Log(
                $"[InputManager] SyncPcPlayerSoapEvents: rebound interact on {interactRebound} listener(s). " +
                $"interactEvent='{interact.name}'");
        }
    }

    public GameplayInputContext GetPcGameplayContext()
    {
        if (inputSets.PCContexts == null)
            return null;

        for (int i = 0; i < inputSets.PCContexts.Length; i++)
        {
            if (inputSets.PCContexts[i] is GameplayInputContext gameplay)
                return gameplay;
        }

        return null;
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