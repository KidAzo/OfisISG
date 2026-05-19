using System.Collections;
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
            public const string UseWater = "use_water";
            public const string PressAlarm = "press_alarm";
            public const string GrabExtinguisher = "grab_extinguisher";
            public const string PullPowerPlug = "pull_power_plug";
            public const string UseExtinguisher = "use_extinguisher";
            public const string ExitArchiveRoom = "exit_archive_room";
            public const string ReachAssemblyArea = "reach_assembly_area";
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

        [Header("Archive — state machine")]
        [SerializeField]
        private ArchiveRoomStateChangedEvent onArchiveStateChanged = new ArchiveRoomStateChangedEvent();

        private ScenarioStateMachine<ArchiveRoomState> _stateMachine;
        private Coroutine _smokeNoticeDelayRoutine;

        public override OfficeFireScenarioId ScenarioId => OfficeFireScenarioId.ArchiveRoom;

        public ArchiveRoomState CurrentState => _stateMachine != null ? _stateMachine.CurrentStateId : ArchiveRoomState.None;

        private void Awake()
        {
            _stateMachine = new ScenarioStateMachine<ArchiveRoomState>();
            _stateMachine.RegisterState(new ArchiveNoneState(this));
            _stateMachine.RegisterState(new ArchiveWaitingForSmokeNoticeState(this));
            _stateMachine.RegisterState(new ArchiveWaitingForDoorOpenState(this));
            _stateMachine.RegisterState(new ArchiveWaitingForAlarmState(this));
            _stateMachine.RegisterState(new ArchiveWaitingForPowerCutState(this));
            _stateMachine.RegisterState(new ArchiveWaitingForExtinguisherUseState(this));
            _stateMachine.RegisterState(new ArchiveWaitingForExitRoomState(this));
            _stateMachine.RegisterState(new ArchiveWaitingForAssemblyAreaState(this));
            _stateMachine.RegisterState(new ArchiveCompletedState(this));
            _stateMachine.StateChanged += HandleArchiveStateChanged;
        }

        private void OnDestroy()
        {
            CancelSmokeNoticeDelay();
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

        public override void StartScenario()
        {
            base.StartScenario();
            BeginIntroThenSmokeNoticePhase();
        }

        public override void NotifyDeselected()
        {
            CancelSmokeNoticeDelay();
            base.NotifyDeselected();
            if (_stateMachine != null)
            {
                _stateMachine.SnapTo(ArchiveRoomState.None);
            }
        }

        public override void HandleAction(string actionId)
        {
            if (!CanProcessActions() || string.IsNullOrEmpty(actionId))
            {
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
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.NoticeSmoke:
                        _archive.MarkReactionIfNeeded();
                        _archive.RegisterCorrectAction(OfficeFireCorrectActionId.NoticedSmoke);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.SmokeWarning);
                        _archive.InvokeSmokeNoticed();
                        _archive.ChangeState(ArchiveRoomState.WaitingForDoorOpen);
                        break;
                    case Actions.OpenArchiveDoor:
                        _archive.MarkReactionIfNeeded();
                        _archive.RegisterCorrectAction(OfficeFireCorrectActionId.NoticedSmoke);
                        _archive.RegisterCorrectAction(OfficeFireCorrectActionId.OpenedArchiveDoor);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveElectricalFireWarning);
                        _archive.InvokeSmokeNoticed();
                        _archive.InvokeDoorOpened();
                        _archive.ChangeState(ArchiveRoomState.WaitingForAlarm);
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
                    case Actions.OpenArchiveDoor:
                        _archive.RegisterCorrectAction(OfficeFireCorrectActionId.OpenedArchiveDoor);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveElectricalFireWarning);
                        _archive.InvokeDoorOpened();
                        _archive.ChangeState(ArchiveRoomState.WaitingForAlarm);
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

        private sealed class ArchiveWaitingForAlarmState : ScenarioStateBase<ArchiveRoomState>
        {
            private readonly ArchiveRoomScenarioController _archive;

            public ArchiveWaitingForAlarmState(ArchiveRoomScenarioController controller)
                : base(controller)
            {
                _archive = controller;
            }

            public override ArchiveRoomState StateId => ArchiveRoomState.WaitingForAlarm;

            public override void Enter()
            {
                _archive.SetObjective(OfficeFireObjectiveId.PressArchiveAlarm);
                _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchivePressAlarmInstruction);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.PressAlarm:
                        _archive.RegisterCorrectAction(OfficeFireCorrectActionId.PressedAlarm);
                        _archive.InvokeAlarmActivated();
                        _archive.ChangeState(ArchiveRoomState.WaitingForPowerCut);
                        break;
                    case Actions.UseWater:
                        _archive.RegisterMistake(OfficeFireMistakeId.UsedWaterOnElectricalFire);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveWaterMistake);
                        _archive.InvokeWaterMistake();
                        break;
                    case Actions.GrabExtinguisher:
                        _archive.RegisterMistake(OfficeFireMistakeId.UsedExtinguisherBeforeAlarm);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchivePressAlarmInstruction);
                        break;
                    case Actions.UseExtinguisher:
                        _archive.RegisterMistake(OfficeFireMistakeId.UsedExtinguisherBeforeAlarm);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchivePressAlarmInstruction);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ArchiveWaitingForPowerCutState : ScenarioStateBase<ArchiveRoomState>
        {
            private readonly ArchiveRoomScenarioController _archive;

            public ArchiveWaitingForPowerCutState(ArchiveRoomScenarioController controller)
                : base(controller)
            {
                _archive = controller;
            }

            public override ArchiveRoomState StateId => ArchiveRoomState.WaitingForPowerCut;

            public override void Enter()
            {
                _archive.SetObjective(OfficeFireObjectiveId.CutArchivePower);
                _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveCutPowerInstruction);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.PullPowerPlug:
                        _archive.RegisterCorrectAction(OfficeFireCorrectActionId.CutPower);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchivePowerCutSuccess);
                        _archive.InvokePowerCut();
                        _archive.ChangeState(ArchiveRoomState.WaitingForExtinguisherUse);
                        break;
                    case Actions.UseWater:
                        _archive.RegisterMistake(OfficeFireMistakeId.UsedWaterOnElectricalFire);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveWaterMistake);
                        _archive.InvokeWaterMistake();
                        break;
                    case Actions.GrabExtinguisher:
                    case Actions.UseExtinguisher:
                        _archive.RegisterMistake(OfficeFireMistakeId.UsedExtinguisherBeforePowerCut);
                        _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveCutPowerInstruction);
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
                _archive.SetObjective(OfficeFireObjectiveId.UseArchiveExtinguisher);
                _archive.PlayAnnouncement(OfficeFireVoiceLineId.ArchiveUseExtinguisherInstruction);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.UseExtinguisher:
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
