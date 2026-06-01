using System;
using UnityEngine;

namespace Woi.SelectionSystem
{
    internal static class SelectionRaycast
    {
        public static ISelectable RaycastFirstSelectable(
            Vector3 origin,
            Vector3 direction,
            out RaycastHit hit,
            float maxDistance,
            LayerMask layerMask,
            QueryTriggerInteraction triggerInteraction,
            Transform skipHierarchyRoot)
        {
            hit = default;
            if (direction.sqrMagnitude < 1e-8f)
                return null;

            direction.Normalize();

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                maxDistance,
                layerMask,
                triggerInteraction);

            if (hits == null || hits.Length == 0)
                return null;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null)
                    continue;

                if (skipHierarchyRoot != null && collider.transform.IsChildOf(skipHierarchyRoot))
                    continue;

                ISelectable selectable = collider.GetComponentInParent<ISelectable>();
                if (selectable == null)
                    continue;

                hit = hits[i];
                return selectable;
            }

            return null;
        }
    }
}
