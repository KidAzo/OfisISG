using System;
using UnityEngine;
using Woi.Equipment;

namespace Woi.Game
{
    /// <summary>
    /// Presentation-only hover outline for interactable objects (PC: camera center ray; VR: same aim as
    /// ExtinguisherHoverTransformRaycaster when registered via FireVrGameplayInteractionRay).
    /// </summary>
    [AddComponentMenu("Woi/Feedback/Hover Outline")]
    public sealed class HoverOutline : MonoBehaviour
    {
        private static readonly Vector2 ViewportCenter = new Vector2(0.5f, 0.5f);

        [SerializeField] private Outline _outline;
        [Tooltip("PC: player camera. VR: same transform as ExtinguisherHoverTransformRaycaster (fallback if VR ray is not registered).")]
        [SerializeField] private Transform _rayOrigin;
        [SerializeField, Min(0f)] private float _hoverRange = 5f;
        [SerializeField] private LayerMask _hoverLayerMask = Physics.AllLayers;
        [SerializeField] private bool _enableOnStart;
        [SerializeField] private bool _useOutlineWidth;
        [SerializeField, Min(0f)] private float _hoverOutlineWidth = 5f;

        private bool _isHovered;
        private bool _loggedMissingOutline;
        private bool _loggedMissingRayOrigin;
        private Camera _resolvedCamera;
        private float _defaultOutlineWidth;

        public bool IsHovered => _isHovered;

        public void ConfigurePickupHover(
            Transform rayOrigin,
            Outline outline,
            float hoverRange = 5f,
            bool useOutlineWidth = false,
            float hoverOutlineWidth = 5f)
        {
            _rayOrigin = rayOrigin;
            _outline = outline;
            _hoverRange = hoverRange;
            _useOutlineWidth = useOutlineWidth;
            _hoverOutlineWidth = hoverOutlineWidth;

            int extinguisherLayer = LayerMask.NameToLayer("Estinguisher");
            if (extinguisherLayer >= 0)
            {
                _hoverLayerMask = 1 << extinguisherLayer;
            }

            EnsureOutlineBinding();
            if (_outline != null)
            {
                _defaultOutlineWidth = _outline.OutlineWidth;
            }

            ApplyInitialState();
        }

        private void Awake()
        {
            EnsureOutlineBinding();
            ApplyInitialState();
        }

        private void OnEnable()
        {
            EnsureOutlineBinding();
            ApplyInitialState();
        }

        private void Update()
        {
            if (!ValidateReferences())
                return;

            ExtinguisherPickupItem pickup = GetComponentInParent<ExtinguisherPickupItem>();
            if (pickup != null && pickup.IsEquipped)
            {
                if (_isHovered)
                {
                    _isHovered = false;
                    ApplyOutlineVisual(false);
                }

                return;
            }

            bool hovered = IsRayHoveringThisObject();
            if (_isHovered == hovered)
                return;

            _isHovered = hovered;
            ApplyOutlineVisual(hovered);
        }

        private void ApplyOutlineVisual(bool isHovered)
        {
            if (_outline == null)
            {
                return;
            }

            if (_defaultOutlineWidth <= 0f)
            {
                _defaultOutlineWidth = _outline.OutlineWidth;
            }

            if (_useOutlineWidth)
            {
                _outline.enabled = isHovered;
                _outline.OutlineWidth = isHovered ? _hoverOutlineWidth : _defaultOutlineWidth;
                return;
            }

            _outline.enabled = isHovered;
        }

        private void ApplyInitialState()
        {
            if (!ValidateOutline())
                return;

            ApplyOutlineVisual(_enableOnStart);
            _isHovered = _enableOnStart;
        }

        public void ResetHover()
        {
            _isHovered = false;
            ApplyOutlineVisual(false);
        }

        private bool IsRayHoveringThisObject()
        {
            if (!TryGetHoverRay(out Vector3 origin, out Vector3 dir))
                return false;

            LayerMask hoverMask = _hoverLayerMask;
            MergeExtinguisherHoverLayers(ref hoverMask);

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                dir,
                _hoverRange,
                hoverMask,
                QueryTriggerInteraction.Collide);

            if (hits.Length == 0)
                return false;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Transform vrSkipRoot = FireVrGameplayInteractionRay.RegisteredRayOriginOrNull;
            ExtinguisherPickupItem pickup = GetComponentInParent<ExtinguisherPickupItem>();
            IHoverOutlineTarget hoverTarget = pickup == null ? GetComponentInParent<IHoverOutlineTarget>() : null;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                Transform hitTransform = hit.collider.transform;
                if (vrSkipRoot != null && IsTransformUnder(hitTransform, vrSkipRoot))
                    continue;

                if (pickup != null)
                {
                    ExtinguisherPickupItem hitPickup = hitTransform.GetComponentInParent<ExtinguisherPickupItem>();
                    if (hitPickup != null && hitPickup == pickup)
                        return true;

                    continue;
                }

                if (hoverTarget != null)
                {
                    if (hoverTarget.IsHoveredCollider(hitTransform))
                        return true;

                    continue;
                }

                if (IsHitRelevantToThisObject(hitTransform))
                    return true;

                if (!hit.collider.isTrigger)
                    return false;
            }

            return false;
        }

        private bool TryGetHoverRay(out Vector3 origin, out Vector3 direction)
        {
            origin = default;
            direction = default;

            Camera camera = ResolveCamera();
            if (camera != null)
            {
                Ray ray = camera.ViewportPointToRay(ViewportCenter);
                origin = ray.origin;
                direction = ray.direction;
                return true;
            }

            return InteractionRaySource.TryGetWorldRay(ResolveRayOrigin(), out origin, out direction);
        }

        private bool IsHitRelevantToThisObject(Transform hitTransform)
        {
            return hitTransform == transform
                || hitTransform.IsChildOf(transform)
                || transform.IsChildOf(hitTransform);
        }

        static void MergeExtinguisherHoverLayers(ref LayerMask mask)
        {
            TryAddLayer(ref mask, "Estinguisher");
            TryAddLayer(ref mask, "Extinguisher");
            TryAddLayer(ref mask, "Outline");
            TryAddLayer(ref mask, "Default");
        }

        static void TryAddLayer(ref LayerMask mask, string layerName)
        {
            int id = LayerMask.NameToLayer(layerName);
            if (id < 0)
                return;

            mask = mask.value | (1 << id);
        }

        static bool IsTransformUnder(Transform leaf, Transform ancestor)
        {
            for (Transform t = leaf; t != null; t = t.parent)
            {
                if (t == ancestor)
                    return true;
            }

            return false;
        }

        private void EnsureOutlineBinding()
        {
            if (_outline != null)
                return;

            _outline = GetComponent<Outline>();
            if (_outline != null)
                return;

            ExtinguisherPickupItem pickup = GetComponentInParent<ExtinguisherPickupItem>();
            if (pickup != null)
            {
                _outline = pickup.GetComponent<Outline>();
                if (_outline == null)
                    _outline = pickup.GetComponentInChildren<Outline>(true);
            }

            if (_outline == null)
                _outline = GetComponentInChildren<Outline>(true);
        }

        private bool ValidateReferences()
        {
            if (!ValidateOutline())
                return false;

            if (TryGetHoverRay(out _, out _))
                return true;

            if (!_loggedMissingRayOrigin)
            {
                Debug.LogWarning("[HoverOutline] No ray origin or camera found. Assign PlayerCamera transform.", this);
                _loggedMissingRayOrigin = true;
            }

            return false;
        }

        private Transform ResolveRayOrigin()
        {
            if (_rayOrigin != null)
                return _rayOrigin;

            Camera camera = ResolveCamera();
            return camera != null ? camera.transform : null;
        }

        private Camera ResolveCamera()
        {
            if (_rayOrigin != null)
            {
                Camera assignedCamera = _rayOrigin.GetComponent<Camera>();
                if (assignedCamera == null)
                    assignedCamera = _rayOrigin.GetComponentInChildren<Camera>(true);

                if (assignedCamera != null)
                {
                    _resolvedCamera = assignedCamera;
                    return assignedCamera;
                }
            }

            if (_resolvedCamera != null && _resolvedCamera.isActiveAndEnabled)
                return _resolvedCamera;

            Camera main = Camera.main;
            if (main != null)
            {
                _resolvedCamera = main;
                return main;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                _resolvedCamera = candidate;
                return candidate;
            }

            return null;
        }

        private bool ValidateOutline()
        {
            EnsureOutlineBinding();

            if (_outline != null)
                return true;

            if (!_loggedMissingOutline)
            {
                Debug.LogWarning("[HoverOutline] No Quick Outline component assigned.", this);
                _loggedMissingOutline = true;
            }

            return false;
        }
    }
}

