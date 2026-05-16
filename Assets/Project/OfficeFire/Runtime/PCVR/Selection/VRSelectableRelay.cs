using UnityEngine;

namespace Woi.OfficeFire
{
    public sealed class VRSelectableRelay : MonoBehaviour
    {
        [SerializeField]
        private SelectionSource source = SelectionSource.VRRay;

        [SerializeField]
        private Transform interactorTransform;

        public void SelectFromRaycastHit(RaycastHit hit)
        {
            if (hit.collider == null)
            {
                return;
            }

            ISelectable selectable = hit.collider.GetComponentInParent<ISelectable>();
            if (selectable == null || !selectable.IsSelectable)
            {
                return;
            }

            Ray ray;
            if (interactorTransform != null)
            {
                ray = new Ray(interactorTransform.position, interactorTransform.forward);
            }
            else
            {
                ray = new Ray(hit.point, hit.normal);
            }

            selectable.Select(new SelectionContext(source, interactorTransform, ray, hit));
        }

        public void SelectTarget(Component target)
        {
            if (target == null)
            {
                return;
            }

            ISelectable selectable = target.GetComponentInParent<ISelectable>();
            if (selectable == null || !selectable.IsSelectable)
            {
                return;
            }

            Ray ray;
            if (interactorTransform != null)
            {
                ray = new Ray(interactorTransform.position, interactorTransform.forward);
            }
            else
            {
                ray = new Ray(target.transform.position, target.transform.forward);
            }

            selectable.Select(new SelectionContext(source, interactorTransform, ray, default));
        }
    }
}
