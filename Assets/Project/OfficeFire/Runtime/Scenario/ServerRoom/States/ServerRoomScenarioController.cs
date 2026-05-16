using UnityEngine;

namespace Woi.OfficeFire
{
    public class ServerRoomScenarioController : OfficeFireScenarioController
    {
        public static class Actions
        {
            public const string NoticeSmoke = "notice_smoke";
            public const string EnterServerRoom = "enter_server_room";
            public const string UseWater = "use_water";
            public const string UseExtinguisher = "use_extinguisher";
            public const string PressSuppressionButton = "press_suppression_button";
            public const string LeaveServerRoom = "leave_server_room";
            public const string ReachAssemblyArea = "reach_assembly_area";
        }

        private ScenarioStateMachine<ServerRoomState> _stateMachine;

        public override OfficeFireScenarioId ScenarioId => OfficeFireScenarioId.ServerRoom;

        public ServerRoomState CurrentState => _stateMachine != null ? _stateMachine.CurrentStateId : ServerRoomState.None;

        private void Awake()
        {
            _stateMachine = new ScenarioStateMachine<ServerRoomState>();
            _stateMachine.RegisterState(new ServerNoneState(this));
            _stateMachine.RegisterState(new ServerWaitingForSmokeNoticeState(this));
            _stateMachine.RegisterState(new ServerWaitingForEntryState(this));
            _stateMachine.RegisterState(new ServerWaitingForSuppressionActivationState(this));
            _stateMachine.RegisterState(new ServerWaitingForExitRoomState(this));
            _stateMachine.RegisterState(new ServerWaitingForAssemblyAreaState(this));
            _stateMachine.RegisterState(new ServerCompletedState(this));
            _stateMachine.StateChanged += HandleServerStateChanged;
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.StateChanged -= HandleServerStateChanged;
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

        public override void StartScenario()
        {
            base.StartScenario();
            ChangeState(ServerRoomState.WaitingForSmokeNotice);
        }

        public override void NotifyDeselected()
        {
            base.NotifyDeselected();
            if (_stateMachine != null)
            {
                _stateMachine.SnapTo(ServerRoomState.None);
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
            base.ResetRuntimeState();
            if (_stateMachine != null)
            {
                _stateMachine.SnapTo(ServerRoomState.None);
            }
        }

        private void HandleServerStateChanged(ServerRoomState previous, ServerRoomState next)
        {
            Debug.Log($"[ServerRoomScenarioController] State {previous} -> {next}.", this);
        }

        private abstract class ServerScenarioStateBase : ScenarioStateBase<ServerRoomState>
        {
            protected readonly ServerRoomScenarioController Server;

            protected ServerScenarioStateBase(ServerRoomScenarioController server)
                : base(server)
            {
                Server = server;
            }

            protected void RegisterWaterMistake()
            {
                Server.RegisterMistake(OfficeFireMistakeId.UsedWaterOnServerFire);
                Server.PlayAnnouncement(OfficeFireVoiceLineId.ServerWaterMistake);
            }

            protected void GoTo(ServerRoomState next)
            {
                Server.ChangeState(next);
            }
        }

        private sealed class ServerNoneState : ServerScenarioStateBase
        {
            public ServerNoneState(ServerRoomScenarioController server)
                : base(server)
            {
            }

            public override ServerRoomState StateId => ServerRoomState.None;

            public override void HandleAction(string actionId)
            {
            }
        }

        private sealed class ServerWaitingForSmokeNoticeState : ServerScenarioStateBase
        {
            public ServerWaitingForSmokeNoticeState(ServerRoomScenarioController server)
                : base(server)
            {
            }

            public override ServerRoomState StateId => ServerRoomState.WaitingForSmokeNotice;

            public override void Enter()
            {
                Server.SetObjective(OfficeFireObjectiveId.CheckServerRoom);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.NoticeSmoke:
                        Server.MarkReactionIfNeeded();
                        Server.RegisterCorrectAction(OfficeFireCorrectActionId.NoticedSmoke);
                        GoTo(ServerRoomState.WaitingForEntry);
                        break;
                    case Actions.UseWater:
                        RegisterWaterMistake();
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ServerWaitingForEntryState : ServerScenarioStateBase
        {
            public ServerWaitingForEntryState(ServerRoomScenarioController server)
                : base(server)
            {
            }

            public override ServerRoomState StateId => ServerRoomState.WaitingForEntry;

            public override void Enter()
            {
                Server.SetObjective(OfficeFireObjectiveId.EnterServerRoom);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.EnterServerRoom:
                        Server.RegisterCorrectAction(OfficeFireCorrectActionId.EnteredServerRoomSafely);
                        GoTo(ServerRoomState.WaitingForSuppressionActivation);
                        break;
                    case Actions.UseWater:
                        RegisterWaterMistake();
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ServerWaitingForSuppressionActivationState : ServerScenarioStateBase
        {
            public ServerWaitingForSuppressionActivationState(ServerRoomScenarioController server)
                : base(server)
            {
            }

            public override ServerRoomState StateId => ServerRoomState.WaitingForSuppressionActivation;

            public override void Enter()
            {
                Server.SetObjective(OfficeFireObjectiveId.ActivateServerSuppression);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.UseExtinguisher:
                        Server.RegisterMistake(OfficeFireMistakeId.UsedManualExtinguisherBeforeSuppression);
                        Server.PlayAnnouncement(OfficeFireVoiceLineId.ServerManualExtinguisherWarning);
                        break;
                    case Actions.PressSuppressionButton:
                        Server.RegisterCorrectAction(OfficeFireCorrectActionId.ActivatedSuppressionSystem);
                        GoTo(ServerRoomState.WaitingForExitRoom);
                        break;
                    case Actions.UseWater:
                        RegisterWaterMistake();
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ServerWaitingForExitRoomState : ServerScenarioStateBase
        {
            public ServerWaitingForExitRoomState(ServerRoomScenarioController server)
                : base(server)
            {
            }

            public override ServerRoomState StateId => ServerRoomState.WaitingForExitRoom;

            public override void Enter()
            {
                Server.SetObjective(OfficeFireObjectiveId.LeaveServerRoom);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.LeaveServerRoom:
                        Server.RegisterCorrectAction(OfficeFireCorrectActionId.LeftServerRoomBeforeGas);
                        GoTo(ServerRoomState.WaitingForAssemblyArea);
                        break;
                    case Actions.UseWater:
                        RegisterWaterMistake();
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ServerWaitingForAssemblyAreaState : ServerScenarioStateBase
        {
            public ServerWaitingForAssemblyAreaState(ServerRoomScenarioController server)
                : base(server)
            {
            }

            public override ServerRoomState StateId => ServerRoomState.WaitingForAssemblyArea;

            public override void Enter()
            {
                Server.SetObjective(OfficeFireObjectiveId.GoToAssemblyArea);
            }

            public override void HandleAction(string actionId)
            {
                switch (actionId)
                {
                    case Actions.ReachAssemblyArea:
                        Server.MarkEvacuated();
                        Server.RegisterCorrectAction(OfficeFireCorrectActionId.ReachedAssemblyArea);
                        GoTo(ServerRoomState.Completed);
                        break;
                    case Actions.UseWater:
                        RegisterWaterMistake();
                        break;
                    default:
                        LogUnknownAction(actionId);
                        break;
                }
            }
        }

        private sealed class ServerCompletedState : ServerScenarioStateBase
        {
            private readonly ServerRoomScenarioController _owner;

            public ServerCompletedState(ServerRoomScenarioController server)
                : base(server)
            {
                _owner = server;
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
