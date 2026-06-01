using System;
using Obvious.Soap;
using UnityEngine;
using Woi.SelectionSystem;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// VR: right trigger (XR Interact) raycasts waste items and calls <see cref="WasteController.Select"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteVrWastePicker : MonoBehaviour
    {
        private const string InteractEventPath =
            "Packages/com.woi.module.fire/Runtime/InputSystem/InputsSO/InputEvents/onInteractInput.asset";

        [SerializeField] private ScriptableEventNoParam interactInputEvent;
        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private LayerMask selectionMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        private WasteResultScreenController resultScreen;
        private WasteSelectionMenu selectionMenu;

        private void Awake()
        {
            ResolveInteractEvent();
            resultScreen = GetComponent<WasteResultScreenController>();
            selectionMenu = GetComponent<WasteSelectionMenu>();
        }

        private void OnEnable()
        {
            if (!WasteCollectionPlatform.IsVR)
            {
                enabled = false;
                return;
            }

            if (interactInputEvent != null)
                interactInputEvent.OnRaised += OnInteractInput;
        }

        private void OnDisable()
        {
            if (interactInputEvent != null)
                interactInputEvent.OnRaised -= OnInteractInput;
        }

        private void OnInteractInput()
        {
            if (!CanPickWaste())
                return;

            if (!TryGetInteractionRay(out Vector3 origin, out Vector3 direction))
                return;

            if (!TryRaycastWaste(origin, direction, out ISelectable selectable))
                return;

            selectable.Select();
        }

        private bool CanPickWaste()
        {
            if (resultScreen != null && resultScreen.IsVisible)
                return false;

            if (selectionMenu != null && selectionMenu.IsVisible)
                return false;

            return true;
        }

        private static bool TryGetInteractionRay(out Vector3 origin, out Vector3 direction)
        {
            if (FireVrGameplayInteractionRay.TryGetRay(out origin, out direction))
                return true;

            Camera cam = Camera.main;
            if (cam == null)
                return false;

            origin = cam.transform.position;
            direction = cam.transform.forward;
            return direction.sqrMagnitude > 1e-8f;
        }

        private bool TryRaycastWaste(Vector3 origin, Vector3 direction, out ISelectable selectable)
        {
            selectable = null;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                maxDistance,
                selectionMask,
                triggerInteraction);

            if (hits == null || hits.Length == 0)
                return false;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Transform skipRoot = FireVrGameplayInteractionRay.RegisteredRayOriginOrNull;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null)
                    continue;

                if (skipRoot != null && collider.transform.IsChildOf(skipRoot))
                    continue;

                selectable = collider.GetComponentInParent<ISelectable>();
                if (selectable != null)
                    return true;
            }

            return false;
        }

        private void ResolveInteractEvent()
        {
            if (interactInputEvent != null)
                return;

#if UNITY_EDITOR
            interactInputEvent =
                UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableEventNoParam>(InteractEventPath);
#endif
        }
    }
}
