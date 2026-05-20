using System;
using Obvious.Soap;
using UnityEngine;

namespace Woi.OfficeFire
{
    public sealed class PCSelectableInteractor : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Raised when Gameplay/Interact is pressed (E in PlayerInputActions).")]
        [SerializeField]
        private ScriptableEventNoParam interactInputEvent;

        [Header("Raycast")]
        [SerializeField]
        private Camera rayCamera;

        [SerializeField]
        private bool autoResolvePlayerCamera = true;

        [SerializeField]
        private string playerTag = "Player";

        [SerializeField]
        private float maxDistance = 5f;

        [SerializeField]
        private LayerMask selectionMask = ~0;

        [SerializeField]
        private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [SerializeField]
        private bool drawDebugRay = true;

        [SerializeField]
        private bool enableDebugLogs = true;

        private Camera _resolvedCamera;

        public void SetRayCamera(Camera camera)
        {
            rayCamera = camera;
            _resolvedCamera = camera;
        }

        private void OnEnable()
        {
            if (interactInputEvent == null)
            {
                return;
            }

            interactInputEvent.OnRaised += OnInteractInput;
        }

        private void OnDisable()
        {
            if (interactInputEvent == null)
            {
                return;
            }

            interactInputEvent.OnRaised -= OnInteractInput;
        }

        private void OnInteractInput()
        {
            if (enableDebugLogs)
            {
                Debug.Log("[PCSelectableInteractor] Interact input received (E).", this);
            }

            TrySelect();
        }

        public void TrySelect()
        {
            Camera cam = ResolveCamera();
            if (cam == null)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning(
                        "[PCSelectableInteractor] No camera found. Assign Ray Camera, tag player camera as MainCamera, or enable Auto Resolve Player Camera.",
                        this);
                }

                return;
            }

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, selectionMask, triggerInteraction);
            if (hits == null || hits.Length == 0)
            {
                if (enableDebugLogs)
                {
                    Debug.Log("[PCSelectableInteractor] Raycast hit nothing.", this);
                }

                return;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                {
                    continue;
                }

                ISelectable selectable = hit.collider.GetComponentInParent<ISelectable>();
                if (selectable == null)
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"[PCSelectableInteractor] Hit '{hit.collider.name}' (layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}) but no ISelectable.",
                            hit.collider);
                    }

                    continue;
                }

                if (!selectable.IsSelectable)
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"[PCSelectableInteractor] Hit '{hit.collider.name}' but ISelectable.IsSelectable is false.",
                            hit.collider);
                    }

                    continue;
                }

                SelectionContext context = new SelectionContext(SelectionSource.PC, cam.transform, ray, hit);
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[PCSelectableInteractor] Selected '{hit.collider.name}' on '{selectable}'.",
                        hit.collider);
                }

                selectable.Select(context);
                return;
            }

            if (enableDebugLogs)
            {
                Debug.Log("[PCSelectableInteractor] No ISelectable found along ray hits.", this);
            }
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
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(ray.origin, ray.direction * maxDistance);
        }
    }
}
