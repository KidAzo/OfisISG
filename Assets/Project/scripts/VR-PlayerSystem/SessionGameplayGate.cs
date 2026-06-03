using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Woi.InputSystem;
using Woi.OfficeFire;
using Woi.Player;
using Woi.WasteCollectionMode;
using WOI.Modules.SDK;

namespace Woi.DataHandler
{
    /// <summary>
    /// Blocks locomotion until <see cref="SessionManager"/> receives Name|ID from the local server,
    /// shows the session profile overlay, then starts gameplay after a short reveal delay.
    /// Overlay stays hidden while bootstrap / login scenes are loaded and until the gameplay scene is active.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class SessionGameplayGate : MonoBehaviour
    {
        private static readonly string[] DefaultBlockedSceneNames =
        {
            "FireModule_Bootstrapper",
            "Bootstrapper",
            "WasteLogin",
        };

        [Header("UI")]
        [Tooltip("Persistent SessionProfileUI on NetworkingSystem (same lifecycle as WasteCollectionUI — never destroyed at runtime).")]
        [FormerlySerializedAs("sessionProfileUiTemplate")]
        [SerializeField] private GameObject sessionProfileUiRoot;
        [SerializeField] private SessionProfileOverlayController profileOverlay;

        [Header("Scenes")]
        [Tooltip("Session overlay runs only when the active scene is one of these (e.g. FireModule_Office).")]
        [SerializeField]
        private string[] sessionOverlaySceneNames =
        {
            "FireModule_Office",
        };

        [Tooltip("Legacy / unused — bootstrapper is detected via the active scene name only (bootstrapper stays loaded additively).")]
        [SerializeField]
        private string[] blockedSceneNames = DefaultBlockedSceneNames;

        [SerializeField] private bool logGateDecisions;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float revealDurationSeconds = 2.5f;

        [Header("Gameplay start")]
        [SerializeField] private bool startScenarioWhenSessionReady = true;
        [SerializeField] private OfficeFireScenarioBootstrapper scenarioBootstrapper;
        [SerializeField] private bool disableScenarioAutoStartOnAwake = true;
        [SerializeField] private bool teleportPlayerOnScenarioStart;

        [Header("Player")]
        [SerializeField] private Transform playerRoot;
        [SerializeField] private bool findPlayerByTagIfMissing = true;
        [SerializeField] private string playerTag = "Player";

        [Tooltip("Only freezes walk/look speed fields. UI (language buttons) stays clickable.")]
        [SerializeField] private bool freezeMovementWhileOverlayVisible = true;

        private readonly PlayerMovementLookFreeze movementLookFreeze = new();

        private SessionManager sessionManager;
        private PlayerSession pendingSession;
        private bool gameplayUnlocked;
        private bool revealRoutineRunning;
        private bool overlayGateActive;
        private bool movementFrozen;
        private bool vrSessionInputApplied;
        private bool overlayContentShown;
        private Coroutine revealRoutine;
        private Coroutine waitForOverlayRoutine;

        private void Awake()
        {
            sessionManager = SessionManager.Instance;
            if (sessionManager == null)
                sessionManager = FindFirstObjectByType<SessionManager>();

            ResolveSessionUiRoot();
            BindSessionUiComponents();
            EnsureSessionUiHostActive();
            HideOverlayImmediate();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            if (sessionManager != null)
                sessionManager.OnSessionReady += OnSessionReady;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            if (sessionManager != null)
                sessionManager.OnSessionReady -= OnSessionReady;

            RestoreVrSessionInputState();
            StopWaitRoutine();
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }
        }

        private void Start()
        {
            ScheduleOverlayRefresh();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsOverlayScene(scene))
                HandleOverlaySceneLoaded();
            else
                ScheduleOverlayRefresh();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            if (IsOverlayScene(next) && (gameplayUnlocked || revealRoutineRunning))
                HandleOverlaySceneLoaded();
            else
                ScheduleOverlayRefresh();
        }

        private void ScheduleOverlayRefresh()
        {
            if (gameplayUnlocked)
            {
                HideOverlayImmediate();
                return;
            }

            StopWaitRoutine();
            waitForOverlayRoutine = StartCoroutine(WaitThenRefreshOverlayGate());
        }

        private void StopWaitRoutine()
        {
            if (waitForOverlayRoutine == null)
                return;

            StopCoroutine(waitForOverlayRoutine);
            waitForOverlayRoutine = null;
        }

        private IEnumerator WaitThenRefreshOverlayGate()
        {
            const int maxFrames = 900;
            for (int frame = 0; frame < maxFrames; frame++)
            {
                RefreshOverlayGate();
                if (overlayGateActive || gameplayUnlocked)
                    break;

                yield return null;
            }

            if (!overlayGateActive && !gameplayUnlocked)
            {
                Debug.LogWarning(
                    "[SessionGameplayGate] Session overlay did not open within the wait window. " +
                    $"Active scene='{SceneManager.GetActiveScene().name}'. " +
                    "Expect active scene FireModule_Office with OfficeFireScenarioBootstrapper.",
                    this);
            }

            waitForOverlayRoutine = null;
        }

        private void RefreshOverlayGate()
        {
            if (gameplayUnlocked)
            {
                HideOverlayImmediate();
                return;
            }

            ResolveSessionUiRoot();
            BindSessionUiComponents();

            overlayGateActive = CanShowSessionOverlay();

            if (!overlayGateActive)
            {
                HideOverlayImmediate();
                return;
            }

            TryBindScenarioBootstrapper();
            ApplyOverlayPresentationState();

            PlayerSession session = GetActiveOrPendingSession();
            if (session != null && !revealRoutineRunning)
                BeginRevealRoutine(session);
            else
                ShowOverlayWaiting();
        }

        private bool CanShowSessionOverlay()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            if (IsBootstrapActiveScene())
            {
                LogGateDecision("blocked: active scene is bootstrap/login");
                return false;
            }

            if (!activeScene.IsValid() || !IsOverlayScene(activeScene))
            {
                LogGateDecision($"blocked: active scene '{activeScene.name}' is not a gameplay overlay scene");
                return false;
            }

            if (!HasScenarioBootstrapperInScene(activeScene))
            {
                LogGateDecision($"blocked: no OfficeFireScenarioBootstrapper in '{activeScene.name}'");
                return false;
            }

            if (!HasGameplayCameraInScene(activeScene) && ResolvePlayerRoot() == null)
            {
                LogGateDecision($"blocked: no camera/player yet in '{activeScene.name}' (still waiting)");
                return false;
            }

            LogGateDecision($"allowed in '{activeScene.name}'");
            return true;
        }

        private void LogGateDecision(string reason)
        {
            if (!logGateDecisions)
                return;

            Debug.Log($"[SessionGameplayGate] {reason}", this);
        }

        private static bool IsBootstrapActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return true;

            string name = activeScene.name;
            return name.IndexOf("Bootstrapper", StringComparison.OrdinalIgnoreCase) >= 0
                   || string.Equals(name, "WasteLogin", StringComparison.Ordinal);
        }

        private static bool HasScenarioBootstrapperInScene(Scene scene)
        {
            if (!scene.IsValid())
                return false;

            OfficeFireScenarioBootstrapper[] bootstrappers =
                FindObjectsByType<OfficeFireScenarioBootstrapper>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < bootstrappers.Length; i++)
            {
                OfficeFireScenarioBootstrapper bootstrapper = bootstrappers[i];
                if (bootstrapper != null && bootstrapper.gameObject.scene == scene)
                    return true;
            }

            return false;
        }

        private static bool HasGameplayCameraInScene(Scene scene)
        {
            if (!scene.IsValid())
                return false;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Camera[] cameras = roots[r].GetComponentsInChildren<Camera>(true);
                for (int c = 0; c < cameras.Length; c++)
                {
                    Camera camera = cameras[c];
                    if (camera == null)
                        continue;

                    if (camera.isActiveAndEnabled && camera.gameObject.activeInHierarchy)
                        return true;
                }
            }

            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r].GetComponentsInChildren<Camera>(true).Length > 0)
                    return true;
            }

            return false;
        }

        private void HideOverlayImmediate()
        {
            overlayGateActive = false;
            overlayContentShown = false;

            if (profileOverlay != null)
                profileOverlay.SetVisible(false);

            RestoreMovementAfterOverlay();
            RestoreVrSessionInputState();
        }

        private void ResolveSessionUiRoot()
        {
            if (sessionProfileUiRoot != null && sessionProfileUiRoot.scene.isLoaded)
                return;

            sessionProfileUiRoot = null;

            Transform legacy = transform.Find("SessionProfileUI");
            if (legacy == null)
                legacy = transform.Find("SessionProfileUI_Template");

            if (legacy != null)
            {
                sessionProfileUiRoot = legacy.gameObject;
                return;
            }

            if (profileOverlay != null)
            {
                sessionProfileUiRoot = profileOverlay.gameObject;
                return;
            }

            sessionProfileUiRoot = FindSessionProfileUiInOverlayScenes();
        }

        private GameObject FindSessionProfileUiInOverlayScenes()
        {
            string[] allowedNames = GetEffectiveOverlaySceneNames();

            SessionProfileOverlayController[] overlays = FindObjectsByType<SessionProfileOverlayController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < overlays.Length; i++)
            {
                SessionProfileOverlayController overlay = overlays[i];
                if (overlay == null)
                    continue;

                if (IsSceneNameAllowed(overlay.gameObject.scene, allowedNames))
                    return overlay.gameObject;
            }

            SessionProfileWorldUiPresenter[] presenters = FindObjectsByType<SessionProfileWorldUiPresenter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < presenters.Length; i++)
            {
                SessionProfileWorldUiPresenter presenter = presenters[i];
                if (presenter == null)
                    continue;

                if (IsSceneNameAllowed(presenter.gameObject.scene, allowedNames))
                    return presenter.gameObject;
            }

            return null;
        }

        private static bool IsSceneNameAllowed(Scene scene, string[] allowedNames)
        {
            if (!scene.IsValid() || allowedNames == null)
                return false;

            string sceneName = scene.name;
            for (int i = 0; i < allowedNames.Length; i++)
            {
                string allowed = allowedNames[i];
                if (!string.IsNullOrEmpty(allowed) && sceneName == allowed)
                    return true;
            }

            return false;
        }

        private void BindSessionUiComponents()
        {
            if (sessionProfileUiRoot == null)
                return;

            if (profileOverlay == null)
                profileOverlay = sessionProfileUiRoot.GetComponent<SessionProfileOverlayController>();
        }

        private void EnsureSessionUiHostActive()
        {
            if (sessionProfileUiRoot == null)
                return;

            if (!sessionProfileUiRoot.activeSelf)
                sessionProfileUiRoot.SetActive(true);
        }

        private void ApplySessionUiPresentation(bool visible)
        {
            if (sessionProfileUiRoot == null)
            {
                Debug.LogError(
                    "[SessionGameplayGate] sessionProfileUiRoot is missing. " +
                    "Run Woi/VR Networking/Setup Session UI On Networking Prefab.",
                    this);
                return;
            }

            EnsureSessionUiHostActive();

            if (visible)
                ApplyVrSessionInputState();
            else
                RestoreVrSessionInputState();
        }

        private void ApplyVrSessionInputState()
        {
            if (!WasteCollectionPlatform.ShouldUseVrPresentation() || vrSessionInputApplied)
                return;

            SessionProfileUiInputEnsurer.EnsureForSessionOverlay();
            vrSessionInputApplied = true;
        }

        private void RestoreVrSessionInputState()
        {
            if (!vrSessionInputApplied)
                return;

            SessionVrGameplayInputRestore.RestoreIfNeeded();
            vrSessionInputApplied = false;
        }

        private void ShowOverlayWaiting()
        {
            BindSessionUiComponents();
            ApplySessionUiPresentation(true);
            profileOverlay?.ShowWaiting();
            profileOverlay?.EnableInteraction();
            overlayContentShown = true;
        }

        private void ShowOverlaySession(PlayerSession session)
        {
            BindSessionUiComponents();
            ApplySessionUiPresentation(true);
            profileOverlay?.ShowSession(session);
            profileOverlay?.EnableInteraction();
            overlayContentShown = true;
        }

        private void TryBindScenarioBootstrapper()
        {
            if (scenarioBootstrapper != null)
                return;

            scenarioBootstrapper = FindFirstObjectByType<OfficeFireScenarioBootstrapper>();
            if (disableScenarioAutoStartOnAwake && scenarioBootstrapper != null)
                scenarioBootstrapper.SetAutoStartOnPlay(false);
        }

        private void OnSessionReady(PlayerSession session)
        {
            if (gameplayUnlocked || session == null || !session.IsActive)
                return;

            pendingSession = session;
            ScheduleOverlayRefresh();
        }

        private void BeginRevealRoutine(PlayerSession session)
        {
            if (!overlayGateActive || revealRoutineRunning)
                return;

            revealRoutineRunning = true;
            pendingSession = null;
            ShowOverlaySession(session);

            if (revealRoutine != null)
                StopCoroutine(revealRoutine);

            revealRoutine = StartCoroutine(RevealThenStartGameplay(session));
        }

        private IEnumerator RevealThenStartGameplay(PlayerSession session)
        {
            ApplyOverlayPresentationState();

            if (revealDurationSeconds > 0f)
                yield return new WaitForSecondsRealtime(revealDurationSeconds);

            gameplayUnlocked = true;
            revealRoutineRunning = false;
            revealRoutine = null;
            pendingSession = null;

            HideOverlayImmediate();
            StartGameplay(session);

            // Scenario roots activate after unlock; interact listeners subscribe on OnEnable with stale Soap refs unless we resync late.
            yield return null;
            yield return null;

            ResyncGameplaySoapEvents();
            ApplyGameplayCursorIfNeeded();
            SessionVrGameplayInputRestore.RestoreIfNeeded();
        }

        private void StartGameplay(PlayerSession session)
        {
            if (startScenarioWhenSessionReady && scenarioBootstrapper != null)
                scenarioBootstrapper.StartConfiguredScenario(teleportPlayerOnScenarioStart);

            Debug.Log(
                $"[SessionGameplayGate] Gameplay unlocked for {session.PlayerName} (ID: {session.PlayerID}).",
                this);
        }

        private PlayerSession GetActiveOrPendingSession()
        {
            if (sessionManager?.CurrentSession != null && sessionManager.CurrentSession.IsActive)
                return sessionManager.CurrentSession;

            return pendingSession != null && pendingSession.IsActive ? pendingSession : null;
        }

        private string[] GetEffectiveOverlaySceneNames()
        {
            if (sessionOverlaySceneNames != null && sessionOverlaySceneNames.Length > 0)
                return sessionOverlaySceneNames;

            return new[] { "FireModule_Office" };
        }

        private bool IsOverlayScene(Scene scene)
        {
            string[] allowedNames = GetEffectiveOverlaySceneNames();
            string sceneName = scene.name;

            for (int i = 0; i < allowedNames.Length; i++)
            {
                string allowed = allowedNames[i];
                if (!string.IsNullOrEmpty(allowed) && sceneName == allowed)
                    return true;
            }

            return false;
        }

        private void ApplyOverlayPresentationState()
        {
            if (gameplayUnlocked)
                return;

            ApplySessionMenuCursor();
            FreezeMovementIfConfigured();
        }

        private void ApplySessionMenuCursor()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void FreezeMovementIfConfigured()
        {
            if (!freezeMovementWhileOverlayVisible || movementFrozen)
                return;

            Transform root = ResolvePlayerRoot();
            if (root != null)
                movementLookFreeze.Freeze(root);

            movementFrozen = true;
        }

        private void RestoreMovementAfterOverlay()
        {
            if (!movementFrozen)
                return;

            movementLookFreeze.Restore();
            movementFrozen = false;
        }

        private void ApplyGameplayCursorIfNeeded()
        {
            if (ServiceLocator.TryGet(out InputManager inputManager))
            {
                if (FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR)
                    inputManager.EnsureVrGameplayInputEnabled();
                else
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                    inputManager.EnsurePcGameplayInputEnabled();
                    inputManager.EnsureVrGameplayInputEnabled();
                    ActivatePcLocomotionIfNeeded();
                }
            }
            else if (!FirePlatformRuntime.IsSourceInitialized || !FirePlatformRuntime.IsVR)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
                ActivatePcLocomotionIfNeeded();
            }
        }

        private static void ResyncGameplaySoapEvents()
        {
            if (!ServiceLocator.TryGet(out InputManager inputManager))
                return;

            inputManager.SyncPcPlayerSoapEvents();
            inputManager.SyncVrInteractSoapEvents();
            inputManager.SyncVrGripSoapEvents();
        }

        private static void ActivatePcLocomotionIfNeeded()
        {
            if (FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR)
                return;

            PlayerController[] players = FindObjectsByType<PlayerController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < players.Length; i++)
            {
                PlayerController player = players[i];
                if (player != null && player.gameObject.activeInHierarchy)
                    player.ActivatePcLocomotion();
            }
        }

        private Transform ResolvePlayerRoot()
        {
            if (playerRoot != null)
                return playerRoot;

            if (!findPlayerByTagIfMissing || string.IsNullOrEmpty(playerTag))
                return null;

            GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
            return tagged != null ? tagged.transform : null;
        }

        private void HandleOverlaySceneLoaded()
        {
            BindSessionManager();

            if (gameplayUnlocked || revealRoutineRunning || overlayContentShown)
                ResetGateStateOnly();

            PrepareSessionAfterOverlaySceneLoad();
            ScheduleOverlayRefresh();
        }

        private void BindSessionManager()
        {
            if (sessionManager != null)
                return;

            sessionManager = SessionManager.Instance;
            if (sessionManager == null)
                sessionManager = FindFirstObjectByType<SessionManager>();
        }

        private void PrepareSessionAfterOverlaySceneLoad()
        {
            BindSessionManager();
            if (sessionManager == null)
                return;

#if UNITY_EDITOR
            sessionManager.PrepareForOverlaySceneReload();
#else
            if (sessionManager.CurrentSession != null && sessionManager.CurrentSession.IsActive)
                sessionManager.ReNotifySessionReady();
            else
                sessionManager.PrepareForOverlaySceneReload();
#endif
        }

        private void ResetGateStateOnly()
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }

            StopWaitRoutine();

            gameplayUnlocked = false;
            revealRoutineRunning = false;
            pendingSession = null;
            overlayContentShown = false;
            overlayGateActive = false;
            scenarioBootstrapper = null;

            ResolveSessionUiRoot();
            BindSessionUiComponents();
            RestoreMovementAfterOverlay();
            RestoreVrSessionInputState();
            HideOverlayImmediate();
        }

        /// <summary>
        /// Resets gate state so the session profile panel can appear again (e.g. after "Tekrar Başla").
        /// </summary>
        public void ResetForNewSession()
        {
            ResetGateStateOnly();
            ScheduleOverlayRefresh();
        }
    }
}
