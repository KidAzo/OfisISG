using UnityEngine;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    public class KitchenCafeScenarioController : OfficeFireScenarioController
    {
        public static class Actions
        {
            public const string NoticeFire = "notice_fire";
            public const string UseWater = "use_water";
            public const string MovePan = "move_pan";
            public const string GrabExtinguisher = "grab_extinguisher";
            public const string UseExtinguisherTooClose = "use_extinguisher_too_close";
            public const string UseExtinguisherControlled = "use_extinguisher_controlled";
            public const string GrabBlanket = "grab_blanket";
            public const string PlaceBlanket = "place_blanket";
            public const string PlaceBlanketWrong = "place_blanket_wrong";
            public const string TurnOffStove = "turn_off_stove";
            public const string PressAlarm = "press_alarm";
            public const string ReachAssemblyArea = OfficeFireSharedScenarioActions.ReachAssemblyArea;
            public const string ReachedExitDoor = OfficeFireSharedScenarioActions.ReachedExitDoor;
            public const string ElevatorProximity = OfficeFireSharedScenarioActions.ElevatorProximity;
        }

        [Header("Kitchen Fire State")]
        [SerializeField]
        private KitchenFireStateController fireStateController;

        [Header("Kitchen — timing")]
        [SerializeField]
        private float delayedNotNoticeSeconds = 12f;

        [SerializeField]
        private float awarenessToDecisionDelaySeconds = 1f;

        [Header("Kitchen — hooks")]
        [SerializeField]
        private UnityEvent onKitchenFireStarted = new UnityEvent();

        [SerializeField]
        private UnityEvent onKitchenFireGrew = new UnityEvent();

        [SerializeField]
        private UnityEvent onPitcherMistake = new UnityEvent();

        [SerializeField]
        private UnityEvent onPanMoveMistake = new UnityEvent();

        [SerializeField]
        private UnityEvent onExtinguisherMistake = new UnityEvent();

        [SerializeField]
        private UnityEvent onExtinguisherAcceptable = new UnityEvent();

        [SerializeField]
        private UnityEvent onBlanketPlacedWrong = new UnityEvent();

        [SerializeField]
        private UnityEvent onBlanketPlacedCorrectly = new UnityEvent();

        [SerializeField]
        private UnityEvent onKitchenFireControlled = new UnityEvent();

        [Header("Kitchen — localized content (events)")]
        [SerializeField]
        private KitchenCafePopupEvent onPopupRequested = new KitchenCafePopupEvent();

        [SerializeField]
        private KitchenCafeVoiceEvent onScenarioVoiceRequested = new KitchenCafeVoiceEvent();

        [SerializeField]
        private KitchenCafeContentCueEvent onContentCueRequested = new KitchenCafeContentCueEvent();

        private ScenarioStateMachine<KitchenCafeState> _stateMachine;

        public override OfficeFireScenarioId ScenarioId => OfficeFireScenarioId.KitchenCafe;

        public KitchenCafeState CurrentState =>
            _stateMachine != null ? _stateMachine.CurrentStateId : KitchenCafeState.None;

        public KitchenFireStateController FireStateController => fireStateController;

        public KitchenCafePopupEvent OnPopupRequested => onPopupRequested;

        public KitchenCafeVoiceEvent OnScenarioVoiceRequested => onScenarioVoiceRequested;

        public KitchenCafeContentCueEvent OnContentCueRequested => onContentCueRequested;

        private void Awake()
        {
            _stateMachine = new ScenarioStateMachine<KitchenCafeState>();
            _stateMachine.RegisterState(new KitchenNoneState(this));
            _stateMachine.RegisterState(new KitchenFireStartedState(this));
            _stateMachine.RegisterState(new KitchenAwarenessState(this));
            _stateMachine.RegisterState(new KitchenNotNoticedState(this));
            _stateMachine.RegisterState(new KitchenDecisionState(this));
            _stateMachine.RegisterState(new KitchenWaitingForBlanketPlacementState(this));
            _stateMachine.RegisterState(new KitchenPitcherWrongResultState(this));
            _stateMachine.RegisterState(new KitchenMovePanWrongResultState(this));
            _stateMachine.RegisterState(new KitchenExtinguisherWrongResultState(this));
            _stateMachine.RegisterState(new KitchenExtinguisherAcceptableResultState(this));
            _stateMachine.RegisterState(new KitchenBlanketWrongResultState(this));
            _stateMachine.RegisterState(new KitchenBlanketCorrectResultState(this));
            _stateMachine.RegisterState(new KitchenWaitingForStoveOffState(this));
            _stateMachine.RegisterState(new KitchenFireControlledState(this));
            _stateMachine.RegisterState(new KitchenAlarmAndEvacuationState(this));
            _stateMachine.RegisterState(new KitchenWaitingForAssemblyAreaState(this));
            _stateMachine.RegisterState(new KitchenCompletedState(this));
            _stateMachine.StateChanged += HandleKitchenStateChanged;
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.StateChanged -= HandleKitchenStateChanged;
            }
        }

        public void ChangeKitchenFireState(KitchenFireState nextState)
        {
            if (fireStateController == null)
            {
                Debug.LogWarning("[KitchenCafeScenarioController] FireStateController is missing.", this);
                return;
            }

            fireStateController.ChangeFireState(nextState);
        }

        public void ShowPopup(KitchenCafePopupId popupId)
        {
            if (popupId == KitchenCafePopupId.None)
            {
                return;
            }

            Debug.Log($"[KitchenCafeScenarioController] Popup requested: {popupId}", this);
            if (onPopupRequested != null)
            {
                onPopupRequested.Invoke(popupId);
            }
        }

        public void PlayScenarioVoice(KitchenCafeVoiceId voiceId)
        {
            if (voiceId == KitchenCafeVoiceId.None)
            {
                return;
            }

            Debug.Log($"[KitchenCafeScenarioController] Voice requested: {voiceId}", this);
            if (onScenarioVoiceRequested != null)
            {
                onScenarioVoiceRequested.Invoke(voiceId);
            }
        }

        public void PlayContentCue(KitchenCafeContentCueId cueId)
        {
            if (cueId == KitchenCafeContentCueId.None)
            {
                return;
            }

            Debug.Log($"[KitchenCafeScenarioController] Content cue requested: {cueId}", this);
            if (onContentCueRequested != null)
            {
                onContentCueRequested.Invoke(cueId);
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

        public override void StartScenario()
        {
            base.StartScenario();
            ChangeState(KitchenCafeState.FireStarted);
        }

        public override void NotifyDeselected()
        {
            CancelInvoke();
            base.NotifyDeselected();
            if (_stateMachine != null)
            {
                _stateMachine.SnapTo(KitchenCafeState.None);
            }

            if (fireStateController != null)
            {
                fireStateController.ResetFire();
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

            if (actionId == Actions.ReachedExitDoor)
            {
                return;
            }

            _stateMachine.HandleAction(actionId);
        }

        protected override void ResetRuntimeState()
        {
            CancelInvoke();
            base.ResetRuntimeState();
            if (fireStateController != null)
            {
                fireStateController.ResetFire();
            }

            if (_stateMachine != null)
            {
                _stateMachine.SnapTo(KitchenCafeState.None);
            }
        }

        public void InvokeKitchenFireStarted()
        {
            if (onKitchenFireStarted != null)
            {
                onKitchenFireStarted.Invoke();
            }
        }

        public void InvokeKitchenFireGrew()
        {
            if (onKitchenFireGrew != null)
            {
                onKitchenFireGrew.Invoke();
            }
        }

        public void InvokePitcherMistake()
        {
            if (onPitcherMistake != null)
            {
                onPitcherMistake.Invoke();
            }
        }

        public void InvokePanMoveMistake()
        {
            if (onPanMoveMistake != null)
            {
                onPanMoveMistake.Invoke();
            }
        }

        public void InvokeExtinguisherMistake()
        {
            if (onExtinguisherMistake != null)
            {
                onExtinguisherMistake.Invoke();
            }
        }

        public void InvokeExtinguisherAcceptable()
        {
            if (onExtinguisherAcceptable != null)
            {
                onExtinguisherAcceptable.Invoke();
            }
        }

        public void InvokeBlanketPlacedWrong()
        {
            if (onBlanketPlacedWrong != null)
            {
                onBlanketPlacedWrong.Invoke();
            }
        }

        public void InvokeBlanketPlacedCorrectly()
        {
            if (onBlanketPlacedCorrectly != null)
            {
                onBlanketPlacedCorrectly.Invoke();
            }
        }

        public void InvokeKitchenFireControlled()
        {
            if (onKitchenFireControlled != null)
            {
                onKitchenFireControlled.Invoke();
            }
        }

        private void TryScheduledNotNoticed()
        {
            if (CurrentState == KitchenCafeState.FireStarted)
            {
                ChangeState(KitchenCafeState.NotNoticed);
            }
        }

        private void TryAwarenessToKitchenDecision()
        {
            if (CurrentState == KitchenCafeState.Awareness)
            {
                ChangeState(KitchenCafeState.KitchenDecision);
            }
        }

        private void HandleKitchenStateChanged(KitchenCafeState previous, KitchenCafeState next)
        {
            Debug.Log($"[KitchenCafeScenarioController] State {previous} -> {next}.", this);
        }

        private abstract class KitchenScenarioStateBase : ScenarioStateBase<KitchenCafeState>
        {
            protected readonly KitchenCafeScenarioController Kitchen;

            protected KitchenScenarioStateBase(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
                Kitchen = kitchen;
            }

            protected void GoTo(KitchenCafeState next)
            {
                Kitchen.ChangeState(next);
            }
        }

        private sealed class KitchenNoneState : KitchenScenarioStateBase
        {
            public KitchenNoneState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.None;

            public override void HandleAction(string actionId)
            {
            }
        }

        private sealed class KitchenFireStartedState : KitchenScenarioStateBase
        {
            public KitchenFireStartedState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.FireStarted;

            public override void Enter()
            {
                Kitchen.ChangeKitchenFireState(KitchenFireState.SmallPanFire);
                Kitchen.SetObjective(OfficeFireObjectiveId.CheckKitchenArea);
                Kitchen.PlayContentCue(KitchenCafeContentCueId.FireRiskDetected);
                Kitchen.InvokeKitchenFireStarted();
                Kitchen.Invoke(nameof(Kitchen.TryScheduledNotNoticed), Kitchen.delayedNotNoticeSeconds);
            }

            public override void Exit()
            {
                Kitchen.CancelInvoke(nameof(Kitchen.TryScheduledNotNoticed));
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.NoticeFire:
                        Kitchen.MarkReactionIfNeeded();
                        GoTo(KitchenCafeState.Awareness);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class KitchenAwarenessState : KitchenScenarioStateBase
        {
            public KitchenAwarenessState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.Awareness;

            public override void Enter()
            {
                Kitchen.SetObjective(OfficeFireObjectiveId.CheckKitchenArea);
                Kitchen.PlayContentCue(KitchenCafeContentCueId.OilFireWarning);
                Kitchen.Invoke(
                    nameof(Kitchen.TryAwarenessToKitchenDecision),
                    Kitchen.awarenessToDecisionDelaySeconds);
            }

            public override void Exit()
            {
                Kitchen.CancelInvoke(nameof(Kitchen.TryAwarenessToKitchenDecision));
            }

            public override void HandleAction(string actionId)
            {
                LogUnknownAction(actionId);
            }
        }

        private sealed class KitchenNotNoticedState : KitchenScenarioStateBase
        {
            public KitchenNotNoticedState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.NotNoticed;

            public override void Enter()
            {
                Kitchen.ChangeKitchenFireState(KitchenFireState.GrowingPanFire);
                if (!Kitchen.Report.mistakes.Contains(OfficeFireMistakeId.DelayedReaction))
                {
                    Kitchen.RegisterMistake(OfficeFireMistakeId.DelayedReaction);
                }

                Kitchen.SetObjective(OfficeFireObjectiveId.CheckKitchenArea);
                Kitchen.PlayContentCue(KitchenCafeContentCueId.GoToKitchen);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.NoticeFire:
                        Kitchen.MarkReactionIfNeeded();
                        GoTo(KitchenCafeState.KitchenDecision);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class KitchenDecisionState : KitchenScenarioStateBase
        {
            public KitchenDecisionState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.KitchenDecision;

            public override void Enter()
            {
                Kitchen.SetObjective(OfficeFireObjectiveId.GetFireBlanket);
                Kitchen.PlayContentCue(KitchenCafeContentCueId.DecisionInstruction);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.UseWater:
                        GoTo(KitchenCafeState.PitcherWrongResult);
                        break;
                    case Actions.MovePan:
                        GoTo(KitchenCafeState.MovePanWrongResult);
                        break;
                    case Actions.UseExtinguisherTooClose:
                        GoTo(KitchenCafeState.ExtinguisherWrongResult);
                        break;
                    case Actions.UseExtinguisherControlled:
                        GoTo(KitchenCafeState.ExtinguisherAcceptableResult);
                        break;
                    case Actions.GrabBlanket:
                        Kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.SelectedFireBlanket);
                        GoTo(KitchenCafeState.WaitingForBlanketPlacement);
                        break;
                    case Actions.PressAlarm:
                        Kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.PressedAlarm);
                        Kitchen.PlayContentCue(KitchenCafeContentCueId.EvacuationInstruction);
                        GoTo(KitchenCafeState.WaitingForAssemblyArea);
                        break;
                    case Actions.GrabExtinguisher:
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class KitchenWaitingForBlanketPlacementState : KitchenScenarioStateBase
        {
            public KitchenWaitingForBlanketPlacementState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.WaitingForBlanketPlacement;

            public override void Enter()
            {
                Kitchen.SetObjective(OfficeFireObjectiveId.PlaceFireBlanket);
                Kitchen.PlayContentCue(KitchenCafeContentCueId.BlanketInstruction);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.PlaceBlanket:
                        GoTo(KitchenCafeState.BlanketCorrectResult);
                        break;
                    case Actions.PlaceBlanketWrong:
                        GoTo(KitchenCafeState.BlanketWrongResult);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class KitchenPitcherWrongResultState : KitchenScenarioStateBase
        {
            public KitchenPitcherWrongResultState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.PitcherWrongResult;

            public override void Enter()
            {
                Kitchen.RegisterMistake(OfficeFireMistakeId.UsedWaterOnOilFire);
                Kitchen.PlayContentCue(KitchenCafeContentCueId.WaterMistake);
                Kitchen.InvokePitcherMistake();
                Kitchen.ChangeKitchenFireState(KitchenFireState.Fireball);
                Kitchen.InvokeKitchenFireGrew();
                GoTo(KitchenCafeState.AlarmAndEvacuation);
            }

            public override void HandleAction(string actionId)
            {
            }
        }

        private sealed class KitchenMovePanWrongResultState : KitchenScenarioStateBase
        {
            public KitchenMovePanWrongResultState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.MovePanWrongResult;

            public override void Enter()
            {
                Kitchen.RegisterMistake(OfficeFireMistakeId.MovedBurningPan);
                Kitchen.PlayContentCue(KitchenCafeContentCueId.PanMoveMistake);
                Kitchen.InvokePanMoveMistake();
                Kitchen.ChangeKitchenFireState(KitchenFireState.OilSpreadOnFloor);
                Kitchen.InvokeKitchenFireGrew();
                GoTo(KitchenCafeState.AlarmAndEvacuation);
            }

            public override void HandleAction(string actionId)
            {
            }
        }

        private sealed class KitchenExtinguisherWrongResultState : KitchenScenarioStateBase
        {
            public KitchenExtinguisherWrongResultState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.ExtinguisherWrongResult;

            public override void Enter()
            {
                Kitchen.RegisterMistake(OfficeFireMistakeId.UsedExtinguisherTooCloseToOilFire);
                Kitchen.PlayContentCue(KitchenCafeContentCueId.ExtinguisherWarning);
                Kitchen.InvokeExtinguisherMistake();
                Kitchen.ChangeKitchenFireState(KitchenFireState.HoodSpread);
                Kitchen.InvokeKitchenFireGrew();
                GoTo(KitchenCafeState.AlarmAndEvacuation);
            }

            public override void HandleAction(string actionId)
            {
            }
        }

        private sealed class KitchenExtinguisherAcceptableResultState : KitchenScenarioStateBase
        {
            public KitchenExtinguisherAcceptableResultState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.ExtinguisherAcceptableResult;

            public override void Enter()
            {
                Kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.UsedExtinguisherControlled);
                Kitchen.InvokeExtinguisherAcceptable();
                Kitchen.ChangeKitchenFireState(KitchenFireState.SuppressedByExtinguisher);
                Kitchen.PlayContentCue(KitchenCafeContentCueId.ExtinguisherWarning);
                GoTo(KitchenCafeState.AlarmAndEvacuation);
            }

            public override void HandleAction(string actionId)
            {
            }
        }

        private sealed class KitchenBlanketWrongResultState : KitchenScenarioStateBase
        {
            public KitchenBlanketWrongResultState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.BlanketWrongResult;

            public override void Enter()
            {
                Kitchen.RegisterMistake(OfficeFireMistakeId.FailedToCoverPanWithBlanket);
                Kitchen.PlayContentCue(KitchenCafeContentCueId.BlanketFailed);
                Kitchen.InvokeBlanketPlacedWrong();
                Kitchen.ChangeKitchenFireState(KitchenFireState.HoodSpread);
                Kitchen.InvokeKitchenFireGrew();
                GoTo(KitchenCafeState.AlarmAndEvacuation);
            }

            public override void HandleAction(string actionId)
            {
            }
        }

        private sealed class KitchenBlanketCorrectResultState : KitchenScenarioStateBase
        {
            public KitchenBlanketCorrectResultState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.BlanketCorrectResult;

            public override void Enter()
            {
                Kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.PlacedFireBlanketCorrectly);
                Kitchen.MarkFireControlled();
                Kitchen.PlayContentCue(KitchenCafeContentCueId.BlanketSuccess);
                Kitchen.InvokeBlanketPlacedCorrectly();
                Kitchen.ChangeKitchenFireState(KitchenFireState.SuppressedByBlanket);
                GoTo(KitchenCafeState.WaitingForStoveOff);
            }

            public override void HandleAction(string actionId)
            {
            }
        }

        private sealed class KitchenWaitingForStoveOffState : KitchenScenarioStateBase
        {
            public KitchenWaitingForStoveOffState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.WaitingForStoveOff;

            public override void Enter()
            {
                Kitchen.SetObjective(OfficeFireObjectiveId.TurnOffStove);
                Kitchen.PlayContentCue(KitchenCafeContentCueId.TurnOffStove);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.TurnOffStove:
                        Kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.TurnedOffStove);
                        if (Kitchen.Report.fireControlled)
                        {
                            GoTo(KitchenCafeState.FireControlled);
                        }

                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class KitchenFireControlledState : KitchenScenarioStateBase
        {
            public KitchenFireControlledState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.FireControlled;

            public override void Enter()
            {
                Kitchen.ChangeKitchenFireState(KitchenFireState.Controlled);
                Kitchen.PlayContentCue(KitchenCafeContentCueId.FireControlled);
                Kitchen.InvokeKitchenFireControlled();
                GoTo(KitchenCafeState.Completed);
            }

            public override void HandleAction(string actionId)
            {
            }
        }

        private sealed class KitchenAlarmAndEvacuationState : KitchenScenarioStateBase
        {
            public KitchenAlarmAndEvacuationState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.AlarmAndEvacuation;

            public override void Enter()
            {
                Kitchen.ChangeKitchenFireState(KitchenFireState.Uncontrolled);
                Kitchen.SetObjective(OfficeFireObjectiveId.PressKitchenAlarm);
                Kitchen.PlayContentCue(KitchenCafeContentCueId.FireGrowingEvacuate);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.PressAlarm:
                        Kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.PressedAlarm);
                        Kitchen.PlayContentCue(KitchenCafeContentCueId.EvacuationInstruction);
                        GoTo(KitchenCafeState.WaitingForAssemblyArea);
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class KitchenWaitingForAssemblyAreaState : KitchenScenarioStateBase
        {
            public KitchenWaitingForAssemblyAreaState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
            }

            public override KitchenCafeState StateId => KitchenCafeState.WaitingForAssemblyArea;

            public override void Enter()
            {
                Kitchen.SetObjective(OfficeFireObjectiveId.GoToAssemblyArea);
                Kitchen.PlayContentCue(KitchenCafeContentCueId.ReachAssemblyArea);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.ReachAssemblyArea:
                        Kitchen.MarkEvacuated();
                        Kitchen.RegisterCorrectAction(OfficeFireCorrectActionId.ReachedAssemblyArea);
                        GoTo(KitchenCafeState.Completed);
                        break;
                    case Actions.ElevatorProximity:
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class KitchenCompletedState : KitchenScenarioStateBase
        {
            private readonly KitchenCafeScenarioController _owner;

            public KitchenCompletedState(KitchenCafeScenarioController kitchen)
                : base(kitchen)
            {
                _owner = kitchen;
            }

            public override KitchenCafeState StateId => KitchenCafeState.Completed;

            public override void Enter()
            {
                _owner.PlayContentCue(KitchenCafeContentCueId.ScenarioCompleted);
                _owner.CompleteScenario();
            }

            public override void HandleAction(string actionId)
            {
            }
        }
    }
}
