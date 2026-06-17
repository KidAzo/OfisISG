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
        EnsureVrGameplayInputEnabled();
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
        ScriptableEventFloat lean = gameplay.LeanInputEvent;

        if (move == null || look == null || sprint == null || interact == null || lean == null)
        {
            Debug.LogError("[InputManager] PC GameplayInputContext has null move/look/sprint/interact/lean Soap events.");
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

        int leanRebound = 0;
        for (int i = 0; i < allBehaviours.Length; i++)
        {
            if (allBehaviours[i] is not ISoapLeanInputListener leanListener)
            {
                continue;
            }

            if (!allBehaviours[i].gameObject.activeInHierarchy)
            {
                continue;
            }

            bool leanSplit = leanListener.IsListeningToDifferentLeanEvent(lean);
            leanListener.RebindLeanInputEvent(lean);
            leanRebound++;

            if (leanSplit)
            {
                Debug.LogWarning(
                    $"[InputManager] Rebound lean (Ctrl) on '{allBehaviours[i].name}' — split onLeanInput instance.",
                    allBehaviours[i]);
            }
        }

        if (leanRebound == 0)
        {
            Debug.LogWarning(
                "[InputManager] SyncPcPlayerSoapEvents: no active ISoapLeanInputListener (Ctrl lean) found yet.");
        }
        else
        {
            Debug.Log(
                $"[InputManager] SyncPcPlayerSoapEvents: rebound lean on {leanRebound} listener(s). " +
                $"leanEvent='{lean.name}'");
        }

        GameplayInputContext liveGameplayContext = GetPcGameplayContext();
        int gameplayContextRebound = 0;
        if (liveGameplayContext != null)
        {
            for (int i = 0; i < allBehaviours.Length; i++)
            {
                if (allBehaviours[i] is not ISoapGameplayInputContextListener gameplayListener)
                    continue;

                if (!allBehaviours[i].gameObject.activeInHierarchy)
                    continue;

                bool contextSplit = gameplayListener.IsUsingDifferentGameplayInputContext(liveGameplayContext);
                gameplayListener.RebindGameplayInputContext(liveGameplayContext);
                gameplayContextRebound++;

                if (contextSplit)
                {
                    Debug.LogWarning(
                        $"[InputManager] Rebound GameplayInputContext on '{allBehaviours[i].name}' — Addressables had split context instance (E/pickup fix).",
                        allBehaviours[i]);
                }
            }
        }

        if (gameplayContextRebound == 0)
        {
            Debug.LogWarning(
                "[InputManager] SyncPcPlayerSoapEvents: no active ISoapGameplayInputContextListener (extinguisher/pickup) found yet.");
        }
        else
        {
            Debug.Log(
                $"[InputManager] SyncPcPlayerSoapEvents: rebound GameplayInputContext on {gameplayContextRebound} listener(s). " +
                $"context='{liveGameplayContext.name}'");
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

    public VrInputContext GetVrInputContext()
    {
        if (inputSets.XRContexts == null)
            return null;

        for (int i = 0; i < inputSets.XRContexts.Length; i++)
        {
            if (inputSets.XRContexts[i] is VrInputContext vrContext)
                return vrContext;
        }

        return null;
    }

    /// <summary>
    /// Ensures VR gameplay actions stay active after additive scene loads (grab, fire, pin-pull on Gameplay map).
    /// </summary>
    public void EnsureVrGameplayInputEnabled()
    {
        if (portingVariable != null && portingVariable.CurrentValue != AppMode.XR)
            return;

        if (inputActions == null)
        {
            if (portingVariable == null)
            {
                Debug.LogError("[InputManager] portingVariable is not assigned — cannot initialize VR input.");
                return;
            }

            EnsureInputSystemInitialized();
            SyncVrInteractSoapEvents();
            SyncVrGripSoapEvents();
            return;
        }

        inputActions.Enable();
        if (!inputActions.XR.enabled)
            inputActions.XR.Enable();
        if (!inputActions.Gameplay.enabled)
            inputActions.Gameplay.Enable();

        SyncVrInteractSoapEvents();
        SyncVrGripSoapEvents();
    }

    /// <summary>
    /// Rebinds scene listeners to the live <see cref="VrInputContext"/> Soap interact event
    /// (Addressables can duplicate ScriptableObject instances — same fix as PC WASD sync).
    /// </summary>
    public void SyncVrInteractSoapEvents()
    {
        VrInputContext vrContext = GetVrInputContext();
        if (vrContext == null)
        {
            Debug.LogError(
                "[InputManager] VrInputContext missing — cannot sync VR interact Soap events. " +
                "Assign XR-InputContext on InputManager prefab and rebuild Addressables.");
            return;
        }

        ScriptableEventNoParam interact = vrContext.InteractEvent;
        if (interact == null)
        {
            Debug.LogError("[InputManager] VrInputContext has null onInteractInput Soap event.");
            return;
        }

        MonoBehaviour[] allBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int rebound = 0;
        for (int i = 0; i < allBehaviours.Length; i++)
        {
            if (allBehaviours[i] is not ISoapInteractInputListener listener)
                continue;

            if (!allBehaviours[i].gameObject.activeInHierarchy)
                continue;

            bool wasSplit = listener.IsListeningToDifferentInteractEvent(interact);
            listener.RebindInteractInputEvent(interact);
            rebound++;

            if (wasSplit)
            {
                Debug.LogWarning(
                    $"[InputManager] Rebound VR interact on '{allBehaviours[i].name}' — Addressables had split onInteractInput instance.",
                    allBehaviours[i]);
            }
        }

        if (rebound == 0)
        {
            Debug.LogWarning(
                "[InputManager] SyncVrInteractSoapEvents: no active ISoapInteractInputListener found yet.");
        }
        else
        {
            Debug.Log(
                $"[InputManager] SyncVrInteractSoapEvents: rebound interact on {rebound} listener(s). " +
                $"interactEvent='{interact.name}' xrEnabled={inputActions != null && inputActions.XR.enabled} " +
                $"gameplayEnabled={inputActions != null && inputActions.Gameplay.enabled}");
        }
    }

    /// <summary>
    /// Rebinds scene listeners to the live <see cref="VrInputContext"/> grip Soap event
    /// (Addressables can duplicate ScriptableObject instances).
    /// </summary>
    public void SyncVrGripSoapEvents()
    {
        VrInputContext vrContext = GetVrInputContext();
        if (vrContext == null)
        {
            Debug.LogError(
                "[InputManager] VrInputContext missing — cannot sync VR grip Soap events. " +
                "Assign XR-InputContext on InputManager prefab and rebuild Addressables.");
            return;
        }

        ScriptableEventNoParam grip = vrContext.PreOnGameplayFinishedEvent;
        if (grip == null)
        {
            Debug.LogError("[InputManager] VrInputContext has null preOnGameplayFinishedInput Soap event.");
            return;
        }

        MonoBehaviour[] allBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int rebound = 0;
        for (int i = 0; i < allBehaviours.Length; i++)
        {
            if (allBehaviours[i] is not ISoapVrGripInputListener listener)
                continue;

            if (!allBehaviours[i].gameObject.activeInHierarchy)
                continue;

            bool wasSplit = listener.IsListeningToDifferentGripEvent(grip);
            listener.RebindGripInputEvent(grip);
            rebound++;

            if (wasSplit)
            {
                Debug.LogWarning(
                    $"[InputManager] Rebound VR grip on '{allBehaviours[i].name}' — Addressables had split preOnGameFinishEvent instance.",
                    allBehaviours[i]);
            }
        }

        if (rebound == 0)
        {
            Debug.LogWarning(
                "[InputManager] SyncVrGripSoapEvents: no active ISoapVrGripInputListener found yet.");
        }
        else
        {
            Debug.Log(
                $"[InputManager] SyncVrGripSoapEvents: rebound grip on {rebound} listener(s). " +
                $"gripEvent='{grip.name}' xrEnabled={inputActions != null && inputActions.XR.enabled}");
        }
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