using System;
using UnityEngine;

namespace Woi.OfficeFire
{
    public sealed class PCSelectableInteractor : MonoBehaviour
    {
        [SerializeField]
        private Camera rayCamera;

        [SerializeField]
        private float maxDistance = 5f;

        [SerializeField]
        private LayerMask selectionMask = ~0;

        [SerializeField]
        private KeyCode selectKey = KeyCode.E;

        [SerializeField]
        private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [SerializeField]
        private bool drawDebugRay = true;

        [SerializeField]
        private bool enableDebugLogs;

        private void Update()
        {
            if (!Input.GetKeyDown(selectKey))
            {
                return;
            }

            TrySelect();
        }

        private void TrySelect()
        {
            Camera cam = rayCamera != null ? rayCamera : Camera.main;
            if (cam == null)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning("[PCSelectableInteractor] No camera assigned and Camera.main is null.", this);
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

        private void OnDrawGizmos()
        {
            if (!drawDebugRay)
            {
                return;
            }

            Camera cam = rayCamera != null ? rayCamera : Camera.main;
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
