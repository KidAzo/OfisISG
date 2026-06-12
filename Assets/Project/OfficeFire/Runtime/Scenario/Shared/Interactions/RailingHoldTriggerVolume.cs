using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Player entering the stair volume starts with <see cref="OfficeFireMistakeId.DidNotHoldHandrail"/>.
    /// After <see cref="requiredHoldSeconds"/> holding a collider tagged <see cref="targetTag"/>,
    /// the mistake is cleared and <see cref="OfficeFireCorrectActionId.HeldHandrailCorrectly"/> is registered.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Woi/Office Fire/Railing Hold Trigger Volume")]
    public sealed class RailingHoldTriggerVolume : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Tag on the hand / grab collider that touches the handrail.")]
        private string targetTag = "Railing";

        [SerializeField, Min(0.1f)]
        private float requiredHoldSeconds = 3f;

        float _timeInsideTrigger;
        bool canCalculateHold;
        bool isHolded;
        bool _defaultMistakeRegistered;

        void OnEnable()
        {
            _timeInsideTrigger = 0f;
            canCalculateHold = false;
            isHolded = false;
            _defaultMistakeRegistered = false;
            RegisterDefaultMistake();
        }

        void Update()
        {
            if (!canCalculateHold || isHolded)
            {
                return;
            }

            _timeInsideTrigger += Time.deltaTime;
            if (_timeInsideTrigger >= requiredHoldSeconds)
            {
                RegisterHoldSuccess();
                isHolded = true;
                Debug.Log("Holding");
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(targetTag))
            {
                return;
            }

            RegisterDefaultMistake();
            canCalculateHold = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(targetTag))
            {
                canCalculateHold = false;
            }
        }

        void RegisterDefaultMistake()
        {
            if (_defaultMistakeRegistered)
            {
                return;
            }

            if (!OfficeFireActiveScenarioLocator.TryGetActive(out OfficeFireScenarioController scenario))
            {
                return;
            }

            scenario.RegisterMistake(OfficeFireMistakeId.DidNotHoldHandrail);
            _defaultMistakeRegistered = true;
        }

        void RegisterHoldSuccess()
        {
            if (!OfficeFireActiveScenarioLocator.TryGetActive(out OfficeFireScenarioController scenario))
            {
                return;
            }

            scenario.Report.RemoveMistake(OfficeFireMistakeId.DidNotHoldHandrail);
            scenario.RegisterCorrectAction(OfficeFireCorrectActionId.HeldHandrailCorrectly);
        }
    }
}
