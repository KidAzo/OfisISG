using System.Collections;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    public class KitchenCafeScenarioController : OfficeFireScenarioController
    {
        public static class Actions
        {
            public const string ReachedExitDoor = "reached_exit_door";
            public const string NoticeSmoke = "notice_smoke";
            public const string OpenKitchenDoor = "open_kitchen_door";
            public const string EnterKitchenCafe = "enter_kitchen_cafe";
            public const string UseWater = "use_water";
            public const string UseBlanket = "use_blanket";
            public const string PressSuppressionButton = "press_suppression_button";
            public const string GrabExtinguisher = "grab_extinguisher";
            public const string UseExtinguisher = "use_extinguisher";
            public const string LeaveKitchenCafe = "leave_kitchen_cafe";
            public const string ReachAssemblyArea = "reach_assembly_area";
            public const string PlayerLeaned = "player_leaned";
            public const string ElevatorProximity = "elevator_proximity";
            public const string FireGrowth = "fire_growth";
            public const string ReachedAssemblyAreaDoor = "reached_assembly_area_door";
        }

        [Header("Kitchen — hooks")]
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
        private UnityEvent onEvacuationStarted = new UnityEvent();

        [Header("Kitchen — timing")]
        [SerializeField]
        [Min(0f)]
        private float delayBeforeSmokeNoticeSeconds = 3f;

        [Header("Kitchen — smoke notice reminders")]
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

        [Header("Kitchen — evacuation NPCs")]
        [SerializeField]
        private EvacuationNpcDirector evacuationNpcDirector;

        [Header("Kitchen — outdoor assembly")]
        [Tooltip("SceneLoader SceneGroup GroupName loaded when ReachedAssemblyAreaDoor fires.")]
        [SerializeField]
        private string outdoorSceneGroupName = "OutDoor";

        [SerializeField]
        [Min(0f)]
        [Tooltip("Fade to black before OutDoor loads. Does not affect OutDoor reveal — set that on OutDoor AssemblySceneController.")]
        private float outdoorFadeToBlackSeconds = 0.45f;

        [Header("Kitchen — state machine")]
        [SerializeField]
        private KitchenCafeStateChangedEvent onKitchenStateChanged = new KitchenCafeStateChangedEvent();

        [Header("Kitchen — fire growth")]
        [SerializeField]
        private ScenarioFireGrowthController fireGrowthController;

        [Tooltip("Seconds between fire growth reminders while in WaitingForExitRoom after all growth stages complete.")]
        [SerializeField]
        [Min(0.1f)]
        private float fireGrowthReminderIntervalSeconds = 15f;

        [Header("Kitchen — assembly area reminders")]
        [Tooltip("EvacuationInstruction loops in WaitingForAssemblyArea until ReachedExitDoor trigger fires.")]
        [SerializeField]
        [Min(0.1f)]
        private float assemblyAreaReminderIntervalSeconds = 15f;

        [SerializeField]
        private OfficeFireVoiceLineId assemblyAreaReminderVoiceLine = OfficeFireVoiceLineId.EvacuationInstruction;

        [Header("Kitchen — debug")]
        [SerializeField]
        private bool enableFireExtinguishDebugLogs = true;

        private ScenarioStateMachine<KitchenCafeState> _stateMachine;
        private Coroutine _smokeNoticeDelayRoutine;
        private Coroutine _smokeNoticeReminderRoutine;
        private Coroutine _fireGrowthReminderRoutine;
        private Coroutine _assemblyAreaReminderRoutine;
        private bool _hasReachedExitDoor;
        private bool _outdoorSceneLoadRequested;
        private bool _isWaitingForNoticeSmokeAction;
        private bool _fireGrowthCompleted;
        private bool _alarmPressed;

        public override OfficeFireScenarioId ScenarioId => OfficeFireScenarioId.KitchenCafe;

        public KitchenCafeState CurrentState => _stateMachine != null ? _stateMachine.CurrentStateId : KitchenCafeState.None;

        private void Awake()
        {
            EnsureFireExtinguishBridge();
            EnsureBlanketBridge();
            EnsureExtinguisherHudBridge();
            DisableLegacySuppressionActions();

            _stateMachine = new ScenarioStateMachine<KitchenCafeState>();
            _stateMachine.RegisterState(new KitchenNoneState(this));
            _stateMachine.RegisterState(new KitchenWaitingForSmokeNoticeState(this));
            _stateMachine.RegisterState(new KitchenWaitingForDoorOpenState(this));
            _stateMachine.RegisterState(new KitchenInterventionState(this));
            _stateMachine.RegisterState(new KitchenWaitingForExtinguisherUseState(this));
            _stateMachine.RegisterState(new KitchenWaitingForExitRoomState(this));
            _stateMachine.RegisterState(new KitchenWaitingForAssemblyAreaState(this));
            _stateMachine.RegisterState(new KitchenCompletedState(this));
            _stateMachine.StateChanged += HandleKitchenStateChanged;
        }

        private void EnsureFireExtinguishBridge()
        {
            if (GetComponent<OfficeFireKitchenFireExtinguishBridge>() != null)
            {
                return;
            }

            gameObject.AddComponent<OfficeFireKitchenFireExtinguishBridge>();
        }

        private void EnsureBlanketBridge()
        {
            if (GetComponent<OfficeFireKitchenBlanketScenarioBridge>() != null)
            {
                return;
            }

            gameObject.AddComponent<OfficeFireKitchenBlanketScenarioBridge>();
        }

        private void EnsureExtinguisherHudBridge()
        {
            if (GetComponent<OfficeFireKitchenExtinguisherHudBridge>() != null)
            {
                return;
            }

            gameObject.AddComponent<OfficeFireKitchenExtinguisherHudBridge>();
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
                _stateMachine.StateChanged -= HandleKitchenStateChanged;
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

        public void ChangeState(KitchenCafeState nextState)
        {
            if (_stateMachine == null)
            {
                Debug.LogError("[KitchenCafeScenarioController] State machine not initialized.", this);
                return;
            }

            _stateMachine.ChangeState(nextState);
        }

        public void LogWaterIgnoredFireNotAccessible()
        {
            Debug.LogWarning(
                "[KitchenCafeScenarioController] UseWater ignored: fire is not accessible from this state yet.",
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
                case KitchenCafeState.Intervention:
                case KitchenCafeState.WaitingForExtinguisherUse:
                    reason = "Evet — yangin sondurulebilir.";
                    return true;
                case KitchenCafeState.WaitingForExitRoom:
                case KitchenCafeState.WaitingForAssemblyArea:
                case KitchenCafeState.Completed:
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
                $"[KitchenCafeScenarioController][FireExtinguish] {context} | state={CurrentState} | canExtinguish={reason}",
                this);
        }

        private static bool IsExtinguisherRelatedAction(string actionId)
        {
            return actionId == Actions.UseExtinguisher || actionId == Actions.GrabExtinguisher;
        }

        public void AllowExtinguisherSpray()
        {
            OfficeFireKitchenFireExtinguishBridge bridge = GetComponent<OfficeFireKitchenFireExtinguishBridge>();
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
            if (CurrentState != KitchenCafeState.Intervention &&
                CurrentState != KitchenCafeState.WaitingForExtinguisherUse)
            {
                if (enableFireExtinguishDebugLogs)
                {
                    Debug.LogWarning(
                        $"[KitchenCafeScenarioController] Fire fully extinguished ignored — state={CurrentState}.",
                        this);
                }

                return;
            }

            LogFireExtinguishStatus("Yangin tamamen sonduruldu — tahliye asamasina geciliyor");
            MarkFireControlled();
            RegisterCorrectAction(OfficeFireCorrectActionId.UsedExtinguisherCorrectly);
            RegisterCorrectAction(OfficeFireCorrectActionId.ControlledKitchenFire);
            PlayAnnouncement(OfficeFireVoiceLineId.ArchiveFireControlled);
            InvokeFireControlled();
            ChangeState(KitchenCafeState.WaitingForExitRoom);
        }

        public void BeginKitchenFireGrowth()
        {
            ScenarioFireGrowthController growth = ResolveFireGrowthController();
            if (growth == null)
            {
                if (enableFireExtinguishDebugLogs)
                {
                    Debug.LogWarning(
                        "[KitchenCafeScenarioController] Fire growth controller not found — growth skipped.",
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

            if (CurrentState == KitchenCafeState.WaitingForAssemblyArea ||
                CurrentState == KitchenCafeState.Completed)
            {
                return;
            }

            if (CurrentState == KitchenCafeState.WaitingForExitRoom)
            {
                BeginFireGrowthReminderLoop();
                return;
            }

            Debug.Log(
                "[KitchenCafeScenarioController] Fire growth completed — dispatching fire_growth.",
                this);
            HandleAction(Actions.FireGrowth);
        }

        public void BeginFireGrowthReminderLoop()
        {
            if (!_fireGrowthCompleted || CurrentState != KitchenCafeState.WaitingForExitRoom)
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
                   && CurrentState == KitchenCafeState.WaitingForExitRoom
                   && _fireGrowthCompleted)
            {
                PlayAnnouncement(OfficeFireVoiceLineId.ArchiveFireGrowth);
                yield return new WaitForSeconds(fireGrowthReminderIntervalSeconds);
            }
        }

        public void BeginAssemblyAreaReminderLoop()
        {
            if (CurrentState != KitchenCafeState.WaitingForAssemblyArea || _hasReachedExitDoor)
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
                   && CurrentState == KitchenCafeState.WaitingForAssemblyArea
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

        public void HandleBlanketUsedOnFire()
        {
            if (!CanProcessActions())
            {
                return;
            }

            RegisterCorrectAction(OfficeFireCorrectActionId.SelectedFireBlanket);
            RegisterCorrectAction(OfficeFireCorrectActionId.PlacedFireBlanketCorrectly);
            RegisterCorrectAction(OfficeFireCorrectActionId.ControlledKitchenFire);
            MarkFireControlled();
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
                Debug.LogWarning("[KitchenCafeScenarioController] Outdoor scene group name is not assigned.", this);
                return;
            }

            _outdoorSceneLoadRequested = true;
            CancelAssemblyAreaReminderLoop();
            StopEvacuationNpcs();

            if (!IsCompleted)
            {
                CompleteScenario();
            }

            OfficeFireScenarioReport report = Report;
            if (report != null && report.scenarioId != ScenarioId)
            {
                report.scenarioId = ScenarioId;
            }

            OfficeFireScenarioReportHolder.Stash(report);

            Debug.Log(
                $"[KitchenCafeScenarioController] Loading outdoor scene group '{outdoorSceneGroupName.Trim()}'.",
                this);
            AssemblySceneController.LoadAssemblyScene(
                outdoorSceneGroupName.Trim(),
                outdoorFadeToBlackSeconds,
                0.45f);
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
                _stateMachine.SnapTo(KitchenCafeState.None);
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
                        $"[KitchenCafeScenarioController][FireExtinguish] '{actionId}' reddedildi: senaryo aktif degil.",
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
            _fireGrowthCompleted = false;
            _hasReachedExitDoor = false;
            _outdoorSceneLoadRequested = false;
            _alarmPressed = false;
            if (_stateMachine != null)
            {
                _stateMachine.SnapTo(KitchenCafeState.None);
            }
        }

        private void BeginIntroThenSmokeNoticePhase()
        {
            CancelSmokeNoticeDelay();
            EnterNoneState();

            if (delayBeforeSmokeNoticeSeconds <= 0f)
            {
                ChangeState(KitchenCafeState.WaitingForSmokeNotice);
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

            if (_stateMachine.CurrentStateId == KitchenCafeState.None && _stateMachine.CurrentState != null)
            {
                _stateMachine.CurrentState.Enter();
                return;
            }

            ChangeState(KitchenCafeState.None);
        }

        private IEnumerator SmokeNoticeDelayRoutine()
        {
            yield return new WaitForSeconds(delayBeforeSmokeNoticeSeconds);
            _smokeNoticeDelayRoutine = null;

            if (!CanProcessActions())
            {
                yield break;
            }

            if (CurrentState != KitchenCafeState.None)
            {
                yield break;
            }

            ChangeState(KitchenCafeState.WaitingForSmokeNotice);
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

            Debug.Log("[KitchenCafeScenarioController] NoticeSmoke reminder.", this);
        }

        private IEnumerator SmokeNoticeReminderRoutine()
        {
            if (delayBeforeSmokeNoticeReminderSeconds > 0f)
            {
                yield return new WaitForSeconds(delayBeforeSmokeNoticeReminderSeconds);
            }

            while (_isWaitingForNoticeSmokeAction)
            {
                if (!CanProcessActions() || CurrentState != KitchenCafeState.WaitingForSmokeNotice)
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

        private void HandleKitchenStateChanged(KitchenCafeState previous, KitchenCafeState next)
        {
            Debug.Log($"[KitchenCafeScenarioController] State {previous} -> {next}.", this);
            onKitchenStateChanged?.Invoke(next);
        }

        private sealed class KitchenNoneState : ScenarioStateBase<KitchenCafeState>
        {
            private readonly KitchenCafeScenarioController _kitchen;

            public KitchenNoneState(KitchenCafeScenarioController controller)
                : base(controller)
            {
                _kitchen = controller;
            }

            public override KitchenCafeState StateId => KitchenCafeState.None;

            public override void Enter()
            {
                _kitchen.InvokeIntroPhaseStarted();
            }

            public override void HandleAction(string actionId)
            {
            }
        }

        private sealed class KitchenWaitingForSmokeNoticeState : ScenarioStateBase<KitchenCafeState>
        {
            private readonly KitchenCafeScenarioController _kitchen;

            public KitchenWaitingForSmokeNoticeState(KitchenCafeScenarioController controller)
                : base(controller)
            {
                _kitchen = controller;
            }

            public override KitchenCafeState StateId => KitchenCafeState.WaitingForSmokeNotice;

            public override void Enter()
            {
                _kitchen.SetObjective(OfficeFireObjectiveId.CheckKitchenArea);
                _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveIncidentDetected);
                _kitchen.BeginSmokeNoticeReminder();
            }

            public override void Exit()
            {
                _kitchen.CancelSmokeNoticeReminder();
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.NoticeSmoke:
                        if (!_kitchen.IsWaitingForNoticeSmokeAction())
                        {
                            LogUnknownAction(actionId);
                            return;
                        }

                        _kitchen.CancelSmokeNoticeReminder();
                        _kitchen.MarkReactionIfNeeded();
                        _kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.NoticedSmoke);
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.SmokeWarning);
                        _kitchen.InvokeSmokeNoticed();
                        _kitchen.ChangeState(KitchenCafeState.WaitingForDoorOpen);
                        break;
                    case Actions.EnterKitchenCafe:
                        _kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.EnteredKitchenCafeSafely);
                        _kitchen.InvokeDoorOpened();
                        _kitchen.ChangeState(KitchenCafeState.Intervention);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class KitchenWaitingForDoorOpenState : ScenarioStateBase<KitchenCafeState>
        {
            private readonly KitchenCafeScenarioController _kitchen;

            public KitchenWaitingForDoorOpenState(KitchenCafeScenarioController controller)
                : base(controller)
            {
                _kitchen = controller;
            }

            public override KitchenCafeState StateId => KitchenCafeState.WaitingForDoorOpen;

            public override void Enter()
            {
                _kitchen.SetObjective(OfficeFireObjectiveId.CheckKitchenArea);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.EnterKitchenCafe:
                        _kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.EnteredKitchenCafeSafely);
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.LeanCorrectly);
                        _kitchen.InvokeDoorOpened();
                        _kitchen.ChangeState(KitchenCafeState.Intervention);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class KitchenInterventionState : ScenarioStateBase<KitchenCafeState>
        {
            private readonly KitchenCafeScenarioController _kitchen;

            public KitchenInterventionState(KitchenCafeScenarioController controller)
                : base(controller)
            {
                _kitchen = controller;
            }

            public override KitchenCafeState StateId => KitchenCafeState.Intervention;

            public override void Enter()
            {
                _kitchen.SetObjective(OfficeFireObjectiveId.ActivateKitchenSuppression);
                _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.ArchivePressAlarmInstruction);
                _kitchen.AllowExtinguisherSpray();
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.PressSuppressionButton:
                        _kitchen._alarmPressed = true;
                        _kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.ActivatedSuppressionSystem);
                        _kitchen.AllowExtinguisherSpray();
                        _kitchen.InvokeSuppressionActivated();
                        _kitchen.LogFireExtinguishStatus("Baski dusurme aktif — sondurucu asamasina geciliyor");
                        break;
                    case Actions.PlayerLeaned:
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.LeanCorrectly);
                        _kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.LeanedCorrectly);
                        break;
                    case Actions.UseWater:
                        _kitchen.RegisterMistake(OfficeFireMistakeId.UsedWaterOnKitchenFire);
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.CarafeUsed);
                        _kitchen.InvokeWaterMistake();
                        break;
                    case Actions.UseBlanket:
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.KitchenBlanketSuccess);
                        _kitchen.HandleBlanketUsedOnFire();
                        break;
                    case Actions.GrabExtinguisher:
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.EstinguisherHandled);
                        _kitchen.AllowExtinguisherSpray();
                        break;
                    case Actions.UseExtinguisher:
                        _kitchen.LogFireExtinguishStatus("Sondurme basladi — EstinguishingStarted anonsu");
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.EstinguishingStarted);
                        break;
                    case Actions.FireGrowth:
                        _kitchen.ChangeState(KitchenCafeState.WaitingForExitRoom);
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.KitchenFireGrowingEvacuate);
                        break;
                    case Actions.LeaveKitchenCafe:
                        _kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.LeftKitchenCafeBeforeGas);
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.ExittedArchiveRoom);
                        if (_kitchen._alarmPressed)
                        {
                            _kitchen.InvokeEvacuationStarted();
                            _kitchen.StartEvacuationNpcs();
                        }
                        _kitchen.ChangeState(KitchenCafeState.WaitingForAssemblyArea);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class KitchenWaitingForExtinguisherUseState : ScenarioStateBase<KitchenCafeState>
        {
            private readonly KitchenCafeScenarioController _kitchen;

            public KitchenWaitingForExtinguisherUseState(KitchenCafeScenarioController controller)
                : base(controller)
            {
                _kitchen = controller;
            }

            public override KitchenCafeState StateId => KitchenCafeState.WaitingForExtinguisherUse;

            public override void Enter()
            {
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.UseExtinguisher:
                        _kitchen.LogFireExtinguishStatus("Sondurme basladi — EstinguishingStarted anonsu");
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.EstinguishingStarted);
                        break;
                    case Actions.UseWater:
                        _kitchen.RegisterMistake(OfficeFireMistakeId.UsedWaterOnKitchenFire);
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.CarafeUsed);
                        _kitchen.InvokeWaterMistake();
                        break;
                    case Actions.UseBlanket:
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.KitchenBlanketSuccess);
                        _kitchen.HandleBlanketUsedOnFire();
                        break;
                    case Actions.GrabExtinguisher:
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.EstinguisherHandled);
                        break;
                    case Actions.FireGrowth:
                        _kitchen.ChangeState(KitchenCafeState.WaitingForExitRoom);
                        break;
                    case Actions.LeaveKitchenCafe:
                        _kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.LeftKitchenCafeBeforeGas);
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.ExittedArchiveRoom);
                        _kitchen.InvokeEvacuationStarted();
                        if (_kitchen._alarmPressed)
                        {
                            _kitchen.StartEvacuationNpcs();
                        }
                        _kitchen.ChangeState(KitchenCafeState.WaitingForAssemblyArea);
                        break;
                    case Actions.PlayerLeaned:
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.LeanCorrectly);
                        _kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.LeanedCorrectly);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class KitchenWaitingForExitRoomState : ScenarioStateBase<KitchenCafeState>
        {
            private readonly KitchenCafeScenarioController _kitchen;

            public KitchenWaitingForExitRoomState(KitchenCafeScenarioController controller)
                : base(controller)
            {
                _kitchen = controller;
            }

            public override KitchenCafeState StateId => KitchenCafeState.WaitingForExitRoom;

            public override void Enter()
            {
                _kitchen.SetObjective(OfficeFireObjectiveId.LeaveKitchenCafe);
                _kitchen.BeginFireGrowthReminderLoop();
            }

            public override void Exit()
            {
                _kitchen.CancelFireGrowthReminderLoop();
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.PressSuppressionButton:
                        _kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.ActivatedSuppressionSystem);
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.AlarmInstruction);
                        _kitchen.AllowExtinguisherSpray();
                        _kitchen.InvokeSuppressionActivated();
                        _kitchen.LogFireExtinguishStatus("Baski dusurme aktif — sondurucu asamasina geciliyor");
                        break;
                    case Actions.LeaveKitchenCafe:
                        _kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.LeftKitchenCafeBeforeGas);
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.ExittedArchiveRoom);
                        _kitchen.InvokeEvacuationStarted();
                        if (_kitchen._alarmPressed)
                        {
                            _kitchen.StartEvacuationNpcs();
                        }
                        _kitchen.ChangeState(KitchenCafeState.WaitingForAssemblyArea);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class KitchenWaitingForAssemblyAreaState : ScenarioStateBase<KitchenCafeState>
        {
            private readonly KitchenCafeScenarioController _kitchen;

            public KitchenWaitingForAssemblyAreaState(KitchenCafeScenarioController controller)
                : base(controller)
            {
                _kitchen = controller;
            }

            public override KitchenCafeState StateId => KitchenCafeState.WaitingForAssemblyArea;

            public override void Enter()
            {
                _kitchen.SetObjective(OfficeFireObjectiveId.GoToAssemblyArea);
                _kitchen.BeginAssemblyAreaReminderLoop();
                _kitchen.InvokeEvacuationStarted();
                if (_kitchen._alarmPressed)
                {
                    _kitchen.StartEvacuationNpcs();
                }
            }

            public override void Exit()
            {
                _kitchen.CancelAssemblyAreaReminderLoop();
                _kitchen.StopEvacuationNpcs();
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.ReachAssemblyArea:
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.ReachAssemblyArea);
                        _kitchen.HandleReachedAssemblyAreaDoor();
                        break;
                    case Actions.ElevatorProximity:
                        _kitchen.PlayAnnouncement(OfficeFireVoiceLineId.DoNotUseElevator);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class KitchenCompletedState : ScenarioStateBase<KitchenCafeState>
        {
            private readonly KitchenCafeScenarioController _owner;

            public KitchenCompletedState(KitchenCafeScenarioController controller)
                : base(controller)
            {
                _owner = controller;
            }

            public override KitchenCafeState StateId => KitchenCafeState.Completed;

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
