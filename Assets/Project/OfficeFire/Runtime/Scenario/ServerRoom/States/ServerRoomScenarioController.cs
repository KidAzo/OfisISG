using System.Collections;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    public class ServerRoomScenarioController : OfficeFireScenarioController
    {
        public static class Actions
        {
            public const string ReachedExitDoor = "reached_exit_door";
            public const string NoticeSmoke = "notice_smoke";
            public const string OpenServerDoor = "open_server_door";
            public const string EnterServerRoom = "enter_server_room";
            public const string UseWater = "use_water";
            public const string PressAlarm = "press_alarm";
            public const string PressSuppressionButton = "press_suppression_button";
            public const string GrabExtinguisher = "grab_extinguisher";
            public const string GrabBlanket = "grab_blanket";
            public const string UseExtinguisher = "use_extinguisher";
            public const string UseBlanket = "use_blanket";
            public const string LeaveServerRoom = "leave_server_room";
            public const string ReachAssemblyArea = "reach_assembly_area";
            public const string PlayerLeaned = "player_leaned";
            public const string ElevatorProximity = "elevator_proximity";
            public const string FireGrowth = "fire_growth";
            public const string ReachedAssemblyAreaDoor = "reached_assembly_area_door";
        }

        [Header("Server — hooks")]
        [SerializeField]
        private UnityEvent onIntroPhaseStarted = new UnityEvent();

        [SerializeField]
        private UnityEvent onSmokeNoticed = new UnityEvent();

        [SerializeField]
        private UnityEvent onDoorOpened = new UnityEvent();

        [SerializeField]
        private UnityEvent onWaterMistake = new UnityEvent();

        [SerializeField]
        private UnityEvent onSuppressionActivated = new UnityEvent();

        [SerializeField]
        private UnityEvent onFireControlled = new UnityEvent();

        [SerializeField]
        private UnityEvent onBlanketUsed = new UnityEvent();

        [SerializeField]
        private UnityEvent onEvacuationStarted = new UnityEvent();

        [Header("Server — timing")]
        [SerializeField]
        [Min(0f)]
        private float delayBeforeSmokeNoticeSeconds = 3f;

        [Header("Server — smoke notice reminders")]
        [Tooltip("After entering WaitingForSmokeNotice, wait this long before the first reminder if NoticeSmoke was not performed.")]
        [SerializeField]
        [Min(0f)]
        private float delayBeforeSmokeNoticeReminderSeconds = 30f;

        [Tooltip("Seconds between reminders while still waiting for NoticeSmoke.")]
        [SerializeField]
        [Min(0.1f)]
        private float smokeNoticeReminderIntervalSeconds = 15f;

        [SerializeField]
        private OfficeFireVoiceLineId smokeNoticeReminderVoiceLine = OfficeFireVoiceLineId.ArchiveIncidentDetected;

        [Header("Server — evacuation NPCs")]
        [SerializeField]
        private EvacuationNpcDirector evacuationNpcDirector;

        [Header("Server — outdoor assembly")]
        [Tooltip("SceneLoader SceneGroup GroupName loaded when ReachedAssemblyAreaDoor fires.")]
        [SerializeField]
        private string outdoorSceneGroupName = "OutDoor";

        [Header("Server — door tracking")]
        [Tooltip("ColorDoor (4) server room door. Auto-resolved at runtime when empty.")]
        [SerializeField]
        private SelectableDoor serverRoomDoor;

        private const string ServerRoomDoorObjectName = "ColorDoor (4)";

        [SerializeField]
        [Min(0f)]
        [Tooltip("Fade to black before OutDoor loads. Does not affect OutDoor reveal — set that on OutDoor AssemblySceneController.")]
        private float outdoorFadeToBlackSeconds = 0.45f;

        [Header("Server — state machine")]
        [SerializeField]
        private ServerRoomStateChangedEvent onServerStateChanged = new ServerRoomStateChangedEvent();

        [Header("Server — fire growth")]
        [SerializeField]
        private ScenarioFireGrowthController fireGrowthController;

        [Tooltip("Seconds between fire growth reminders while in WaitingForExitRoom after all growth stages complete.")]
        [SerializeField]
        [Min(0.1f)]
        private float fireGrowthReminderIntervalSeconds = 15f;

        [Header("Server — assembly area reminders")]
        [Tooltip("EvacuationInstruction loops in WaitingForAssemblyArea until ReachedExitDoor trigger fires.")]
        [SerializeField]
        [Min(0.1f)]
        private float assemblyAreaReminderIntervalSeconds = 15f;

        [SerializeField]
        private OfficeFireVoiceLineId assemblyAreaReminderVoiceLine = OfficeFireVoiceLineId.EvacuationInstruction;

        [Header("Server — debug")]
        [SerializeField]
        private bool enableFireExtinguishDebugLogs = true;

        private ScenarioStateMachine<ServerRoomState> _stateMachine;
        private Coroutine _smokeNoticeDelayRoutine;
        private Coroutine _smokeNoticeReminderRoutine;
        private Coroutine _fireGrowthReminderRoutine;
        private Coroutine _assemblyAreaReminderRoutine;
        private bool _hasReachedExitDoor;
        private bool _outdoorSceneLoadRequested;
        private bool _isWaitingForNoticeSmokeAction;
        private bool _fireGrowthCompleted;
        private bool _alarmPressed;

        public override OfficeFireScenarioId ScenarioId => OfficeFireScenarioId.ServerRoom;

        public ServerRoomState CurrentState => _stateMachine != null ? _stateMachine.CurrentStateId : ServerRoomState.None;

        private void Awake()
        {
            EnsureFireExtinguishBridge();
            EnsureExtinguisherHudBridge();
            DisableLegacySuppressionActions();

            _stateMachine = new ScenarioStateMachine<ServerRoomState>();
            _stateMachine.RegisterState(new ServerNoneState(this));
            _stateMachine.RegisterState(new ServerWaitingForSmokeNoticeState(this));
            _stateMachine.RegisterState(new ServerWaitingForDoorOpenState(this));
            _stateMachine.RegisterState(new ServerInterventionState(this));
            _stateMachine.RegisterState(new ServerWaitingForExtinguisherUseState(this));
            _stateMachine.RegisterState(new ServerWaitingForExitRoomState(this));
            _stateMachine.RegisterState(new ServerWaitingForAssemblyAreaState(this));
            _stateMachine.RegisterState(new ServerCompletedState(this));
            _stateMachine.StateChanged += HandleServerStateChanged;
        }

        private void EnsureFireExtinguishBridge()
        {
            if (GetComponent<OfficeFireServerFireExtinguishBridge>() != null)
            {
                return;
            }

            gameObject.AddComponent<OfficeFireServerFireExtinguishBridge>();
        }

        private void EnsureExtinguisherHudBridge()
        {
            if (GetComponent<OfficeFireServerExtinguisherHudBridge>() != null)
            {
                return;
            }

            gameObject.AddComponent<OfficeFireServerExtinguisherHudBridge>();
        }

        private static void DisableLegacySuppressionActions()
        {
            SelectableScenarioAction[] actions = FindObjectsByType<SelectableScenarioAction>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < actions.Length; i++)
            {
                SelectableScenarioAction action = actions[i];
                if (action == null || action.ActionId != Actions.PressSuppressionButton)
                {
                    continue;
                }

                if (HasHoverBasedSelectableOnSameObject(action))
                {
                    continue;
                }

                action.enabled = false;
            }
        }

        private static bool HasHoverBasedSelectableOnSameObject(SelectableScenarioAction action)
        {
            MonoBehaviour[] behaviours = action.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour == action)
                {
                    continue;
                }

                if (behaviour is IHoverable && behaviour is ISelectable)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            CancelSmokeNoticeDelay();
            CancelSmokeNoticeReminder();
            CancelFireGrowthReminderLoop();
            CancelAssemblyAreaReminderLoop();
            if (_stateMachine != null)
            {
                _stateMachine.StateChanged -= HandleServerStateChanged;
            }

            ScenarioFireGrowthController growth = fireGrowthController;
            if (growth == null && ScenarioRoot != null)
            {
                growth = ScenarioRoot.GetComponentInChildren<ScenarioFireGrowthController>(true);
            }

            if (growth != null)
            {
                growth.AllStagesCompleted -= HandleFireGrowthCompleted;
            }
        }

        public void ChangeState(ServerRoomState nextState)
        {
            if (_stateMachine == null)
            {
                Debug.LogError("[ServerRoomScenarioController] State machine not initialized.", this);
                return;
            }

            _stateMachine.ChangeState(nextState);
        }

        public void LogWaterIgnoredFireNotAccessible()
        {
            Debug.LogWarning(
                "[ServerRoomScenarioController] UseWater ignored: fire is not accessible from this state yet.",
                this);
        }

        public bool CanExtinguishFire()
        {
            return CanExtinguishFire(out _);
        }

        public bool CanExtinguishFire(out string reason)
        {
            if (!CanProcessActions())
            {
                reason = "Senaryo aktif degil veya tamamlandi.";
                return false;
            }

            switch (CurrentState)
            {
                case ServerRoomState.Intervention:
                case ServerRoomState.WaitingForExtinguisherUse:
                    reason = "Evet — yangin sondurulebilir.";
                    return true;
                case ServerRoomState.WaitingForExitRoom:
                case ServerRoomState.WaitingForAssemblyArea:
                case ServerRoomState.Completed:
                    reason = "Hayir — yangin zaten kontrol altinda veya tahliye asamasindasin.";
                    return false;
                default:
                    reason = $"Hayir — su anki durum: {CurrentState}.";
                    return false;
            }
        }

        public void LogFireExtinguishStatus(string context)
        {
            if (!enableFireExtinguishDebugLogs)
            {
                return;
            }

            CanExtinguishFire(out string reason);
            Debug.Log(
                $"[ServerRoomScenarioController][FireExtinguish] {context} | state={CurrentState} | canExtinguish={reason}",
                this);
        }

        private static bool IsExtinguisherRelatedAction(string actionId)
        {
            return actionId == Actions.UseExtinguisher || actionId == Actions.GrabExtinguisher;
        }

        public void AllowExtinguisherSpray()
        {
            OfficeFireServerFireExtinguishBridge bridge = GetComponent<OfficeFireServerFireExtinguishBridge>();
            if (bridge != null)
            {
                bridge.AllowExtinguisherSpray();
                return;
            }

            RemoveSuppressionGatesFromFireSource();
        }

        private static void RemoveSuppressionGatesFromFireSource()
        {
            FireSource source = FindFirstObjectByType<FireSource>(FindObjectsInactive.Include);
            if (source == null)
            {
                return;
            }

            FireExtinguishPrerequisiteGate[] gates = source.GetComponents<FireExtinguishPrerequisiteGate>();
            for (int i = 0; i < gates.Length; i++)
            {
                if (gates[i] != null)
                {
                    Destroy(gates[i]);
                }
            }
        }

        public void NotifyFireFullyExtinguished()
        {
            if (CurrentState != ServerRoomState.Intervention &&
                CurrentState != ServerRoomState.WaitingForExtinguisherUse)
            {
                if (enableFireExtinguishDebugLogs)
                {
                    Debug.LogWarning(
                        $"[ServerRoomScenarioController] Fire fully extinguished ignored — state={CurrentState}.",
                        this);
                }

                return;
            }

            LogFireExtinguishStatus("Yangin tamamen sonduruldu — tahliye asamasina geciliyor");
            MarkFireControlled();
            RegisterCorrectAction(OfficeFireCorrectActionId.UsedExtinguisherCorrectly);
            RegisterCorrectAction(OfficeFireCorrectActionId.ControlledServerFire);
            PlayAnnouncement(OfficeFireVoiceLineId.ArchiveFireControlled);
            InvokeFireControlled();
            ChangeState(ServerRoomState.WaitingForExitRoom);
        }

        public void BeginServerFireGrowth()
        {
            ScenarioFireGrowthController growth = ResolveFireGrowthController();
            if (growth == null)
            {
                if (enableFireExtinguishDebugLogs)
                {
                    Debug.LogWarning(
                        "[ServerRoomScenarioController] Fire growth controller not found — growth skipped.",
                        this);
                }

                return;
            }

            growth.AllStagesCompleted -= HandleFireGrowthCompleted;
            growth.AllStagesCompleted += HandleFireGrowthCompleted;
            growth.BeginGrowth();
        }

        private void HandleFireGrowthCompleted()
        {
            _fireGrowthCompleted = true;

            if (CurrentState == ServerRoomState.WaitingForAssemblyArea ||
                CurrentState == ServerRoomState.Completed)
            {
                return;
            }

            if (CurrentState == ServerRoomState.WaitingForExitRoom)
            {
                BeginFireGrowthReminderLoop();
                return;
            }

            Debug.Log(
                "[ServerRoomScenarioController] Fire growth completed — dispatching fire_growth.",
                this);
            HandleAction(Actions.FireGrowth);
        }

        public void BeginFireGrowthReminderLoop()
        {
            if (!_fireGrowthCompleted || CurrentState != ServerRoomState.WaitingForExitRoom)
            {
                return;
            }

            CancelFireGrowthReminderLoop();
            _fireGrowthReminderRoutine = StartCoroutine(FireGrowthReminderRoutine());
        }

        public void CancelFireGrowthReminderLoop()
        {
            if (_fireGrowthReminderRoutine == null)
            {
                return;
            }

            StopCoroutine(_fireGrowthReminderRoutine);
            _fireGrowthReminderRoutine = null;
        }

        private IEnumerator FireGrowthReminderRoutine()
        {
            while (CanProcessActions()
                   && CurrentState == ServerRoomState.WaitingForExitRoom
                   && _fireGrowthCompleted)
            {
                PlayAnnouncement(OfficeFireVoiceLineId.ServerFireGrowth);
                yield return new WaitForSeconds(fireGrowthReminderIntervalSeconds);
            }
        }

        public void BeginAssemblyAreaReminderLoop()
        {
            if (CurrentState != ServerRoomState.WaitingForAssemblyArea || _hasReachedExitDoor)
            {
                return;
            }

            CancelAssemblyAreaReminderLoop();
            _assemblyAreaReminderRoutine = StartCoroutine(AssemblyAreaReminderRoutine());
        }

        public void CancelAssemblyAreaReminderLoop()
        {
            if (_assemblyAreaReminderRoutine == null)
            {
                return;
            }

            StopCoroutine(_assemblyAreaReminderRoutine);
            _assemblyAreaReminderRoutine = null;
        }

        public void NotifyReachedExitDoor()
        {
            if (_hasReachedExitDoor)
            {
                return;
            }

            _hasReachedExitDoor = true;
            CancelAssemblyAreaReminderLoop();
            RegisterCorrectAction(OfficeFireCorrectActionId.ReachedExitDoor);
        }

        public void HandleReachedExitDoor()
        {
            NotifyReachedExitDoor();
            PlayAnnouncement(OfficeFireVoiceLineId.ReachedExitDoor);
        }

        public void HandleReachedAssemblyAreaDoor()
        {
            MarkEvacuated();
            RegisterCorrectAction(OfficeFireCorrectActionId.ReachedAssemblyArea);
            LoadOutdoorAssemblyScene();
        }

        private IEnumerator AssemblyAreaReminderRoutine()
        {
            while (CanProcessActions()
                   && CurrentState == ServerRoomState.WaitingForAssemblyArea
                   && !_hasReachedExitDoor)
            {
                if (assemblyAreaReminderVoiceLine != OfficeFireVoiceLineId.None)
                {
                    PlayAnnouncement(assemblyAreaReminderVoiceLine);
                }

                yield return new WaitForSeconds(assemblyAreaReminderIntervalSeconds);
            }
        }

        private ScenarioFireGrowthController ResolveFireGrowthController()
        {
            if (fireGrowthController != null)
            {
                return fireGrowthController;
            }

            fireGrowthController = GetComponent<ScenarioFireGrowthController>();
            if (fireGrowthController != null)
            {
                return fireGrowthController;
            }

            if (ScenarioRoot == null)
            {
                return null;
            }

            fireGrowthController = ScenarioRoot.GetComponentInChildren<ScenarioFireGrowthController>(true);
            return fireGrowthController;
        }

        public void InvokeIntroPhaseStarted()
        {
            onIntroPhaseStarted?.Invoke();
        }

        public void InvokeSmokeNoticed()
        {
            onSmokeNoticed?.Invoke();
        }

        public void InvokeDoorOpened()
        {
            onDoorOpened?.Invoke();
        }

        public void InvokeWaterMistake()
        {
            onWaterMistake?.Invoke();
        }

        public void InvokeSuppressionActivated()
        {
            onSuppressionActivated?.Invoke();
        }

        public void InvokeFireControlled()
        {
            onFireControlled?.Invoke();
        }

        public void InvokeBlanketUsed()
        {
            onBlanketUsed?.Invoke();
        }

        public void HandleBlanketGrabbed()
        {
            RegisterCorrectAction(OfficeFireCorrectActionId.SelectedFireBlanket);
            SetObjective(OfficeFireObjectiveId.PlaceFireBlanket);
        }

        public void HandleBlanketUsed()
        {
            RegisterCorrectAction(OfficeFireCorrectActionId.PlacedFireBlanketCorrectly);
            SetObjective(OfficeFireObjectiveId.UseServerFireBlanket);
            MarkFireControlled();
            InvokeBlanketUsed();
            InvokeFireControlled();
        }

        public void InvokeEvacuationStarted()
        {
            onEvacuationStarted?.Invoke();
        }

        public void StartEvacuationNpcs()
        {
            if (evacuationNpcDirector == null)
            {
                return;
            }

            evacuationNpcDirector.StartEvacuation();
        }

        public void StopEvacuationNpcs()
        {
            if (evacuationNpcDirector == null)
            {
                return;
            }

            evacuationNpcDirector.StopEvacuation();
        }

        public void LoadOutdoorAssemblyScene()
        {
            if (_outdoorSceneLoadRequested)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(outdoorSceneGroupName))
            {
                Debug.LogWarning("[ServerRoomScenarioController] Outdoor scene group name is not assigned.", this);
                return;
            }

            _outdoorSceneLoadRequested = true;
            CancelAssemblyAreaReminderLoop();
            StopEvacuationNpcs();

            if (!IsCompleted)
            {
                RecordServerRoomDoorEndState();
                CompleteScenario();
            }

            OfficeFireScenarioReportHolder.Stash(Report);

            Debug.Log(
                $"[ServerRoomScenarioController] Loading outdoor scene group '{outdoorSceneGroupName.Trim()}'.",
                this);
            AssemblySceneController.LoadAssemblyScene(
                outdoorSceneGroupName.Trim(),
                outdoorFadeToBlackSeconds,
                0.45f);
        }

        private void EnsureServerRoomDoorResolved()
        {
            if (serverRoomDoor != null)
            {
                return;
            }

            SelectableDoor[] doors = FindObjectsByType<SelectableDoor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < doors.Length; i++)
            {
                SelectableDoor door = doors[i];
                if (door != null && door.gameObject.name == ServerRoomDoorObjectName)
                {
                    serverRoomDoor = door;
                    return;
                }
            }
        }

        private void RecordServerRoomDoorEndState()
        {
            EnsureServerRoomDoorResolved();
            if (serverRoomDoor == null)
            {
                Debug.LogWarning(
                    "[ServerRoomScenarioController] Server room door not found for end-state report.",
                    this);
                return;
            }

            OfficeFireScenarioReport report = Report;
            if (report == null)
            {
                return;
            }

            report.hasServerRoomDoorEndState = true;
            report.serverRoomDoorClosedAtEnd = !serverRoomDoor.IsOpen;
        }

        public override void StartScenario()
        {
            base.StartScenario();
            BeginIntroThenSmokeNoticePhase();
        }

        public override void NotifyDeselected()
        {
            CancelSmokeNoticeDelay();
            CancelSmokeNoticeReminder();
            CancelFireGrowthReminderLoop();
            CancelAssemblyAreaReminderLoop();
            StopEvacuationNpcs();
            base.NotifyDeselected();
            if (_stateMachine != null)
            {
                _stateMachine.SnapTo(ServerRoomState.None);
            }
        }

        public override void HandleAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
            {
                return;
            }

            if (enableFireExtinguishDebugLogs && IsExtinguisherRelatedAction(actionId))
            {
                LogFireExtinguishStatus($"HandleAction('{actionId}') geldi");
            }

            if (!CanProcessActions())
            {
                if (enableFireExtinguishDebugLogs && IsExtinguisherRelatedAction(actionId))
                {
                    Debug.LogWarning(
                        $"[ServerRoomScenarioController][FireExtinguish] '{actionId}' reddedildi: senaryo aktif degil.",
                        this);
                }

                return;
            }

            if (_stateMachine == null)
            {
                return;
            }

            if (actionId == Actions.ReachedExitDoor)
            {
                HandleReachedExitDoor();
                return;
            }

            if (actionId == Actions.ReachedAssemblyAreaDoor)
            {
                HandleReachedAssemblyAreaDoor();
                return;
            }

            if (actionId == Actions.PressAlarm)
            {
                RegisterCorrectAction(OfficeFireCorrectActionId.PressedAlarm);
                return;
            }

            if (actionId == Actions.GrabBlanket)
            {
                HandleBlanketGrabbed();
                return;
            }

            if (actionId == Actions.UseBlanket)
            {
                HandleBlanketUsed();
                return;
            }

            _stateMachine.HandleAction(actionId);
        }

        protected override void ResetRuntimeState()
        {
            CancelSmokeNoticeDelay();
            CancelSmokeNoticeReminder();
            CancelFireGrowthReminderLoop();
            CancelAssemblyAreaReminderLoop();
            StopEvacuationNpcs();
            base.ResetRuntimeState();
            _fireGrowthCompleted = false;
            _hasReachedExitDoor = false;
            _outdoorSceneLoadRequested = false;
            _alarmPressed = false;
            if (_stateMachine != null)
            {
                _stateMachine.SnapTo(ServerRoomState.None);
            }
        }

        private void BeginIntroThenSmokeNoticePhase()
        {
            CancelSmokeNoticeDelay();
            EnterNoneState();

            if (delayBeforeSmokeNoticeSeconds <= 0f)
            {
                ChangeState(ServerRoomState.WaitingForSmokeNotice);
                return;
            }

            _smokeNoticeDelayRoutine = StartCoroutine(SmokeNoticeDelayRoutine());
        }

        private void EnterNoneState()
        {
            if (_stateMachine == null)
            {
                return;
            }

            if (_stateMachine.CurrentStateId == ServerRoomState.None && _stateMachine.CurrentState != null)
            {
                _stateMachine.CurrentState.Enter();
                return;
            }

            ChangeState(ServerRoomState.None);
        }

        private IEnumerator SmokeNoticeDelayRoutine()
        {
            yield return new WaitForSeconds(delayBeforeSmokeNoticeSeconds);
            _smokeNoticeDelayRoutine = null;

            if (!CanProcessActions())
            {
                yield break;
            }

            if (CurrentState != ServerRoomState.None)
            {
                yield break;
            }

            ChangeState(ServerRoomState.WaitingForSmokeNotice);
        }

        private void CancelSmokeNoticeDelay()
        {
            if (_smokeNoticeDelayRoutine == null)
            {
                return;
            }

            StopCoroutine(_smokeNoticeDelayRoutine);
            _smokeNoticeDelayRoutine = null;
        }

        public bool IsWaitingForNoticeSmokeAction()
        {
            return _isWaitingForNoticeSmokeAction;
        }

        public void BeginSmokeNoticeReminder()
        {
            CancelSmokeNoticeReminder();
            _isWaitingForNoticeSmokeAction = true;
            _smokeNoticeReminderRoutine = StartCoroutine(SmokeNoticeReminderRoutine());
        }

        public void CancelSmokeNoticeReminder()
        {
            _isWaitingForNoticeSmokeAction = false;

            if (_smokeNoticeReminderRoutine == null)
            {
                return;
            }

            StopCoroutine(_smokeNoticeReminderRoutine);
            _smokeNoticeReminderRoutine = null;
        }

        public void RemindNoticeSmoke()
        {
            if (!_isWaitingForNoticeSmokeAction)
            {
                return;
            }

            if (smokeNoticeReminderVoiceLine != OfficeFireVoiceLineId.None)
            {
                PlayAnnouncement(smokeNoticeReminderVoiceLine);
            }

            Debug.Log("[ServerRoomScenarioController] NoticeSmoke reminder.", this);
        }

        private IEnumerator SmokeNoticeReminderRoutine()
        {
            if (delayBeforeSmokeNoticeReminderSeconds > 0f)
            {
                yield return new WaitForSeconds(delayBeforeSmokeNoticeReminderSeconds);
            }

            while (_isWaitingForNoticeSmokeAction)
            {
                if (!CanProcessActions() || CurrentState != ServerRoomState.WaitingForSmokeNotice)
                {
                    yield break;
                }

                if (!_isWaitingForNoticeSmokeAction)
                {
                    yield break;
                }

                RemindNoticeSmoke();
                yield return new WaitForSeconds(smokeNoticeReminderIntervalSeconds);
            }
        }

        private void HandleServerStateChanged(ServerRoomState previous, ServerRoomState next)
        {
            Debug.Log($"[ServerRoomScenarioController] State {previous} -> {next}.", this);
            onServerStateChanged?.Invoke(next);
        }

        private sealed class ServerNoneState : ScenarioStateBase<ServerRoomState>
        {
            private readonly ServerRoomScenarioController _server;

            public ServerNoneState(ServerRoomScenarioController controller)
                : base(controller)
            {
                _server = controller;
            }

            public override ServerRoomState StateId => ServerRoomState.None;

            public override void Enter()
            {
                _server.InvokeIntroPhaseStarted();
            }

            public override void HandleAction(string actionId)
            {
            }
        }

        private sealed class ServerWaitingForSmokeNoticeState : ScenarioStateBase<ServerRoomState>
        {
            private readonly ServerRoomScenarioController _server;

            public ServerWaitingForSmokeNoticeState(ServerRoomScenarioController controller)
                : base(controller)
            {
                _server = controller;
            }

            public override ServerRoomState StateId => ServerRoomState.WaitingForSmokeNotice;

            public override void Enter()
            {
                _server.SetObjective(OfficeFireObjectiveId.CheckServerRoom);
                _server.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveIncidentDetected);
                _server.BeginSmokeNoticeReminder();
            }

            public override void Exit()
            {
                _server.CancelSmokeNoticeReminder();
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.NoticeSmoke:
                        if (!_server.IsWaitingForNoticeSmokeAction())
                        {
                            LogUnknownAction(actionId);
                            return;
                        }

                        _server.CancelSmokeNoticeReminder();
                        _server.MarkReactionIfNeeded();
                        _server.RegisterCorrectAction(OfficeFireCorrectActionId.NoticedSmoke);
                        _server.PlayAnnouncement(OfficeFireVoiceLineId.SmokeWarning);
                        _server.InvokeSmokeNoticed();
                        _server.ChangeState(ServerRoomState.WaitingForDoorOpen);
                        break;
                    case Actions.EnterServerRoom:
                        _server.RegisterCorrectAction(OfficeFireCorrectActionId.EnteredServerRoomSafely);
                        _server.PlayAnnouncement(OfficeFireVoiceLineId.ServerRoomEntered);
                        _server.InvokeDoorOpened();
                        _server.ChangeState(ServerRoomState.Intervention);
                        break;
                    case Actions.PressSuppressionButton:
                        _server._alarmPressed = true;
                        _server.RegisterCorrectAction(OfficeFireCorrectActionId.ActivatedSuppressionSystem);
                        _server.AllowExtinguisherSpray();
                        _server.InvokeSuppressionActivated();
                        _server.InvokeEvacuationStarted();
                        _server.StartEvacuationNpcs();
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ServerWaitingForDoorOpenState : ScenarioStateBase<ServerRoomState>
        {
            private readonly ServerRoomScenarioController _server;

            public ServerWaitingForDoorOpenState(ServerRoomScenarioController controller)
                : base(controller)
            {
                _server = controller;
            }

            public override ServerRoomState StateId => ServerRoomState.WaitingForDoorOpen;

            public override void Enter()
            {
                _server.SetObjective(OfficeFireObjectiveId.CheckServerRoom);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.PressSuppressionButton:
                        _server._alarmPressed = true;
                        _server.RegisterCorrectAction(OfficeFireCorrectActionId.ActivatedSuppressionSystem);
                        _server.AllowExtinguisherSpray();
                        _server.InvokeSuppressionActivated();
                        _server.InvokeEvacuationStarted();
                        _server.StartEvacuationNpcs();
                        break;
                    case Actions.EnterServerRoom:
                        _server.RegisterCorrectAction(OfficeFireCorrectActionId.EnteredServerRoomSafely);
                        _server.PlayAnnouncement(OfficeFireVoiceLineId.LeanCorrectly);
                        _server.InvokeDoorOpened();
                        _server.ChangeState(ServerRoomState.Intervention);
                        break;
                    case Actions.UseWater:
                        _server.LogWaterIgnoredFireNotAccessible();
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ServerInterventionState : ScenarioStateBase<ServerRoomState>
        {
            private readonly ServerRoomScenarioController _server;

            public ServerInterventionState(ServerRoomScenarioController controller)
                : base(controller)
            {
                _server = controller;
            }

            public override ServerRoomState StateId => ServerRoomState.Intervention;

            public override void Enter()
            {
                _server.SetObjective(OfficeFireObjectiveId.ActivateServerSuppression);
                _server.PlayAnnouncement(OfficeFireVoiceLineId.ArchivePressAlarmInstruction);
                _server.AllowExtinguisherSpray();
                _server.BeginServerFireGrowth();
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.PressSuppressionButton:
                        _server._alarmPressed = true;
                        _server.RegisterCorrectAction(OfficeFireCorrectActionId.ActivatedSuppressionSystem);
                        _server.AllowExtinguisherSpray();
                        _server.InvokeSuppressionActivated();
                        _server.LogFireExtinguishStatus("Baski dusurme aktif — sondurucu asamasina geciliyor");
                        _server.ChangeState(ServerRoomState.WaitingForExtinguisherUse);
                        _server.InvokeEvacuationStarted();
                        _server.StartEvacuationNpcs();
                        break;
                    case Actions.PlayerLeaned:
                        _server.RegisterCorrectAction(OfficeFireCorrectActionId.LeanedCorrectly);
                        break;
                    case Actions.UseWater:
                        _server.RegisterMistake(OfficeFireMistakeId.UsedWaterOnServerFire);
                        _server.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveWaterMistake);
                        _server.InvokeWaterMistake();
                        break;
                    case Actions.GrabExtinguisher:
                        _server.PlayAnnouncement(OfficeFireVoiceLineId.EstinguisherHandled);
                        break;
                    case Actions.UseExtinguisher:
                        _server.LogFireExtinguishStatus("Sondurme basladi — EstinguishingStarted anonsu");
                        _server.PlayAnnouncement(OfficeFireVoiceLineId.EstinguishingStarted);
                        break;
                    case Actions.FireGrowth:
                        _server.RegisterStoodInSmokeIfNotLeaned();
                        _server.PlayAnnouncement(OfficeFireVoiceLineId.ServerFireGrowth);
                        _server.ChangeState(ServerRoomState.WaitingForExitRoom);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ServerWaitingForExtinguisherUseState : ScenarioStateBase<ServerRoomState>
        {
            private readonly ServerRoomScenarioController _server;

            public ServerWaitingForExtinguisherUseState(ServerRoomScenarioController controller)
                : base(controller)
            {
                _server = controller;
            }

            public override ServerRoomState StateId => ServerRoomState.WaitingForExtinguisherUse;

            public override void Enter()
            {
                _server.AllowExtinguisherSpray();
                _server.SetObjective(OfficeFireObjectiveId.LeaveServerRoom);
                _server.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveUseExtinguisherInstruction);
                _server.LogFireExtinguishStatus("Baski dusurme sonrasi asama — yangin sondurulebilir");
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                   case Actions.PressSuppressionButton:
                        _server._alarmPressed = true;
                        _server.RegisterCorrectAction(OfficeFireCorrectActionId.ActivatedSuppressionSystem);
                        _server.AllowExtinguisherSpray();
                        _server.InvokeSuppressionActivated();
                        _server.InvokeEvacuationStarted();
                        _server.StartEvacuationNpcs();
                        break;       
                    case Actions.UseExtinguisher:
                        _server.LogFireExtinguishStatus("Sondurme basladi — EstinguishingStarted anonsu");
                        _server.PlayAnnouncement(OfficeFireVoiceLineId.EstinguishingStarted);
                        break;
                    case Actions.UseWater:
                        _server.RegisterMistake(OfficeFireMistakeId.UsedWaterOnServerFire);
                        _server.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveWaterMistake);
                        _server.InvokeWaterMistake();
                        break;
                    case Actions.GrabExtinguisher:
                        _server.PlayAnnouncement(OfficeFireVoiceLineId.EstinguisherHandled);
                        break;
                    case Actions.FireGrowth:
                        _server.RegisterStoodInSmokeIfNotLeaned();
                        _server.PlayAnnouncement(OfficeFireVoiceLineId.ServerFireGrowth);
                        _server.ChangeState(ServerRoomState.WaitingForExitRoom);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ServerWaitingForExitRoomState : ScenarioStateBase<ServerRoomState>
        {
            private readonly ServerRoomScenarioController _server;

            public ServerWaitingForExitRoomState(ServerRoomScenarioController controller)
                : base(controller)
            {
                _server = controller;
            }

            public override ServerRoomState StateId => ServerRoomState.WaitingForExitRoom;

            public override void Enter()
            {
                _server.SetObjective(OfficeFireObjectiveId.LeaveServerRoom);
                _server.BeginFireGrowthReminderLoop();
            }

            public override void Exit()
            {
                _server.CancelFireGrowthReminderLoop();
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.PressSuppressionButton:
                        _server._alarmPressed = true;
                        _server.RegisterCorrectAction(OfficeFireCorrectActionId.ActivatedSuppressionSystem);
                        _server.AllowExtinguisherSpray();
                        _server.InvokeSuppressionActivated();
                        if (_server._alarmPressed)
                        {
                            _server.InvokeEvacuationStarted();
                            _server.StartEvacuationNpcs();
                        }
                        break;
                    case Actions.LeaveServerRoom:
                        _server.RegisterCorrectAction(OfficeFireCorrectActionId.LeftServerRoomBeforeGas);
                        _server.PlayAnnouncement(OfficeFireVoiceLineId.ExittedArchiveRoom);
                        _server.ChangeState(ServerRoomState.WaitingForAssemblyArea);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ServerWaitingForAssemblyAreaState : ScenarioStateBase<ServerRoomState>
        {
            private readonly ServerRoomScenarioController _server;

            public ServerWaitingForAssemblyAreaState(ServerRoomScenarioController controller)
                : base(controller)
            {
                _server = controller;
            }

            public override ServerRoomState StateId => ServerRoomState.WaitingForAssemblyArea;

            public override void Enter()
            {
                _server.SetObjective(OfficeFireObjectiveId.GoToAssemblyArea);
                _server.BeginAssemblyAreaReminderLoop();
            }

            public override void Exit()
            {
                _server.CancelAssemblyAreaReminderLoop();
                _server.StopEvacuationNpcs();
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.PressSuppressionButton:
                        _server._alarmPressed = true;
                        _server.RegisterCorrectAction(OfficeFireCorrectActionId.ActivatedSuppressionSystem);
                        _server.AllowExtinguisherSpray();
                        _server.InvokeSuppressionActivated();
                        if (_server._alarmPressed)
                        {
                            _server.InvokeEvacuationStarted();
                            _server.StartEvacuationNpcs();
                        }
                        break;
                    case Actions.LeaveServerRoom:
                        break;
                    case Actions.ReachAssemblyArea:
                        _server.HandleReachedAssemblyAreaDoor();
                        break;
                    case Actions.ElevatorProximity:
                        _server.PlayAnnouncement(OfficeFireVoiceLineId.DoNotUseElevator);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ServerCompletedState : ScenarioStateBase<ServerRoomState>
        {
            private readonly ServerRoomScenarioController _owner;

            public ServerCompletedState(ServerRoomScenarioController controller)
                : base(controller)
            {
                _owner = controller;
            }

            public override ServerRoomState StateId => ServerRoomState.Completed;

            public override void Enter()
            {
                _owner.CompleteScenario();
            }

            public override void HandleAction(string actionId)
            {
            }
        }
    }
}
