using UnityEngine;

namespace Woi.Equipment
{
    /// <summary>
    /// Places used extinguishers into sequential ground slots for visual collection.
    /// </summary>
    [AddComponentMenu("Woi/Equipment/Used Extinguisher Area")]
    public sealed class UsedExtinguisherArea : MonoBehaviour
    {
        [SerializeField] private Transform[] _usedSlots;
        [SerializeField] private Vector3 _localStartOffset;
        [SerializeField] private Vector3 _localSpacing = new Vector3(0.3f, 0f, 0f);
        [SerializeField] private bool _disableInteractionWhenUsedPlaced = true;

        [Header("Used Appearance")]
        [Tooltip("Euler angles applied to make the extinguisher lie down (e.g. 90 on X).")]
        [SerializeField] private Vector3 _layDownRotation = new Vector3(90f, 0f, 0f);
        [Tooltip("Offset applied after base pose to prevent clipping into the floor (Y is up).")]
        [SerializeField] private Vector3 _layDownOffset = new Vector3(0f, 0.08f, 0f);

        private int _currentIndex;

        public Pose GetNextUsedPose()
        {
            if (_usedSlots != null && _currentIndex < _usedSlots.Length && _usedSlots[_currentIndex] != null)
            {
                Transform slot = _usedSlots[_currentIndex];
                return new Pose(slot.position, slot.rotation);
            }

            Vector3 localOffset = _localStartOffset + _localSpacing * _currentIndex;
            return new Pose(transform.TransformPoint(localOffset), transform.rotation);
        }

        public void PlaceUsedExtinguisher(ExtinguisherPickupItem extinguisher)
        {
            if (extinguisher == null)
                return;

            Pose pose = GetNextUsedPose();
            _currentIndex++;

            Quaternion finalRot = pose.rotation * Quaternion.Euler(_layDownRotation);
            Vector3 finalPos = pose.position + pose.rotation * _layDownOffset;

            extinguisher.PlaceInWorld(
                finalPos,
                finalRot,
                extinguisher.transform.localScale,
                enableColliders: !_disableInteractionWhenUsedPlaced);
        }
    }
}
