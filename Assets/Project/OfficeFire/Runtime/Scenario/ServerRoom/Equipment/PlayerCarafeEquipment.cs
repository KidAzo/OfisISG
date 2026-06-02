using System;
using System.Collections.Generic;
using UnityEngine;
using Woi.Equipment;
using Woi.InputSystem;

namespace Woi.OfficeFire
{
    /// <summary>
    /// PC pickup for <see cref="CarafePickupItem"/> using the same E / crosshair ray as the fire blanket.
    /// Mirrors <see cref="PlayerFireBlanketEquipment"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Player Carafe Equipment")]
    public sealed class PlayerCarafeEquipment : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField]
        private GameplayInputContext inputContext;

        [Header("Equip Anchor")]
        [SerializeField]
        private Transform equipAnchor;

        [Header("Pickup Detection")]
        [SerializeField]
        private Transform interactionRayOrigin;

        [SerializeField]
        private Camera playerCamera;

        [SerializeField, Min(0f)]
        private float pickupRange = 3f;

        [SerializeField]
        private LayerMask pickupLayerMask = Physics.AllLayers;

        [SerializeField]
        private bool preferHoveredPickupTarget = true;

        [SerializeField]
        private bool enableDebugLogs;

        public CarafePickupItem CurrentItem { get; private set; }

        public event Action<CarafePickupItem> OnCarafeChanged;

        public Transform EquipAnchor => equipAnchor;

        public bool TryGetCrosshairRay(out Ray ray)
        {
            ray = default;
            Camera camera = ResolvePickupCamera();
            if (camera == null)
            {
                return false;
            }

            ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            return true;
        }

        private void OnEnable()
        {
            TryAutoResolveReferences();
            BindInput();
        }

        private void Start()
        {
            TryAutoResolveReferences();
            BindInput();
        }

        private void OnDisable()
        {
            UnbindInput();
        }

        public void NotifyConsumed(CarafePickupItem item)
        {
            if (item == null || CurrentItem != item)
            {
                return;
            }

            CurrentItem = null;
            OnCarafeChanged?.Invoke(null);
        }

        public void NotifyDropped(CarafePickupItem item)
        {
            if (item == null || CurrentItem != item)
            {
                return;
            }

            CurrentItem = null;
            OnCarafeChanged?.Invoke(null);
        }

        private void BindInput()
        {
            if (inputContext == null)
            {
                return;
            }

            if (inputContext.InteractEvent != null)
            {
                inputContext.InteractEvent.OnRaised -= HandleInteractInput;
                inputContext.InteractEvent.OnRaised += HandleInteractInput;
            }

            if (inputContext.EquipEvent != null && inputContext.EquipEvent != inputContext.InteractEvent)
            {
                inputContext.EquipEvent.OnRaised -= HandleInteractInput;
                inputContext.EquipEvent.OnRaised += HandleInteractInput;
            }
        }

        private void UnbindInput()
        {
            if (inputContext == null)
            {
                return;
            }

            if (inputContext.InteractEvent != null)
            {
                inputContext.InteractEvent.OnRaised -= HandleInteractInput;
            }

            if (inputContext.EquipEvent != null && inputContext.EquipEvent != inputContext.InteractEvent)
            {
                inputContext.EquipEvent.OnRaised -= HandleInteractInput;
            }
        }

        private void HandleInteractInput()
        {
            if (FireVrGameplayInteractionRay.RegisteredRayOriginOrNull != null)
            {
                return;
            }

            if (CurrentItem != null)
            {
                return;
            }

            if (TryGetEquippedExtinguisher() != null)
            {
                Log("E ignored — drop the extinguisher before picking up the carafe.");
                return;
            }

            if (TryGetEquippedBlanket() != null)
            {
                Log("E ignored — drop the blanket before picking up the carafe.");
                return;
            }

            CarafePickupItem candidate = ResolvePickupCandidate();
            if (candidate == null)
            {
                Log("E ignored — no CarafePickupItem under crosshair.");
                return;
            }

            PerformEquip(candidate);
        }

        private ExtinguisherPickupItem TryGetEquippedExtinguisher()
        {
            PlayerExtinguisherEquipment extinguisherEquipment = GetComponent<PlayerExtinguisherEquipment>();
            return extinguisherEquipment != null ? extinguisherEquipment.CurrentItem : null;
        }

        private FireBlanketPickupItem TryGetEquippedBlanket()
        {
            PlayerFireBlanketEquipment blanketEquipment = GetComponent<PlayerFireBlanketEquipment>();
            return blanketEquipment != null ? blanketEquipment.CurrentItem : null;
        }

        private CarafePickupItem ResolvePickupCandidate()
        {
            if (preferHoveredPickupTarget)
            {
                CarafePickupItem hovered = TryGetHoveredCarafe();
                if (hovered != null)
                {
                    return hovered;
                }
            }

            return RaycastForItem();
        }

        private CarafePickupItem TryGetHoveredCarafe()
        {
            PCHoverInteractor hoverInteractor = GetComponent<PCHoverInteractor>();
            if (hoverInteractor == null)
            {
                hoverInteractor = FindFirstObjectByType<PCHoverInteractor>();
            }

            if (hoverInteractor == null)
            {
                return null;
            }

            IReadOnlyList<IHoverable> hoverables = hoverInteractor.CurrentHoverables;
            for (int i = 0; i < hoverables.Count; i++)
            {
                if (hoverables[i] is CarafePickupItem item
                    && !item.IsEquipped
                    && !item.IsConsumed)
                {
                    return item;
                }
            }

            return null;
        }

        private void PerformEquip(CarafePickupItem item)
        {
            if (equipAnchor == null)
            {
                Debug.LogError("[PlayerCarafeEquipment] Equip anchor is not assigned.", this);
                return;
            }

            if (!item.EquipToPlayer(equipAnchor))
            {
                return;
            }

            CurrentItem = item;
            OnCarafeChanged?.Invoke(item);
        }

        private CarafePickupItem RaycastForItem()
        {
            if (!TryGetPickupRay(out Vector3 origin, out Vector3 direction))
            {
                return null;
            }

            if (!float.IsFinite(origin.x) || !float.IsFinite(origin.y) || !float.IsFinite(origin.z)
                || !float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(direction.z)
                || direction.sqrMagnitude < 1e-10f)
            {
                return null;
            }

            if (!float.IsFinite(pickupRange) || pickupRange <= 0f)
            {
                return null;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                pickupRange,
                pickupLayerMask,
                QueryTriggerInteraction.Collide);

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                CarafePickupItem item = collider.GetComponentInParent<CarafePickupItem>();
                if (item == null || item.IsEquipped || item.IsConsumed)
                {
                    continue;
                }

                return item;
            }

            return null;
        }

        private bool TryGetPickupRay(out Vector3 origin, out Vector3 direction)
        {
            Camera camera = ResolvePickupCamera();
            if (camera != null)
            {
                Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                origin = ray.origin;
                direction = ray.direction;
                return true;
            }

            Transform rayOrigin = ResolveInteractionRayOriginTransform();
            return InteractionRaySource.TryGetWorldRay(rayOrigin, out origin, out direction);
        }

        private Camera ResolvePickupCamera()
        {
            if (playerCamera != null)
            {
                return playerCamera;
            }

            Camera childCamera = GetComponentInChildren<Camera>(true);
            if (childCamera != null)
            {
                return childCamera;
            }

            return Camera.main;
        }

        private Transform ResolveInteractionRayOriginTransform()
        {
            if (interactionRayOrigin != null)
            {
                return interactionRayOrigin;
            }

            if (playerCamera != null)
            {
                return playerCamera.transform;
            }

            Camera childCamera = GetComponentInChildren<Camera>(true);
            if (childCamera != null)
            {
                return childCamera.transform;
            }

            return Camera.main != null ? Camera.main.transform : null;
        }

        private void TryAutoResolveReferences()
        {
            PlayerFireBlanketEquipment blanketEquipment = GetComponent<PlayerFireBlanketEquipment>();
            PlayerExtinguisherEquipment extinguisherEquipment = GetComponent<PlayerExtinguisherEquipment>();

            if (equipAnchor == null)
            {
                if (blanketEquipment != null && blanketEquipment.EquipAnchor != null)
                {
                    equipAnchor = blanketEquipment.EquipAnchor;
                }
                else if (extinguisherEquipment != null)
                {
                    equipAnchor = extinguisherEquipment.EquipAnchor;
                }
            }

            if (playerCamera == null)
            {
                if (extinguisherEquipment != null && extinguisherEquipment.PlayerCamera != null)
                {
                    playerCamera = extinguisherEquipment.PlayerCamera;
                }
                else
                {
                    playerCamera = GetComponentInChildren<Camera>(true);
                }
            }

            if (inputContext == null && extinguisherEquipment != null)
            {
                inputContext = extinguisherEquipment.InputContext;
            }
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.Log($"[PlayerCarafeEquipment] {message}", this);
        }
    }
}
