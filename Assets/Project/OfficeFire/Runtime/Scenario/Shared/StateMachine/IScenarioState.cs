using System;

namespace Woi.OfficeFire
{
    public interface IScenarioState<TState> where TState : Enum
    {
        TState StateId { get; }

        void Enter();

        void Exit();

        void HandleAction(string actionId);
    }
}
