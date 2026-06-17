using Obvious.Soap;
using UnityEngine;
using Woi.InputSystem;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Dispatches a scenario action when lean input starts (e.g. Left Ctrl held).
    /// Resolves the active scenario from <see cref="OfficeFireActiveScenarioLocator"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficeFirePlayerLeanScenarioBridge : MonoBehaviour, ISoapLeanInputListener
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
            SubscribeLean();
        }

        private void Start()
        {
            TryBindLiveLeanEvent();
        }

        private void OnDisable()
        {
            UnsubscribeLean();
        }

        public bool IsListeningToDifferentLeanEvent(ScriptableEventFloat liveLeanEvent) =>
            leanInputEvent != null
            && liveLeanEvent != null
            && !ReferenceEquals(leanInputEvent, liveLeanEvent);

        public void RebindLeanInputEvent(ScriptableEventFloat liveLeanEvent)
        {
            UnsubscribeLean();
            leanInputEvent = liveLeanEvent;
            if (isActiveAndEnabled)
            {
                SubscribeLean();
            }
        }

        private void SubscribeLean()
        {
            TryBindLiveLeanEvent();

            if (leanInputEvent == null)
            {
                return;
            }

            leanInputEvent.OnRaised += OnLeanInput;
        }

        private void TryBindLiveLeanEvent()
        {
            InputManager inputManager = FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);
            if (inputManager == null)
            {
                return;
            }

            ScriptableEventFloat liveLean = inputManager.GetPcGameplayContext()?.LeanInputEvent;
            if (liveLean == null)
            {
                return;
            }

            if (leanInputEvent == null || IsListeningToDifferentLeanEvent(liveLean))
            {
                RebindLeanInputEvent(liveLean);
            }
        }

        private void UnsubscribeLean()
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
