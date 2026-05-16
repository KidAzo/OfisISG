using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.Equipment
{
    /// <summary>
    /// Stores where an extinguisher belongs when unused and can spawn a fresh replacement.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Equipment/Extinguisher Home Point")]
    public sealed class ExtinguisherHomePoint : MonoBehaviour
    {
        [SerializeField] private Transform _homePoint;
        [SerializeField] private GameObject _extinguisherPrefab;
        [SerializeField] private Transform _parentForSpawnedExtinguishers;
        [SerializeField] private bool _spawnReplacementWhenUsed = true;

        private bool _replacementSpawnedForUsedDrop;

        public Transform HomePoint => _homePoint;
        public bool SpawnReplacementWhenUsed => _spawnReplacementWhenUsed;

        private void Awake()
        {
            if (_homePoint == null)
                _homePoint = transform;
        }

        public void ReturnToHome(ExtinguisherPickupItem item)
        {
            if (item == null)
                return;

            if (_homePoint == null)
            {
                Debug.LogWarning("[ExtinguisherHomePoint] No home point assigned.", this);
                return;
            }

            item.PlaceInWorld(_homePoint.position, _homePoint.rotation, _homePoint.localScale, enableColliders: true);
        }

        public GameObject SpawnFreshReplacement()
        {
            if (!_spawnReplacementWhenUsed)
                return null;

            if (_replacementSpawnedForUsedDrop)
                return null;

            if (_homePoint == null)
            {
                Debug.LogWarning("[ExtinguisherHomePoint] No home point assigned; cannot spawn replacement.", this);
                return null;
            }

            if (_extinguisherPrefab == null)
            {
                Debug.LogWarning("[ExtinguisherHomePoint] No extinguisher prefab assigned; cannot spawn replacement.", this);
                return null;
            }

            _replacementSpawnedForUsedDrop = true;

            GameObject replacement = Instantiate(
                _extinguisherPrefab,
                _homePoint.position,
                _homePoint.rotation,
                _parentForSpawnedExtinguishers);

            replacement.transform.localScale = _homePoint.localScale;
            ResetReplacementState(replacement);
            return replacement;
        }

        private static void ResetReplacementState(GameObject replacement)
        {
            if (replacement == null)
                return;

            ExtinguisherUsageState usageState = replacement.GetComponentInChildren<ExtinguisherUsageState>(true);
            if (usageState != null)
                usageState.ResetUsageState();

            ExtinguisherController controller = replacement.GetComponentInChildren<ExtinguisherController>(true);
            if (controller != null)
                controller.ResetAfterWorldDrop();

            replacement.SendMessage("ResetFeedback", SendMessageOptions.DontRequireReceiver);
            replacement.SendMessage("ResetHover", SendMessageOptions.DontRequireReceiver);
        }
    }
}
