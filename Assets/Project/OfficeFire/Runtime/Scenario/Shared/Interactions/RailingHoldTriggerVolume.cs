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
    public sealed class RailingHoldTriggerVolume : MonoBehaviour, IPlayerTriggerVolumeRefresh
    {
        [SerializeField]
        [Tooltip("Tag on the hand / grab collider that touches the handrail.")]
        private string targetTag = "Railing";

        [SerializeField, Min(0.1f)]
        private float requiredHoldSeconds = 3f;

        [SerializeField]
        private LayerMask playerLayer;

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
            StartCoroutine(RefreshAfterEnable());
        }

        System.Collections.IEnumerator RefreshAfterEnable()
        {
            yield return new WaitForFixedUpdate();
            RefreshPlayerOverlap();
        }

        public void RefreshPlayerOverlap()
        {
            Collider volume = GetComponent<Collider>();
            if (volume == null || !volume.enabled)
                return;

            bool playerInside = false;

            if (playerLayer.value != 0)
            {
                Collider[] overlaps = PlayerTriggerOverlapUtility.QueryLayerColliders(volume, playerLayer, transform);
                if (overlaps != null && overlaps.Length > 0)
                    playerInside = true;
            }

            CharacterController[] controllers = PlayerTriggerOverlapUtility.FindActiveCharacterControllers();
            for (int i = 0; i < controllers.Length; i++)
            {
                CharacterController controller = controllers[i];
                if (controller == null || !controller.gameObject.activeInHierarchy)
                    continue;

                if (playerLayer.value != 0
                    && !PlayerTriggerOverlapUtility.IsLayerInMask(controller.gameObject.layer, playerLayer))
                {
                    continue;
                }

                if (!PlayerTriggerOverlapUtility.CharacterControllerIntersectsVolume(controller, volume))
                    continue;

                playerInside = true;
                break;
            }

            if (playerInside)
                RegisterDefaultMistake();

            if (string.IsNullOrEmpty(targetTag))
                return;

            Collider[] allOverlaps = PlayerTriggerOverlapUtility.QueryLayerColliders(
                volume,
                Physics.AllLayers,
                transform);

            if (allOverlaps == null)
                return;

            for (int i = 0; i < allOverlaps.Length; i++)
            {
                Collider overlap = allOverlaps[i];
                if (overlap != null && overlap.CompareTag(targetTag))
                    canCalculateHold = true;
            }
        }

        void Update()
        {
            if (!canCalculateHold || isHolded)
                return;

            _timeInsideTrigger += Time.deltaTime;
            if (_timeInsideTrigger >= requiredHoldSeconds)
            {
                RegisterHoldSuccess();
                isHolded = true;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(targetTag))
            {
                RegisterDefaultMistake();
                canCalculateHold = true;
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(targetTag))
                canCalculateHold = false;
        }

        void RegisterDefaultMistake()
        {
            if (_defaultMistakeRegistered)
                return;

            if (!OfficeFireActiveScenarioLocator.TryGetActive(out OfficeFireScenarioController scenario))
                return;

            scenario.RegisterMistake(OfficeFireMistakeId.DidNotHoldHandrail);
            _defaultMistakeRegistered = true;
        }

        void RegisterHoldSuccess()
        {
            if (!OfficeFireActiveScenarioLocator.TryGetActive(out OfficeFireScenarioController scenario))
                return;

            scenario.Report.RemoveMistake(OfficeFireMistakeId.DidNotHoldHandrail);
            scenario.RegisterCorrectAction(OfficeFireCorrectActionId.HeldHandrailCorrectly);
        }

        void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null)
                c.isTrigger = true;

            playerLayer = LayerMask.GetMask("Player");
        }
    }
}
