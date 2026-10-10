using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Woi.SelectionSystem
{
    public class SelectionSystemController
    {
        private readonly Camera mainCamera;
        private readonly float maxDistance;
        private readonly LayerMask layerMask;
        private readonly QueryTriggerInteraction triggerInteraction;

        public SelectionSystemController(
            Camera mainCamera,
            float maxDistance = Mathf.Infinity,
            LayerMask layerMask = default,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide)
        {
            this.mainCamera = mainCamera;
            this.maxDistance = maxDistance;
            this.layerMask = layerMask.value == 0 ? Physics.DefaultRaycastLayers : layerMask;
            this.triggerInteraction = triggerInteraction;
        }

        public ISelectable SelectFromMouse()
        {
            if (mainCamera == null)
                return null;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return null;

            Vector2 screenPosition = mouse.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            return RaycastFirstSelectable(ray.origin, ray.direction, out _, skipHierarchyRoot: null);
        }

        public ISelectable SelectFromWorldRay(Vector3 origin, Vector3 direction, Transform skipHierarchyRoot)
        {
            return RaycastFirstSelectable(origin, direction, out _, skipHierarchyRoot);
        }

        public ISelectable RaycastFirstSelectable(
            Vector3 origin,
            Vector3 direction,
            out RaycastHit hit,
            Transform skipHierarchyRoot)
        {
            return SelectionRaycast.RaycastFirstSelectable(
                origin,
                direction,
                out hit,
                maxDistance,
                layerMask,
                triggerInteraction,
                skipHierarchyRoot);
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
        private readonly InputAction mouseLeftClick;
        private readonly Func<bool> canSelect;
        private bool listening;

        public SelectionInputController(
            SelectionSystemController selectionSystemController,
            InputAction mouseLeftClick,
            Func<bool> canSelect = null)
        {
            this.selectionSystemController = selectionSystemController;
            this.mouseLeftClick = mouseLeftClick;
            this.canSelect = canSelect;
        }

        public void Enable()
        {
            if (mouseLeftClick == null)
                return;

            mouseLeftClick.Enable();
            if (listening)
                return;

            mouseLeftClick.performed += OnMouseLeftClickPerformed;
            listening = true;
        }

        public void Disable()
        {
            if (mouseLeftClick == null)
                return;

            if (listening)
            {
                mouseLeftClick.performed -= OnMouseLeftClickPerformed;
                listening = false;
            }

            mouseLeftClick.Disable();
        }

        private void OnMouseLeftClickPerformed(InputAction.CallbackContext context)
        {
            if (canSelect != null && !canSelect())
                return;

            ISelectable selected = selectionSystemController.SelectFromMouse();
            selected?.Select();
        }
    }
}
