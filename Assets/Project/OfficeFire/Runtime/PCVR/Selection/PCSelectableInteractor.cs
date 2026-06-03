using System;
using System.Collections.Generic;
using Obvious.Soap;
using UnityEngine;
using Woi.InputSystem;

namespace Woi.OfficeFire
{
    public sealed class PCSelectableInteractor : MonoBehaviour, ISoapInteractInputListener
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
        private PCHoverInteractor _hoverInteractor;

        public void SetRayCamera(Camera camera)
        {
            rayCamera = camera;
            _resolvedCamera = camera;
        }

        private void OnEnable()
        {
            SubscribeInteract();
        }

        private void OnDisable()
        {
            UnsubscribeInteract();
        }

        public bool IsListeningToDifferentInteractEvent(ScriptableEventNoParam liveInteractEvent) =>
            interactInputEvent != null
            && liveInteractEvent != null
            && !ReferenceEquals(interactInputEvent, liveInteractEvent);

        public void RebindInteractInputEvent(ScriptableEventNoParam liveInteractEvent)
        {
            UnsubscribeInteract();
            interactInputEvent = liveInteractEvent;
            if (isActiveAndEnabled)
            {
                SubscribeInteract();
            }
        }

        private void SubscribeInteract()
        {
            if (interactInputEvent == null)
            {
                return;
            }

            interactInputEvent.OnRaised += OnInteractInput;
        }

        private void UnsubscribeInteract()
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
            if (TrySelectHoveredTarget())
            {
                return;
            }

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

                ISelectable selectable = FindSelectable(hit.collider);
                if (selectable == null)
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"[PCSelectableInteractor] Hit '{hit.collider.name}' (layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}) but no selectable ISelectable.",
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

        private bool TrySelectHoveredTarget()
        {
            PCHoverInteractor hoverInteractor = ResolveHoverInteractor();
            if (hoverInteractor == null)
            {
                return false;
            }

            IHoverable hoverable = hoverInteractor.CurrentHoverable;
            ISelectable selectable = hoverInteractor.ResolveSelectableHoverTarget();
            if (selectable == null || !selectable.IsSelectable)
            {
                if (enableDebugLogs && hoverable != null)
                {
                    Debug.Log(
                        $"[PCSelectableInteractor] Hovered '{GetDebugName(hoverable)}' is not selectable — E ignored.",
                        this);
                }

                return false;
            }

            if (selectable is not IHoverable selectableHoverable)
            {
                return false;
            }

            hoverable = selectableHoverable;

            Camera cam = ResolveCamera();
            if (cam == null)
            {
                return false;
            }

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit = default;
            if (hoverable is Component hoverComponent)
            {
                Collider[] colliders = hoverComponent.GetComponentsInParent<Collider>(true);
                float bestDistance = float.MaxValue;
                bool foundHit = false;

                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider collider = colliders[i];
                    if (collider == null || !collider.Raycast(ray, out RaycastHit candidate, maxDistance))
                    {
                        continue;
                    }

                    if (candidate.distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = candidate.distance;
                    hit = candidate;
                    foundHit = true;
                }

                if (!foundHit)
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"[PCSelectableInteractor] Hovered '{hoverComponent.name}' has no ray hit — E ignored.",
                            this);
                    }

                    return false;
                }
            }

            SelectionContext context = new SelectionContext(SelectionSource.PC, cam.transform, ray, hit);
            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[PCSelectableInteractor] Selected hovered '{GetDebugName(hoverable)}'.",
                    hoverable as Component);
            }

            selectable.Select(context);
            return true;
        }

        private static ISelectable FindSelectable(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            List<ISelectable> candidates = new List<ISelectable>();
            AddSelectables(candidates, collider.GetComponentsInParent<ISelectable>(true));
            AddSelectables(candidates, collider.GetComponentsInChildren<ISelectable>(true));

            if (candidates.Count == 0)
            {
                return null;
            }

            ISelectable fallback = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                ISelectable candidate = candidates[i];
                if (candidate == null || !candidate.IsSelectable)
                {
                    continue;
                }

                if (candidate is IHoverable)
                {
                    return candidate;
                }

                fallback ??= candidate;
            }

            return fallback;
        }

        private static void AddSelectables(List<ISelectable> candidates, ISelectable[] selectables)
        {
            if (selectables == null)
            {
                return;
            }

            for (int i = 0; i < selectables.Length; i++)
            {
                ISelectable selectable = selectables[i];
                if (selectable == null)
                {
                    continue;
                }

                bool exists = false;
                for (int j = 0; j < candidates.Count; j++)
                {
                    if (ReferenceEquals(candidates[j], selectable))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    candidates.Add(selectable);
                }
            }
        }

        private PCHoverInteractor ResolveHoverInteractor()
        {
            if (_hoverInteractor != null)
            {
                return _hoverInteractor;
            }

            _hoverInteractor = FindFirstObjectByType<PCHoverInteractor>(FindObjectsInactive.Include);
            return _hoverInteractor;
        }

        private static string GetDebugName(IHoverable hoverable)
        {
            return hoverable is Component component ? component.name : hoverable.ToString();
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
