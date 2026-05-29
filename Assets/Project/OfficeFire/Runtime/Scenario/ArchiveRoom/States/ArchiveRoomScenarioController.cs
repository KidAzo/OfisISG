using System.Collections;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    public class ArchiveRoomScenarioController : OfficeFireScenarioController
    {
        public static class Actions
        {
            public const string ReachedExitDoor = "reached_exit_door";
            public const string NoticeSmoke = "notice_smoke";
            public const string OpenArchiveDoor = "open_archive_door";
            public const string EnteredArchiveRoom = "entered_archive_room";
            public const string UseWater = "use_water";
            public const string PressAlarm = "press_alarm";
            public const string GrabExtinguisher = "grab_extinguisher";
            public const string PullPowerPlug = "pull_power_plug";
            public const string UseExtinguisher = "use_extinguisher";
            public const string ExitArchiveRoom = "exit_archive_room";
            public const string ReachAssemblyArea = "reach_assembly_area";
            public const string PlayerLeaned = "player_leaned";
            public const string ElevatorProximity = "elevator_proximity";
            public const string FireGrowth = "fire_growth";
            public const string ReachedAssemblyAreaDoor = "reached_assembly_area_door";
        }

        [Header("Archive — hooks")]
        [SerializeField]
        private UnityEvent onIntroPhaseStarted = new UnityEvent();

        [SerializeField]
        private UnityEvent onSmokeNoticed = new UnityEvent();

        [SerializeField]
        private UnityEvent onDoorOpened = new UnityEvent();

        [SerializeField]
        private UnityEvent onWaterMistake = new UnityEvent();

        [SerializeField]
        private UnityEvent onAlarmActivated = new UnityEvent();

        [SerializeField]
        private UnityEvent onPowerCut = new UnityEvent();

        [SerializeField]
        private UnityEvent onFireControlled = new UnityEvent();

        [SerializeField]
        private UnityEvent onEvacuationStarted = new UnityEvent();

        [Header("Archive — timing")]
        [SerializeField]
        [Min(0f)]
        private float delayBeforeSmokeNoticeSeconds = 3f;

        [Header("Archive — smoke notice reminders")]
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

        [Header("Archive — evacuation NPCs")]
        [SerializeField]
        private EvacuationNpcDirector evacuationNpcDirector;

        [Header("Archive — outdoor assembly")]
        [Tooltip("SceneLoader SceneGroup GroupName loaded when ReachedAssemblyAreaDoor fires.")]
        [SerializeField]
        private string outdoorSceneGroupName = "OutDoor";

        [Header("Archive — state machine")]
        [SerializeField]
        private ArchiveRoomStateChangedEvent onArchiveStateChanged = new ArchiveRoomStateChangedEvent();

        [Header("Archive — fire growth")]
        [SerializeField]
        private ArchiveRoomFireGrowthController fireGrowthController;

        [Tooltip("Seconds between ArchiveFireGrowth reminders while in WaitingForExitRoom after all growth stages complete.")]
        [SerializeField]
        [Min(0.1f)]
        private float fireGrowthReminderIntervalSeconds = 15f;

        [Header("Archive — assembly area reminders")]
        [Tooltip("EvacuationInstruction loops in WaitingForAssemblyArea until ReachedExitDoor trigger fires.")]
        [SerializeField]
        [Min(0.1f)]
        private float assemblyAreaReminderIntervalSeconds = 15f;

        [SerializeField]
        private OfficeFireVoiceLineId assemblyAreaReminderVoiceLine = OfficeFireVoiceLineId.EvacuationInstruction;

        [Header("Archive — debug")]
        [SerializeField]
        private bool enableFireExtinguishDebugLogs = true;

        private ScenarioStateMachine<ArchiveRoomState> _stateMachine;
        private Coroutine _smokeNoticeDelayRoutine;
        private Coroutine _smokeNoticeReminderRoutine;
        private Coroutine _fireGrowthReminderRoutine;
        private Coroutine _assemblyAreaReminderRoutine;
        private bool _hasReachedExitDoor;
        private bool _outdoorSceneLoadRequested;
        private bool _isWaitingForNoticeSmokeAction;
        private bool _extinguishingStarted;
        private bool _fireGrowthCompleted;

        public override OfficeFireScenarioId ScenarioId => OfficeFireScenarioId.ArchiveRoom;

        public ArchiveRoomState CurrentState => _stateMachine != null ? _stateMachine.CurrentStateId : ArchiveRoomState.None;

        private void Awake()
        {
            EnsureFireExtinguishBridge();
            EnsureExtinguisherHudBridge();
            DisableLegacyAlarmActions();

            _stateMachine = new ScenarioStateMachine<ArchiveRoomState>();
            _stateMachine.RegisterState(new ArchiveNoneState(this));
            _stateMachine.RegisterState(new ArchiveWaitingForSmokeNoticeState(this));
            _stateMachine.RegisterState(new ArchiveWaitingForDoorOpenState(this));
            _stateMachine.RegisterState(new ArchiveInterventionState(this));
            _stateMachine.RegisterState(new ArchiveWaitingForExtinguisherUseState(this));
            _stateMachine.RegisterState(new ArchiveWaitingForExitRoomState(this));
            _stateMachine.RegisterState(new ArchiveWaitingForAssemblyAreaState(this));
            _stateMachine.RegisterState(new ArchiveCompletedState(this));
            _stateMachine.StateChanged += HandleArchiveStateChanged;
        }

        private void EnsureFireExtinguishBridge()
        {
            if (GetComponent<OfficeFireArchiveFireExtinguishBridge>() != null)
            {
                return;
            }

            gameObject.AddComponent<OfficeFireArchiveFireExtinguishBridge>();
        }

        private void EnsureExtinguisherHudBridge()
        {
            if (GetComponent<OfficeFireArchiveExtinguisherHudBridge>() != null)
            {
                return;
            }

            gameObject.AddComponent<OfficeFireArchiveExtinguisherHudBridge>();
        }

        private static void DisableLegacyAlarmActions()
        {
            SelectableScenarioAction[] actions = FindObjectsByType<SelectableScenarioAction>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < actions.Length; i++)
            {
                SelectableScenarioAction action = actions[i];
                if (action == null || action.ActionId != Actions.PressAlarm)
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
                _stateMachine.StateChanged -= HandleArchiveStateChanged;
            }

            ArchiveRoomFireGrowthController growth = fireGrowthController;
            if (growth == null && ScenarioRoot != null)
            {
                growth = ScenarioRoot.GetComponentInChildren<ArchiveRoomFireGrowthController>(true);
            }

            if (growth != null)
            {
                growth.AllStagesCompleted -= HandleFireGrowthCompleted;
            }
        }

        public void ChangeState(ArchiveRoomState nextState)
        {
            if (_stateMachine == null)
            {
                Debug.LogError("[ArchiveRoomScenarioController] State machine not initialized.", this);
                return;
            }

            _stateMachine.ChangeState(nextState);
        }

        /// <summary>
        /// Water is not a gameplay mistake until the fire is actually accessible (e.g. door open / alarm states).
        /// </summary>
        public void LogWaterIgnoredFireNotAccessible()
        {
            Debug.LogWarning(
                "[ArchiveRoomScenarioController] UseWater ignored: fire is not accessible from this state yet.",
                this);
        }

        /// <summary>
        /// True when the scenario accepts extinguisher success (e.g. full extinguish notification).
        /// </summary>
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
                case ArchiveRoomState.Intervention:
                case ArchiveRoomState.WaitingForExtinguisherUse:
                    reason = "Evet — yangin sondurulebilir.";
                    return true;
                case ArchiveRoomState.WaitingForExitRoom:
                case ArchiveRoomState.WaitingForAssemblyArea:
                case ArchiveRoomState.Completed:
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
                $"[ArchiveRoomScenarioController][FireExtinguish] {context} | state={CurrentState} | canExtinguish={reason}",
                this);
        }

        private static bool IsExtinguisherRelatedAction(string actionId)
        {
            return actionId == Actions.UseExtinguisher || actionId == Actions.GrabExtinguisher;
        }

        public void AllowExtinguisherSpray()
        {
            OfficeFireArchiveFireExtinguishBridge bridge = GetComponent<OfficeFireArchiveFireExtinguishBridge>();
            if (bridge != null)
            {
                bridge.AllowExtinguisherSpray();
                return;
            }

            RemoveArchiveAlarmGatesFromFireSource();
        }

        private static void RemoveArchiveAlarmGatesFromFireSource()
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

        /// <summary>
        /// Called when archive fire is fully extinguished during intervention or extinguisher use.
        /// </summary>
        public void NotifyFireFullyExtinguished()
        {
            if (CurrentState != ArchiveRoomState.Intervention &&
                CurrentState != ArchiveRoomState.WaitingForExtinguisherUse)
            {
                if (enableFireExtinguishDebugLogs)
                {
                    Debug.LogWarning(
                        $"[ArchiveRoomScenarioController] Fire fully extinguished ignored — state={CurrentState}.",
                        this);
                }

                return;
            }

            LogFireExtinguishStatus("Yangin tamamen sonduruldu — tahliye asamasina geciliyor");
            MarkFireControlled();
            RegisterCorrectAction(OfficeFireCorrectActionId.UsedExtinguisherCorrectly);
            RegisterCorrectAction(OfficeFireCorrectActionId.ControlledArchiveFire);
            PlayAnnouncement(OfficeFireVoiceLineId.ArchiveFireControlled);
            InvokeFireControlled();
            ChangeState(ArchiveRoomState.WaitingForExitRoom);
        }

        public void BeginArchiveFireGrowth()
        {
            ArchiveRoomFireGrowthController growth = ResolveFireGrowthController();
            if (growth == null)
            {
                if (enableFireExtinguishDebugLogs)
                {
                    Debug.LogWarning(
                        "[ArchiveRoomScenarioController] Fire growth controller not found — growth skipped.",
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

            if (CurrentState == ArchiveRoomState.WaitingForAssemblyArea ||
                CurrentState == ArchiveRoomState.Completed)
            {
                return;
            }

            if (CurrentState == ArchiveRoomState.WaitingForExitRoom)
            {
                BeginFireGrowthReminderLoop();
                return;
            }

            Debug.Log(
                "[ArchiveRoomScenarioController] Fire growth completed — dispatching fire_growth.",
                this);
            HandleAction(Actions.FireGrowth);
        }

        public void BeginFireGrowthReminderLoop()
        {
            if (!_fireGrowthCompleted || CurrentState != ArchiveRoomState.WaitingForExitRoom)
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
                   && CurrentState == ArchiveRoomState.WaitingForExitRoom
                   && _fireGrowthCompleted)
            {
                PlayAnnouncement(OfficeFireVoiceLineId.ArchiveFireGrowth);
                yield return new WaitForSeconds(fireGrowthReminderIntervalSeconds);
            }
        }

        public void BeginAssemblyAreaReminderLoop()
        {
            if (CurrentState != ArchiveRoomState.WaitingForAssemblyArea || _hasReachedExitDoor)
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
                   && CurrentState == ArchiveRoomState.WaitingForAssemblyArea
                   && !_hasReachedExitDoor)
            {
                if (assemblyAreaReminderVoiceLine != OfficeFireVoiceLineId.None)
                {
                    PlayAnnouncement(assemblyAreaReminderVoiceLine);
                }

                yield return new WaitForSeconds(assemblyAreaReminderIntervalSeconds);
            }
        }

        private ArchiveRoomFireGrowthController ResolveFireGrowthController()
        {
            if (fireGrowthController != null)
            {
                return fireGrowthController;
            }

            fireGrowthController = GetComponent<ArchiveRoomFireGrowthController>();
            if (fireGrowthController != null)
            {
                return fireGrowthController;
            }

            if (ScenarioRoot == null)
            {
                return null;
            }

            fireGrowthController = ScenarioRoot.GetComponentInChildren<ArchiveRoomFireGrowthController>(true);
            return fireGrowthController;
        }

        public void InvokeIntroPhaseStarted()
        {
            if (onIntroPhaseStarted != null)
            {
                onIntroPhaseStarted.Invoke();
            }
        }

        public void InvokeSmokeNoticed()
        {
            if (onSmokeNoticed != null)
            {
                onSmokeNoticed.Invoke();
            }
        }

        public void InvokeDoorOpened()
        {
            if (onDoorOpened != null)
            {
                onDoorOpened.Invoke();
            }
        }

        public void InvokeWaterMistake()
        {
            if (onWaterMistake != null)
            {
                onWaterMistake.Invoke();
            }
        }

        public void InvokeAlarmActivated()
        {
            if (onAlarmActivated != null)
            {
                onAlarmActivated.Invoke();
            }
        }

        public void InvokePowerCut()
        {
            if (onPowerCut != null)
            {
                onPowerCut.Invoke();
            }
        }

        public void InvokeFireControlled()
        {
            if (onFireControlled != null)
            {
                onFireControlled.Invoke();
            }
        }

        public void InvokeEvacuationStarted()
        {
            if (onEvacuationStarted != null)
            {
                onEvacuationStarted.Invoke();
            }
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
                Debug.LogWarning("[ArchiveRoomScenarioController] Outdoor scene group name is not assigned.", this);
                return;
            }

            _outdoorSceneLoadRequested = true;
            CancelAssemblyAreaReminderLoop();
            StopEvacuationNpcs();

            if (!IsCompleted)
            {
                CompleteScenario();
            }

            OfficeFireScenarioReportHolder.Stash(Report);

            Debug.Log(
                $"[ArchiveRoomScenarioController] Loading outdoor scene group '{outdoorSceneGroupName.Trim()}'.",
                this);
            AssemblySceneController.LoadAssemblyScene(outdoorSceneGroupName.Trim());
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
                _stateMachine.SnapTo(ArchiveRoomState.None);
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
                        $"[ArchiveRoomScenarioController][FireExtinguish] '{actionId}' reddedildi: senaryo aktif degil.",
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
            _extinguishingStarted = false;
            _fireGrowthCompleted = false;
            _hasReachedExitDoor = false;
            _outdoorSceneLoadRequested = false;
            if (_stateMachine != null)
            {
                _stateMachine.SnapTo(ArchiveRoomState.None);
            }
        }

        /// <summary>
        /// Play intro at <see cref="ArchiveRoomState.None"/>, then after <see cref="delayBeforeSmokeNoticeSeconds"/>
        /// enter <see cref="ArchiveRoomState.WaitingForSmokeNotice"/> (objective + first announcement).
        /// </summary>
        private void BeginIntroThenSmokeNoticePhase()
        {
            CancelSmokeNoticeDelay();
            EnterNoneState();

            if (delayBeforeSmokeNoticeSeconds <= 0f)
            {
                ChangeState(ArchiveRoomState.WaitingForSmokeNotice);
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

            if (_stateMachine.CurrentStateId == ArchiveRoomState.None && _stateMachine.CurrentState != null)
            {
                _stateMachine.CurrentState.Enter();
                return;
            }

            ChangeState(ArchiveRoomState.None);
        }

        private IEnumerator SmokeNoticeDelayRoutine()
        {
            yield return new WaitForSeconds(delayBeforeSmokeNoticeSeconds);
            _smokeNoticeDelayRoutine = null;

            if (!CanProcessActions())
            {
                yield break;
            }

            if (CurrentState != ArchiveRoomState.None)
            {
                yield break;
            }

            ChangeState(ArchiveRoomState.WaitingForSmokeNotice);
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

        /// <summary>
        /// True while <see cref="ArchiveRoomState.WaitingForSmokeNotice"/> is active and
        /// <see cref="Actions.NoticeSmoke"/> has not been handled yet.
        /// </summary>
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

            Debug.Log("[ArchiveRoomScenarioController] NoticeSmoke reminder.", this);
        }

        private IEnumerator SmokeNoticeReminderRoutine()
        {
            if (delayBeforeSmokeNoticeReminderSeconds > 0f)
            {
                yield return new WaitForSeconds(delayBeforeSmokeNoticeReminderSeconds);
            }

            while (_isWaitingForNoticeSmokeAction)
            {
                if (!CanProcessActions() || CurrentState != ArchiveRoomState.WaitingForSmokeNotice)
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

        private void HandleArchiveStateChanged(ArchiveRoomState previous, ArchiveRoomState next)
        {
            Debug.Log($"[ArchiveRoomScenarioController] State {previous} -> {next}.", this);
            if (onArchiveStateChanged != null)
            {
                onArchiveStateChanged.Invoke(next);
            }
        }

        private sealed class ArchiveNoneState : ScenarioStateBase<ArchiveRoomState>
        {
            private readonly ArchiveRoomScenarioController _archive;

            public ArchiveNoneState(ArchiveRoomScenarioController controller)
                : base(controller)
            {
                _archive = controller;
            }

            public override ArchiveRoomState StateId => ArchiveRoomState.None;

            public override void Enter()
            {
                _archive.InvokeIntroPhaseStarted();
            }

            public override void HandleAction(string actionId)
            {
            }
        }

        private sealed class ArchiveWaitingForSmokeNoticeState : ScenarioStateBase<ArchiveRoomState>
        {
            private readonly ArchiveRoomScenarioController _archive;

            public ArchiveWaitingForSmokeNoticeState(ArchiveRoomScenarioController controller)
                : base(controller)
            {
                _archive = controller;
            }

            public override ArchiveRoomState StateId => ArchiveRoomState.WaitingForSmokeNotice;

            public override void Enter()
            {
                _archive.SetObjective(OfficeFireObjectiveId.CheckArchiveRoom);
                _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveIncidentDetected);
                _archive.BeginSmokeNoticeReminder();
            }

            public override void Exit()
            {
                _archive.CancelSmokeNoticeReminder();
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.NoticeSmoke:
                        if (!_archive.IsWaitingForNoticeSmokeAction())
                        {
                            LogUnknownAction(actionId);
                            return;
                        }

                        _archive.CancelSmokeNoticeReminder();
                        _archive.MarkReactionIfNeeded();
                        _archive.RegisterCorrectAction(OfficeFireCorrectActionId.NoticedSmoke);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.SmokeWarning);
                        _archive.InvokeSmokeNoticed();
                        _archive.ChangeState(ArchiveRoomState.WaitingForDoorOpen);
                        break;
                    case Actions.EnteredArchiveRoom:
                        _archive.RegisterCorrectAction(OfficeFireCorrectActionId.OpenedArchiveDoor);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveElectricalFireWarning);
                        _archive.InvokeDoorOpened();
                        _archive.ChangeState(ArchiveRoomState.Intervention);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ArchiveWaitingForDoorOpenState : ScenarioStateBase<ArchiveRoomState>
        {
            private readonly ArchiveRoomScenarioController _archive;

            public ArchiveWaitingForDoorOpenState(ArchiveRoomScenarioController controller)
                : base(controller)
            {
                _archive = controller;
            }

            public override ArchiveRoomState StateId => ArchiveRoomState.WaitingForDoorOpen;

            public override void Enter()
            {
                _archive.SetObjective(OfficeFireObjectiveId.CheckArchiveRoom);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.EnteredArchiveRoom:
                        _archive.RegisterCorrectAction(OfficeFireCorrectActionId.OpenedArchiveDoor);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.LeanCorrectly);
                        _archive.InvokeDoorOpened();
                        _archive.ChangeState(ArchiveRoomState.Intervention);
                        break;
                    case Actions.UseWater:
                        _archive.LogWaterIgnoredFireNotAccessible();
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ArchiveInterventionState : ScenarioStateBase<ArchiveRoomState>
        {
            private readonly ArchiveRoomScenarioController _archive;
            bool _isLeaned = false;

            public ArchiveInterventionState(ArchiveRoomScenarioController controller)
                : base(controller)
            {
                _archive = controller;
            }

            public override ArchiveRoomState StateId => ArchiveRoomState.Intervention;

            public override void Enter()
            {
                _archive.SetObjective(OfficeFireObjectiveId.PressArchiveAlarm);
                _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchivePressAlarmInstruction);
                _archive.AllowExtinguisherSpray();
                _archive.BeginArchiveFireGrowth();
                NotLeaned();
            }

            void NotLeaned()
            {
               //Do something
            }

            void Leaned()
            {
                //Do something
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.PressAlarm:
                        _archive.RegisterCorrectAction(OfficeFireCorrectActionId.PressedAlarm);
                        _archive.AllowExtinguisherSpray();
                        _archive.InvokeAlarmActivated();
                        _archive.LogFireExtinguishStatus("Alarm basildi — sondurucu asamasina geciliyor");
                        _archive.ChangeState(ArchiveRoomState.WaitingForExtinguisherUse);
                        break;
                    case Actions.PlayerLeaned:
                        _archive.RegisterCorrectAction(OfficeFireCorrectActionId.LeanedCorrectly);
                        Leaned();
                        break;
                    case Actions.UseWater:
                        _archive.RegisterMistake(OfficeFireMistakeId.UsedWaterOnElectricalFire);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveWaterMistake);
                        _archive.InvokeWaterMistake();
                        break;
                    case Actions.GrabExtinguisher:
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.EstinguisherHandled);
                        break;
                    case Actions.UseExtinguisher:
                        _archive.LogFireExtinguishStatus("Sondurme basladi — EstinguishingStarted anonsu");
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.EstinguishingStarted);
                        break;
                    case Actions.FireGrowth:
                        _archive.ChangeState(ArchiveRoomState.WaitingForExitRoom);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ArchiveWaitingForExtinguisherUseState : ScenarioStateBase<ArchiveRoomState>
        {
            private readonly ArchiveRoomScenarioController _archive;

            public ArchiveWaitingForExtinguisherUseState(ArchiveRoomScenarioController controller)
                : base(controller)
            {
                _archive = controller;
            }

            public override ArchiveRoomState StateId => ArchiveRoomState.WaitingForExtinguisherUse;

            public override void Enter()
            {
                _archive.AllowExtinguisherSpray();
                _archive.SetObjective(OfficeFireObjectiveId.UseArchiveExtinguisher);
                _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveUseExtinguisherInstruction);
                _archive.LogFireExtinguishStatus("Sondurucu kullanim asamasi basladi — yangin sondurulebilir");
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.UseExtinguisher:
                        _archive.LogFireExtinguishStatus("Sondurme basladi — EstinguishingStarted anonsu");
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.EstinguishingStarted);
                        break;
                    case Actions.UseWater:
                        _archive.RegisterMistake(OfficeFireMistakeId.UsedWaterOnElectricalFire);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveWaterMistake);
                        _archive.InvokeWaterMistake();
                        break;
                    case Actions.GrabExtinguisher:
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.EstinguisherHandled);
                        break;
                    case Actions.FireGrowth:
                        _archive.ChangeState(ArchiveRoomState.WaitingForExitRoom);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ArchiveWaitingForExitRoomState : ScenarioStateBase<ArchiveRoomState>
        {
            private readonly ArchiveRoomScenarioController _archive;

            public ArchiveWaitingForExitRoomState(ArchiveRoomScenarioController controller)
                : base(controller)
            {
                _archive = controller;
            }

            public override ArchiveRoomState StateId => ArchiveRoomState.WaitingForExitRoom;

            public override void Enter()
            {
                _archive.SetObjective(OfficeFireObjectiveId.ExitArchiveRoom);
                _archive.BeginFireGrowthReminderLoop();
            }

            public override void Exit()
            {
                _archive.CancelFireGrowthReminderLoop();
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.ExitArchiveRoom:
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ExittedArchiveRoom);
                        _archive.InvokeEvacuationStarted();
                        _archive.StartEvacuationNpcs();
                        _archive.ChangeState(ArchiveRoomState.WaitingForAssemblyArea);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ArchiveWaitingForAssemblyAreaState : ScenarioStateBase<ArchiveRoomState>
        {
            private readonly ArchiveRoomScenarioController _archive;

            public ArchiveWaitingForAssemblyAreaState(ArchiveRoomScenarioController controller)
                : base(controller)
            {
                _archive = controller;
            }

            public override ArchiveRoomState StateId => ArchiveRoomState.WaitingForAssemblyArea;

            public override void Enter()
            {
                _archive.SetObjective(OfficeFireObjectiveId.GoToAssemblyArea);
                _archive.BeginAssemblyAreaReminderLoop();
                _archive.InvokeEvacuationStarted();
                _archive.StartEvacuationNpcs();
            }

            public override void Exit()
            {
                _archive.CancelAssemblyAreaReminderLoop();
                _archive.StopEvacuationNpcs();
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.ReachAssemblyArea:
                        _archive.MarkEvacuated();
                        _archive.RegisterCorrectAction(OfficeFireCorrectActionId.ReachedAssemblyArea);
                        _archive.ChangeState(ArchiveRoomState.Completed);
                        break;
                    case Actions.ElevatorProximity:
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.DoNotUseElevator);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ArchiveCompletedState : ScenarioStateBase<ArchiveRoomState>
        {
            private readonly ArchiveRoomScenarioController _owner;

            public ArchiveCompletedState(ArchiveRoomScenarioController controller)
                : base(controller)
            {
                _owner = controller;
            }

            public override ArchiveRoomState StateId => ArchiveRoomState.Completed;

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
