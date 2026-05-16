using System;
using UnityEngine;
using Woi.Equipment;

namespace Woi.Game
{
    /// <summary>
    /// Presentation-only hover outline for interactable objects (PC: camera ray; VR: same aim as
    /// ExtinguisherHoverTransformRaycaster when registered via FireVrGameplayInteractionRay).
    /// </summary>
    [AddComponentMenu("Woi/Feedback/Hover Outline")]
    public sealed class HoverOutline : MonoBehaviour
    {
        [SerializeField] private Outline _outline;
        [Tooltip("PC: player camera. VR: same transform as ExtinguisherHoverTransformRaycaster (fallback if VR ray is not registered).")]
        [SerializeField] private Transform _rayOrigin;
        [SerializeField, Min(0f)] private float _hoverRange = 3f;
        [SerializeField] private LayerMask _hoverLayerMask = Physics.AllLayers;
        [SerializeField] private bool _enableOnStart;

        private bool _isHovered;
        private bool _loggedMissingOutline;
        private bool _loggedMissingRayOrigin;

        private void Awake()
        {
            ApplyInitialState();
        }

        private void OnEnable()
        {
            ApplyInitialState();
        }

        private void Update()
        {
            if (!ValidateReferences())
                return;

            bool hovered = IsRayHoveringThisObject();
            if (_isHovered == hovered)
                return;

            _isHovered = hovered;
            _outline.enabled = _isHovered;
        }

        private void ApplyInitialState()
        {
            if (!ValidateOutline())
                return;

            _outline.enabled = _enableOnStart;
            _isHovered = _enableOnStart;
        }

        public void ResetHover()
        {
            _isHovered = false;

            if (_outline != null)
                _outline.enabled = false;
        }

        private bool IsRayHoveringThisObject()
        {
            if (!InteractionRaySource.TryGetWorldRay(_rayOrigin, out Vector3 origin, out Vector3 dir))
                return false;

            RaycastHit[] hits = Physics.RaycastAll(
                    origin,
                    dir,
                    _hoverRange,
                    _hoverLayerMask,
                    QueryTriggerInteraction.Collide);

            if (hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Transform vrSkipRoot = FireVrGameplayInteractionRay.RegisteredRayOriginOrNull;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                Transform hitTransform = hit.collider.transform;
                if (vrSkipRoot != null && IsTransformUnder(hitTransform, vrSkipRoot))
                    continue;

                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                    return true;
            }

            return false;
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

        private bool ValidateReferences()
        {
            if (!ValidateOutline())
                return false;

            if (_rayOrigin != null)
                return true;

            if (!_loggedMissingRayOrigin)
            {
                Debug.LogWarning("[HoverOutline] No ray origin assigned. Assign PlayerCamera transform.", this);
                _loggedMissingRayOrigin = true;
            }

            return false;
        }

        private bool ValidateOutline()
        {
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
