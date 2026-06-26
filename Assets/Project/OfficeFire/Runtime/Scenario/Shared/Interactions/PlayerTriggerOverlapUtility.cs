using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Shared overlap helpers for trigger volumes (CharacterController does not appear in Physics.Overlap*).
    /// </summary>
    public static class PlayerTriggerOverlapUtility
    {
        public static Collider[] QueryLayerColliders(Collider volume, LayerMask layers, Transform volumeTransform)
        {
            if (volume == null)
                return null;

            if (volume is BoxCollider box)
            {
                return Physics.OverlapBox(
                    volumeTransform.TransformPoint(box.center),
                    Vector3.Scale(box.size, volumeTransform.lossyScale) * 0.5f,
                    volumeTransform.rotation,
                    layers,
                    QueryTriggerInteraction.Collide);
            }

            if (volume is SphereCollider sphere)
            {
                float maxScale = Mathf.Max(
                    volumeTransform.lossyScale.x,
                    Mathf.Max(volumeTransform.lossyScale.y, volumeTransform.lossyScale.z));
                return Physics.OverlapSphere(
                    volumeTransform.TransformPoint(sphere.center),
                    sphere.radius * maxScale,
                    layers,
                    QueryTriggerInteraction.Collide);
            }

            return Physics.OverlapBox(
                volume.bounds.center,
                volume.bounds.extents,
                Quaternion.identity,
                layers,
                QueryTriggerInteraction.Collide);
        }

        public static bool IsLayerInMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        public static bool CharacterControllerIntersectsVolume(CharacterController controller, Collider volume)
        {
            if (controller == null || volume == null || !controller.enabled)
                return false;

            return volume.bounds.Intersects(GetCharacterControllerWorldBounds(controller));
        }

        public static Bounds GetCharacterControllerWorldBounds(CharacterController controller)
        {
            Vector3 worldCenter = controller.transform.TransformPoint(controller.center);
            float diameter = controller.radius * 2f;
            float height = Mathf.Max(controller.height, diameter);
            return new Bounds(worldCenter, new Vector3(diameter, height, diameter));
        }

        public static CharacterController[] FindActiveCharacterControllers()
        {
            return Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        }
    }
}
