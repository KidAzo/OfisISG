using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Woi.OfficeFire
{
    /// <summary>
    /// VR: Sağ veya sol kontrolcü trigger'ına basıldığında o kontrolcüden ileri bir ışın atar
    /// ve isabet eden <see cref="ISelectable"/> öğesini (ör. <c>SelectableDoor</c>) seçer.
    /// Waste/gameplay <c>SelectionSystem</c>'den bağımsızdır; her el kendi ışınını kullanır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VrSelectableInteractor : MonoBehaviour
    {
        [Header("Kontrolcüler (ışın kaynağı)")]
        [Tooltip("Boşsa sahnede ismi 'Right' + 'Controller' içeren obje aranır.")]
        [SerializeField] private Transform rightControllerRayOrigin;

        [Tooltip("Boşsa sahnede ismi 'Left' + 'Controller' içeren obje aranır.")]
        [SerializeField] private Transform leftControllerRayOrigin;

        [SerializeField] private bool autoFindControllers = true;
        [SerializeField] private string rightControllerNameContains = "Right";
        [SerializeField] private string leftControllerNameContains = "Left";

        [Header("Girdi")]
        [SerializeField] private bool useRightTrigger = true;
        [SerializeField] private bool useLeftTrigger = true;

        [Header("Raycast")]
        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private LayerMask selectionMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        private InputAction rightTriggerAction;
        private InputAction leftTriggerAction;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

        private void OnEnable()
        {
            ResolveControllers();

            if (useRightTrigger)
            {
                rightTriggerAction = new InputAction(
                    "VrSelectRightTrigger",
                    InputActionType.Button,
                    "<XRController>{RightHand}/{TriggerButton}");
                rightTriggerAction.performed += OnRightTriggerPerformed;
                rightTriggerAction.Enable();
            }

            if (useLeftTrigger)
            {
                leftTriggerAction = new InputAction(
                    "VrSelectLeftTrigger",
                    InputActionType.Button,
                    "<XRController>{LeftHand}/{TriggerButton}");
                leftTriggerAction.performed += OnLeftTriggerPerformed;
                leftTriggerAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (rightTriggerAction != null)
            {
                rightTriggerAction.performed -= OnRightTriggerPerformed;
                rightTriggerAction.Disable();
                rightTriggerAction.Dispose();
                rightTriggerAction = null;
            }

            if (leftTriggerAction != null)
            {
                leftTriggerAction.performed -= OnLeftTriggerPerformed;
                leftTriggerAction.Disable();
                leftTriggerAction.Dispose();
                leftTriggerAction = null;
            }
        }

        private void OnRightTriggerPerformed(InputAction.CallbackContext _)
        {
            TrySelectFromController(ResolveRightOrigin());
        }

        private void OnLeftTriggerPerformed(InputAction.CallbackContext _)
        {
            TrySelectFromController(ResolveLeftOrigin());
        }

        private void ResolveControllers()
        {
            ResolveRightOrigin();
            ResolveLeftOrigin();
        }

        private Transform ResolveRightOrigin()
        {
            if (rightControllerRayOrigin == null && autoFindControllers)
                rightControllerRayOrigin = FindControllerByName(rightControllerNameContains);
            return rightControllerRayOrigin;
        }

        private Transform ResolveLeftOrigin()
        {
            if (leftControllerRayOrigin == null && autoFindControllers)
                leftControllerRayOrigin = FindControllerByName(leftControllerNameContains);
            return leftControllerRayOrigin;
        }

        private void TrySelectFromController(Transform origin)
        {
            if (origin == null)
                return;

            Vector3 direction = origin.forward;
            if (direction.sqrMagnitude < 1e-8f)
                return;

            direction.Normalize();
            Vector3 start = origin.position;
            var ray = new Ray(start, direction);

            int hitCount = Physics.RaycastNonAlloc(ray, HitBuffer, maxDistance, selectionMask, triggerInteraction);
            if (hitCount <= 0)
                return;

            SortHitsByDistance(hitCount);

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = HitBuffer[i].collider;
                if (collider == null)
                    continue;

                ISelectable selectable = FindSelectable(collider);
                if (selectable == null || !selectable.IsSelectable)
                    continue;

                selectable.Select(new SelectionContext(SelectionSource.VRRay, origin, ray, HitBuffer[i]));
                return;
            }
        }

        private static void SortHitsByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit key = HitBuffer[i];
                int j = i - 1;
                while (j >= 0 && HitBuffer[j].distance > key.distance)
                {
                    HitBuffer[j + 1] = HitBuffer[j];
                    j--;
                }

                HitBuffer[j + 1] = key;
            }
        }

        private static ISelectable FindSelectable(Collider collider)
        {
            ISelectable selectable = collider.GetComponentInParent<ISelectable>();
            if (selectable != null)
                return selectable;

            return collider.GetComponentInChildren<ISelectable>();
        }

        private static Transform FindControllerByName(string nameContains)
        {
            if (string.IsNullOrWhiteSpace(nameContains))
                return null;

            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go == null || !go.scene.IsValid())
                    continue;

                if (go.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0
                    && go.name.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0)
                    return go.transform;
            }

            return null;
        }
    }
}
