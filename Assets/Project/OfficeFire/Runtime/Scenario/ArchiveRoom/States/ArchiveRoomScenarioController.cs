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

        [Header("Archive — state machine")]
        [SerializeField]
        private ArchiveRoomStateChangedEvent onArchiveStateChanged = new ArchiveRoomStateChangedEvent();

        [Header("Archive — debug")]
        [SerializeField]
        private bool enableFireExtinguishDebugLogs = true;

        private ScenarioStateMachine<ArchiveRoomState> _stateMachine;
        private Coroutine _smokeNoticeDelayRoutine;
        private Coroutine _smokeNoticeReminderRoutine;
        private bool _isWaitingForNoticeSmokeAction;

        public override OfficeFireScenarioId ScenarioId => OfficeFireScenarioId.ArchiveRoom;

        public ArchiveRoomState CurrentState => _stateMachine != null ? _stateMachine.CurrentStateId : ArchiveRoomState.None;

        private void Awake()
        {
            EnsureFireExtinguishBridge();
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
            if (_stateMachine != null)
            {
                _stateMachine.StateChanged -= HandleArchiveStateChanged;
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
        /// True when the scenario accepts a successful <see cref="Actions.UseExtinguisher"/> action.
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
                case ArchiveRoomState.WaitingForExtinguisherUse:
                    reason = "Evet — alarm basildi, yangin sondurulebilir.";
                    return true;
                case ArchiveRoomState.Intervention:
                    reason = "Hayir — once alarm butonuna bas (E).";
                    return false;
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

            FireSource source = FindFirstObjectByType<FireSource>(FindObjectsInactive.Include);
            if (source == null)
            {
                return;
            }

            FireExtinguishPrerequisiteGate gate = source.GetComponent<FireExtinguishPrerequisiteGate>();
            if (gate == null)
            {
                gate = source.gameObject.AddComponent<FireExtinguishPrerequisiteGate>();
                gate.ConfigureForManualOnly();
            }

            gate.ForceAllowExtinguisher();
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

        public override void StartScenario()
        {
            base.StartScenario();
            BeginIntroThenSmokeNoticePhase();
        }

        public override void NotifyDeselected()
        {
            CancelSmokeNoticeDelay();
            CancelSmokeNoticeReminder();
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

            _stateMachine.HandleAction(actionId);
        }

        protected override void ResetRuntimeState()
        {
            CancelSmokeNoticeDelay();
            CancelSmokeNoticeReminder();
            StopEvacuationNpcs();
            base.ResetRuntimeState();
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
                        _archive.LogFireExtinguishStatus("Sondurucu alindi ama alarm henuz basilmadi — HATA");
                        _archive.RegisterMistake(OfficeFireMistakeId.UsedExtinguisherBeforeAlarm);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchivePressAlarmInstruction);
                        break;
                    case Actions.UseExtinguisher:
                        _archive.LogFireExtinguishStatus("Sondurucu kullanildi ama alarm henuz basilmadi — HATA");
                        _archive.RegisterMistake(OfficeFireMistakeId.UsedExtinguisherBeforeAlarm);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchivePressAlarmInstruction);
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
                        _archive.LogFireExtinguishStatus("Sondurucu basarili — yangin kontrol altina alindi");
                        _archive.MarkFireControlled();
                        _archive.RegisterCorrectAction(OfficeFireCorrectActionId.UsedExtinguisherCorrectly);
                        _archive.RegisterCorrectAction(OfficeFireCorrectActionId.ControlledArchiveFire);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveFireControlled);
                        _archive.InvokeFireControlled();
                        _archive.ChangeState(ArchiveRoomState.WaitingForExitRoom);
                        break;
                    case Actions.UseWater:
                        _archive.RegisterMistake(OfficeFireMistakeId.UsedWaterOnElectricalFire);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveWaterMistake);
                        _archive.InvokeWaterMistake();
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
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.ExitArchiveRoom:
                        _archive.ChangeState(ArchiveRoomState.WaitingForAssemblyArea);
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
                _archive.PlayAnnouncement(OfficeFireVoiceLineId.EvacuationInstruction);
                _archive.InvokeEvacuationStarted();
                _archive.StartEvacuationNpcs();
            }

            public override void Exit()
            {
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
