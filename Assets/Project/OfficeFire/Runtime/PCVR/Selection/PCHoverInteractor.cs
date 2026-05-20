using System;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Casts a center-screen ray from the player camera each frame and calls
    /// <see cref="IHoverable.Hover"/> on the hit object.
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

        private Camera _resolvedCamera;
        private IHoverable _currentHoverable;
        private bool _loggedMissingCamera;

        public IHoverable CurrentHoverable => _currentHoverable;

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
            IHoverable hoverable = TryGetHoverableFromRay();
            if (ReferenceEquals(hoverable, _currentHoverable))
            {
                return;
            }

            ClearHover();
            _currentHoverable = hoverable;

            if (_currentHoverable != null)
            {
                _currentHoverable.Hover(true);

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[PCHoverInteractor] Hover started on '{GetHoverableDebugName(_currentHoverable)}'.",
                        this);
                }
            }
            else if (enableDebugLogs)
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
            if (_currentHoverable == null)
            {
                return;
            }

            _currentHoverable.Hover(false);
            _currentHoverable = null;
        }

        private IHoverable TryGetHoverableFromRay()
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

                return null;
            }

            _loggedMissingCamera = false;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, hoverMask, triggerInteraction);
            if (hits == null || hits.Length == 0)
            {
                return null;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                {
                    continue;
                }

                IHoverable hoverable = hit.collider.GetComponentInParent<IHoverable>();
                if (hoverable != null)
                {
                    return hoverable;
                }
            }

            return null;
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
