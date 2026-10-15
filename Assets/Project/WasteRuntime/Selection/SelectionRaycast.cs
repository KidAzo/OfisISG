using System;
using UnityEngine;
using OfficeSelectable = Woi.OfficeFire.ISelectable;
using WasteSelectable = Woi.SelectionSystem.ISelectable;

namespace Woi.SelectionSystem
{
    internal static class SelectionRaycast
    {
        /// <summary>
        /// Nearest hit carrying either selectable interface wins, so a door in front occludes waste behind it.
        /// Colliders with neither interface are passed through, matching <see cref="RaycastFirstSelectable"/>.
        /// </summary>
        public static bool RaycastFirstInteractable(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            LayerMask layerMask,
            QueryTriggerInteraction triggerInteraction,
            Transform skipHierarchyRoot,
            out RaycastHit hit,
            out WasteSelectable wasteSelectable,
            out OfficeSelectable officeSelectable)
        {
            hit = default;
            wasteSelectable = null;
            officeSelectable = null;
            if (direction.sqrMagnitude < 1e-8f)
                return false;

            direction.Normalize();

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                maxDistance,
                layerMask,
                triggerInteraction);

            if (hits == null || hits.Length == 0)
                return false;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null)
                    continue;

                if (skipHierarchyRoot != null && collider.transform.IsChildOf(skipHierarchyRoot))
                    continue;

                wasteSelectable = collider.GetComponentInParent<WasteSelectable>();
                if (wasteSelectable == null)
                {
                    officeSelectable = collider.GetComponentInParent<OfficeSelectable>()
                        ?? collider.GetComponentInChildren<OfficeSelectable>();
                }

                if (wasteSelectable == null && officeSelectable == null)
                    continue;

                hit = hits[i];
                return true;
            }

            return false;
        }

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
