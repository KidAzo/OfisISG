using System;
using UnityEngine;

namespace Woi.OfficeFire
{
    public abstract class ScenarioStateBase<TState> : IScenarioState<TState>
        where TState : Enum
    {
        protected readonly OfficeFireScenarioController Controller;

        public abstract TState StateId { get; }

        protected ScenarioStateBase(OfficeFireScenarioController controller)
        {
            Controller = controller;
        }

        public virtual void Enter()
        {
        }

        public virtual void Exit()
        {
        }

        public virtual void HandleAction(string actionId)
        {
        }

        protected void LogUnknownAction(string actionId)
        {
            Debug.Log(
                $"[{Controller.GetType().Name}] Unknown action '{actionId}' in state {StateId}.",
                Controller);
        }
    }
}
