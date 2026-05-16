using UnityEngine;

namespace Woi.UI.Announcements
{
    /// <summary>
    /// One ray per frame from the camera through a viewport point (default screen center). Drives
    /// <see cref="ExtinguisherHoverController"/> instances that use <see cref="HoverPointerMode.CameraCenterRay"/>.
    /// VR: aynı mod için sağ kontrolcüde <see cref="ExtinguisherHoverTransformRaycaster"/> kullanın.
    /// Uses <see cref="Collider"/> hits on self or parent (child mesh colliders). Uses <see cref="QueryTriggerInteraction.Collide"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExtinguisherHoverRaycaster : MonoBehaviour
    {
        [SerializeField] private Camera rayCamera;

        [Tooltip("0.5,0.5 = screen center / crosshair.")]
        [SerializeField] private Vector2 viewportPoint = new Vector2(0.5f, 0.5f);

        [SerializeField] private float maxDistance = 12f;

        [SerializeField] private LayerMask layerMask = ~0;

        [Tooltip("When true, every frame the ray hits no HoverController we clear stale hover state and optionally hide any visible popup (fixes sticky cards when raycast state drifts).")]
        [SerializeField] private bool enforceCleanupWhenRayMissesHover = true;

        [Tooltip("When cleanup runs and no HoverController is hit, also call IPopupService.Hide() if a popup is visible.")]
        [SerializeField] private bool hideVisiblePopupWhenRayMissesHover = true;

        private ExtinguisherHoverController _current;

        private void Awake()
        {
            if (rayCamera == null)
                rayCamera = GetComponent<Camera>();

            if (rayCamera == null)
                rayCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (rayCamera == null)
                return;

            if (ExtinguisherHoverController.IsPlayerHoldingExtinguisherForTubeHover())
            {
                if (_current != null)
                {
                    _current.NotifyRayHoverEnd();
                    _current = null;
                }
            }

            if (!TryResolveHoverTarget(rayCamera, out ExtinguisherHoverController target, out RaycastHit hit))
                target = null;

            if (target != _current)
            {
                if (_current != null)
                    _current.NotifyRayHoverEnd();

                _current = target;

                if (_current != null && !_current.NotifyRayHoverBegin(in hit))
                    _current = null;
            }

            if (enforceCleanupWhenRayMissesHover && target == null)
                ExtinguisherHoverController.ApplyRayMissCleanup(hideVisiblePopupWhenRayMissesHover);
        }

        bool TryResolveHoverTarget(Camera cam, out ExtinguisherHoverController ctrl, out RaycastHit hit)
        {
            hit = default;
            ctrl = null;
            Ray ray = cam.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));

            if (!Physics.Raycast(ray, out hit, maxDistance, layerMask, QueryTriggerInteraction.Collide))
                return false;

            ctrl = hit.collider.GetComponentInParent<ExtinguisherHoverController>();
            if (ctrl == null || ctrl.PointerMode != HoverPointerMode.CameraCenterRay)
            {
                ctrl = null;
                return false;
            }

            return true;
        }

        private void OnDisable()
        {
            if (_current != null)
            {
                _current.NotifyRayHoverEnd();
                _current = null;
            }

            if (enforceCleanupWhenRayMissesHover)
                ExtinguisherHoverController.ApplyRayMissCleanup(hideVisiblePopupWhenRayMissesHover);
        }
    }
}
