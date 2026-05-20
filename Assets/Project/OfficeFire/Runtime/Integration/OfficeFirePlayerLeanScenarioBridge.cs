using Obvious.Soap;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Dispatches a scenario action when lean input starts (e.g. Left Ctrl held).
    /// Resolves the active scenario from <see cref="OfficeFireActiveScenarioLocator"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficeFirePlayerLeanScenarioBridge : MonoBehaviour
    {
        [SerializeField]
        private ScriptableEventFloat leanInputEvent;

        [SerializeField]
        private string actionId = "player_leaned";

        [SerializeField]
        private bool dispatchOnceUntilLeanEnds = true;

        private bool _hasDispatchedThisLean;

        private void OnEnable()
        {
            if (leanInputEvent == null)
            {
                return;
            }

            leanInputEvent.OnRaised += OnLeanInput;
        }

        private void OnDisable()
        {
            if (leanInputEvent == null)
            {
                return;
            }

            leanInputEvent.OnRaised -= OnLeanInput;
        }

        private void OnLeanInput(float leanAxis)
        {
            if (leanAxis > 0.01f)
            {
                if (dispatchOnceUntilLeanEnds && _hasDispatchedThisLean)
                {
                    return;
                }

                DispatchAction();
                _hasDispatchedThisLean = true;
                return;
            }

            _hasDispatchedThisLean = false;
        }

        private void DispatchAction()
        {
            if (string.IsNullOrEmpty(actionId))
            {
                return;
            }

            if (!OfficeFireActiveScenarioLocator.TryGetActive(out OfficeFireScenarioController scenario))
            {
                return;
            }

            scenario.HandleAction(actionId);
        }
    }
}
