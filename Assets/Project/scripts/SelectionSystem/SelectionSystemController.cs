using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Woi.SelectionSystem
{
    public class SelectionSystemController
    {
        private readonly Camera mainCamera;

        public SelectionSystemController(Camera mainCamera)
        {
            this.mainCamera = mainCamera;
        }

        public ISelectable SelectObject()
        {
            return RaycastFirstSelectable(out _);
        }

        public ISelectable RaycastFirstSelectable(
            out RaycastHit hit,
            float maxDistance = Mathf.Infinity,
            int layerMask = Physics.DefaultRaycastLayers)
        {
            hit = default;
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                maxDistance,
                layerMask,
                QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0)
            {
                return null;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                ISelectable selectable = collider.GetComponentInParent<ISelectable>();
                if (selectable == null)
                {
                    continue;
                }

                hit = hits[i];
                return selectable;
            }

            return null;
        }
    }

    public interface ISelectable
    {
        void Select();
        void Deselect();
    }

    public class SelectionInputController
    {
        private readonly SelectionSystemController selectionSystemController;
        private InputAction mouseLeftClick;
        
        public SelectionInputController(SelectionSystemController selectionSystemController, InputAction mouseLeftClick)
        {
            this.selectionSystemController = selectionSystemController;
            this.mouseLeftClick = mouseLeftClick;
        }

        public void Enable()
        {
            mouseLeftClick.Enable();
            mouseLeftClick.performed += OnMouseLeftClickPerformed;
        }

        public void Disable()
        {
            mouseLeftClick.Disable();
            mouseLeftClick.performed -= OnMouseLeftClickPerformed;
        }

        private void OnMouseLeftClickPerformed(InputAction.CallbackContext context)
        {
            Debug.Log("Mouse left click pressed");
            var selected = selectionSystemController.SelectObject();
            selected?.Select();
        }
    }
}
