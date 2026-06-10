using System;
using System.Collections.Generic;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Casts a center-screen ray from the player camera each frame and calls
    /// <see cref="IHoverable.Hover"/> on every matching object in the hit hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/PC Hover Interactor")]
    public sealed class PCHoverInteractor : MonoBehaviour
    {
        [SerializeField]
        private Camera rayCamera;

        [SerializeField]
        private bool autoResolvePlayerCamera = true;

        [SerializeField]
        private string playerTag = "Player";

        [SerializeField]
        private float maxDistance = 5f;

        [SerializeField]
        private LayerMask hoverMask = ~0;

        [SerializeField]
        private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [SerializeField]
        private bool drawDebugRay = true;

        [SerializeField]
        private bool enableDebugLogs = true;

        private static readonly IHoverable[] EmptyHoverables = Array.Empty<IHoverable>();

        private Camera _resolvedCamera;
        private IHoverable[] _currentHoverables = EmptyHoverables;
        private bool _loggedMissingCamera;

        public IHoverable CurrentHoverable =>
            _currentHoverables.Length > 0 ? _currentHoverables[0] : null;

        public IReadOnlyList<IHoverable> CurrentHoverables => _currentHoverables;

        public ISelectable ResolveSelectableHoverTarget()
        {
            for (int i = 0; i < _currentHoverables.Length; i++)
            {
                if (_currentHoverables[i] is ISelectable selectable && selectable.IsSelectable)
                {
                    return selectable;
                }
            }

            return null;
        }

        public void SetRayCamera(Camera camera)
        {
            rayCamera = camera;
            _resolvedCamera = camera;
        }

        private void Update()
        {
            UpdateHoverTarget();
        }

        private void OnDisable()
        {
            ClearHover();
        }

        private void UpdateHoverTarget()
        {
            IHoverable[] hoverables = TryGetHoverablesFromRay();
            if (HoverablesEqual(_currentHoverables, hoverables))
            {
                return;
            }

            ClearHover();
            _currentHoverables = hoverables ?? EmptyHoverables;

            for (int i = 0; i < _currentHoverables.Length; i++)
            {
                IHoverable hoverable = _currentHoverables[i];
                if (hoverable == null)
                {
                    continue;
                }

                hoverable.Hover(true);

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[PCHoverInteractor] Hover started on '{GetHoverableDebugName(hoverable)}'.",
                        this);
                }
            }

            if (enableDebugLogs && _currentHoverables.Length == 0)
            {
                Debug.Log("[PCHoverInteractor] Hover cleared — ray missed IHoverable.", this);
            }
        }

        private static string GetHoverableDebugName(IHoverable hoverable)
        {
            if (hoverable is Component component)
            {
                return component.name;
            }

            return hoverable?.ToString() ?? "(null)";
        }

        private void ClearHover()
        {
            for (int i = 0; i < _currentHoverables.Length; i++)
            {
                IHoverable hoverable = _currentHoverables[i];
                if (hoverable == null)
                {
                    continue;
                }

                hoverable.Hover(false);
            }

            _currentHoverables = EmptyHoverables;
        }

        private IHoverable[] TryGetHoverablesFromRay()
        {
            Ray ray;
            Transform skipHierarchyRoot = null;
            if (!TryGetGameplayRay(out ray, out skipHierarchyRoot))
            {
                Camera cam = ResolveCamera();
                if (cam == null)
                {
                    if (!_loggedMissingCamera)
                    {
                        Debug.LogWarning(
                            "[PCHoverInteractor] No camera found. Assign Ray Camera or enable Auto Resolve Player Camera.",
                            this);
                        _loggedMissingCamera = true;
                    }

                    return EmptyHoverables;
                }

                _loggedMissingCamera = false;
                ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            }
            else
            {
                _loggedMissingCamera = false;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                maxDistance,
                hoverMask,
                IsVrMode() ? QueryTriggerInteraction.Collide : triggerInteraction);

            // To fix "Raycast misses when origin is inside collider" issue:
            Collider[] overlaps = Physics.OverlapSphere(
                ray.origin,
                0.15f,
                hoverMask,
                IsVrMode() ? QueryTriggerInteraction.Collide : triggerInteraction);

            if ((hits == null || hits.Length == 0) && (overlaps == null || overlaps.Length == 0))
            {
                return EmptyHoverables;
            }

            if (hits != null && hits.Length > 0)
            {
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit hit = hits[i];
                    if (hit.collider == null)
                        continue;

                    if (skipHierarchyRoot != null && hit.collider.transform.IsChildOf(skipHierarchyRoot))
                        continue;

                    IHoverable[] hoverables = CollectHoverablesFromCollider(hit.collider);
                    if (hoverables.Length == 0)
                        continue;

                    return hoverables;
                }
            }

            // Fallback for when camera is already inside the interaction trigger
            if (overlaps != null && overlaps.Length > 0)
            {
                for (int i = 0; i < overlaps.Length; i++)
                {
                    Collider col = overlaps[i];
                    if (col == null)
                        continue;

                    if (skipHierarchyRoot != null && col.transform.IsChildOf(skipHierarchyRoot))
                        continue;

                    IHoverable[] hoverables = CollectHoverablesFromCollider(col);
                    if (hoverables.Length == 0)
                        continue;

                    return hoverables;
                }
            }

            return EmptyHoverables;
        }

        private static bool TryGetGameplayRay(out Ray ray, out Transform skipHierarchyRoot)
        {
            ray = default;
            skipHierarchyRoot = null;

            if (!IsVrMode() || !FireVrGameplayInteractionRay.TryGetRay(out Vector3 origin, out Vector3 direction))
                return false;

            skipHierarchyRoot = FireVrGameplayInteractionRay.RegisteredRayOriginOrNull;
            ray = new Ray(origin, direction);
            return true;
        }

        private static bool IsVrMode()
        {
            return FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR;
        }

        private static IHoverable[] CollectHoverablesFromCollider(Collider collider)
        {
            if (collider == null)
            {
                return EmptyHoverables;
            }

            List<IHoverable> unique = new List<IHoverable>();
            AddUniqueHoverables(unique, collider.GetComponentsInParent<IHoverable>(true));
            AddUniqueHoverables(unique, collider.GetComponentsInChildren<IHoverable>(true));
            return unique.Count > 0 ? unique.ToArray() : EmptyHoverables;
        }

        private static void AddUniqueHoverables(List<IHoverable> unique, IHoverable[] hoverables)
        {
            if (hoverables == null)
            {
                return;
            }

            for (int i = 0; i < hoverables.Length; i++)
            {
                IHoverable hoverable = hoverables[i];
                if (hoverable == null)
                {
                    continue;
                }

                bool alreadyAdded = false;
                for (int j = 0; j < unique.Count; j++)
                {
                    if (ReferenceEquals(unique[j], hoverable))
                    {
                        alreadyAdded = true;
                        break;
                    }
                }

                if (!alreadyAdded)
                {
                    unique.Add(hoverable);
                }
            }
        }

        private static IHoverable[] DeduplicateHoverables(IHoverable[] hoverables)
        {
            List<IHoverable> unique = new List<IHoverable>(hoverables.Length);
            AddUniqueHoverables(unique, hoverables);
            return unique.Count > 0 ? unique.ToArray() : EmptyHoverables;
        }

        private static bool HoverablesEqual(IHoverable[] left, IHoverable[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (!ReferenceEquals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private Camera ResolveCamera()
        {
            if (rayCamera != null)
            {
                _resolvedCamera = rayCamera;
                return rayCamera;
            }

            if (_resolvedCamera != null && _resolvedCamera.isActiveAndEnabled)
            {
                return _resolvedCamera;
            }

            if (Camera.main != null)
            {
                _resolvedCamera = Camera.main;
                return _resolvedCamera;
            }

            if (!autoResolvePlayerCamera)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(playerTag))
            {
                GameObject player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null)
                {
                    Camera playerCamera = player.GetComponentInChildren<Camera>(true);
                    if (playerCamera != null)
                    {
                        _resolvedCamera = playerCamera;
                        return _resolvedCamera;
                    }
                }
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                {
                    continue;
                }

                _resolvedCamera = candidate;
                return _resolvedCamera;
            }

            return null;
        }

        private void OnDrawGizmos()
        {
            if (!drawDebugRay)
            {
                return;
            }

            Camera cam = ResolveCamera();
            if (cam == null)
            {
                return;
            }

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(ray.origin, ray.direction * maxDistance);
        }
    }
}
