using System;
using System.Collections.Generic;
using UnityEngine;

namespace Woi.OfficeFire
{
    public sealed class ScenarioStateMachine<TState> where TState : Enum
    {
        private readonly Dictionary<TState, IScenarioState<TState>> _states =
            new Dictionary<TState, IScenarioState<TState>>();

        public TState CurrentStateId { get; private set; }

        public IScenarioState<TState> CurrentState { get; private set; }

        public event Action<TState, TState> StateChanged;

        public void RegisterState(IScenarioState<TState> state)
        {
            if (state == null)
            {
                return;
            }

            _states[state.StateId] = state;
        }

        public bool HasState(TState stateId)
        {
            return _states.ContainsKey(stateId);
        }

        public void ChangeState(TState nextStateId)
        {
            if (!_states.TryGetValue(nextStateId, out IScenarioState<TState> nextState))
            {
                Debug.LogError(
                    $"[ScenarioStateMachine<{typeof(TState).Name}>] Invalid transition: state '{nextStateId}' is not registered.");
                return;
            }

            if (EqualityComparer<TState>.Default.Equals(CurrentStateId, nextStateId))
            {
                return;
            }

            TState previousStateId = CurrentStateId;
            CurrentState?.Exit();
            CurrentStateId = nextStateId;
            CurrentState = nextState;
            CurrentState?.Enter();
            StateChanged?.Invoke(previousStateId, nextStateId);
        }

        public void HandleAction(string actionId)
        {
            if (CurrentState == null)
            {
                Debug.LogError(
                    $"[ScenarioStateMachine<{typeof(TState).Name}>] HandleAction('{actionId}') ignored: no active state.");
                return;
            }

            CurrentState.HandleAction(actionId);
        }

        /// <summary>
        /// Resets the active state pointer for deselect/restart wiring only.
        /// Does not call <see cref="IScenarioState{TState}.Enter"/> or <see cref="IScenarioState{TState}.Exit"/> and does not raise <see cref="StateChanged"/>.
        /// Normal gameplay must use <see cref="ChangeState"/>.
        /// </summary>
        public void SnapTo(TState stateId)
        {
            if (!_states.TryGetValue(stateId, out IScenarioState<TState> nextState))
            {
                Debug.LogError(
                    $"[ScenarioStateMachine<{typeof(TState).Name}>] SnapTo failed: state '{stateId}' is not registered.");
                return;
            }

            CurrentStateId = stateId;
            CurrentState = nextState;
        }
    }
}
