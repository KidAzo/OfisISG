using System;
using FireExtinguisher.Core;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Events;
using Woi.Events;
using Woi.InputSystem;

namespace Woi.Equipment
{
    /// <summary>
    /// Game-layer equipment manager that lives on the player.
    /// Listens to the Woi SO input events for interact and drop,
    /// performs a proximity search for pickupable extinguishers,
    /// and delegates the actual equip / drop transition to
    /// <see cref="ExtinguisherPickupItem"/>.
    /// </summary>
    /// <remarks>
    /// Only one extinguisher can be equipped at a time.
    /// Spray logic, capacity, and hold-state gating all remain in the
    /// core framework components on the extinguisher itself.
    /// </remarks>
    [AddComponentMenu("Woi/Equipment/Player Extinguisher Equipment")]
    public sealed class PlayerExtinguisherEquipment : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Input")]
        [Tooltip("The gameplay input context ScriptableObject that owns the interact and drop events.")]
        [SerializeField] private GameplayInputContext _inputContext;

        [Header("SO Events")]
        [Tooltip("Raised when the equipped extinguisher changes. Carries name, capacity, and duration.")]
        [SerializeField] private ScriptableEventExtinguisherChangedEvent _extinguisherChangedEvent;

        [Tooltip("Raised immediately after an extinguisher is successfully equipped.")]
        [SerializeField] private ScriptableEventNoParam _onEquipEvent;

        [Tooltip("Raised after a real drop (not when swap-only drop suppressed). Empty-hand drop input does nothing.")]
        [SerializeField] private ScriptableEventNoParam _onDropEvent;

        [Tooltip(
            "Raised only when the pin-pull input actually pulls the safety pin (first success this equip cycle). " +
            "Wire pin SFX here — do not also play the same sound from the raw pin ScriptableEvent or it will repeat every key press.")]
        [SerializeField] private UnityEvent _onPinPullSucceeded;

        [Header("Equip Anchor")]
        [Tooltip("Transform that defines the hand/hold pose. " +
                 "The extinguisher will be parented here with local pose zeroed.")]
        [SerializeField] private Transform _equipAnchor;

        [Header("Pickup Detection")]
        [Tooltip("İsteğe bağlı: şalterdeki gibi ışın kökü (kamera pivotu / XR ray kökü). Boş bırakılırsa önce Player Camera, sonra bu objedeki ilk Camera, son çare Camera.main kullanılır — Inspector’da sürükle bırak gerekmez.")]
        [SerializeField] private Transform _interactionRayOrigin;

        [Tooltip("Pickup ışını için ikinci yedek: Interaction Ray Origin boşsa bu kameranın transform’u kullanılır; o da boşsa hiyerarşideki Camera veya Camera.main denenir.")]
        [SerializeField] private Camera _playerCamera;

        [Tooltip("Maximum raycast distance for pickup detection.")]
        [SerializeField, Min(0f)] private float _pickupRange = 3f;

        [Tooltip("Layer mask for the pickup raycast. " +
                 "Set this to the layer your extinguisher GameObjects live on.")]
        [SerializeField] private LayerMask _pickupLayerMask = Physics.AllLayers;

        [Tooltip("When true, interacting while already holding an extinguisher will drop the current one\n" +
                 "and immediately equip the new one the ray is pointing at.\n\n" +
                 "When false, the player must manually drop first before picking up another.")]
        [SerializeField] private bool _allowSwap = true;

        [Tooltip("When true, the serialized On Equip Event is not raised when this input flow started while already holding an extinguisher with a controller (e.g. swap after drop). HUD still updates via Extinguisher Changed.")]
        [SerializeField] private bool _suppressOnEquipEventWhenAlreadyHeld = true;

        [Tooltip("When true, On Drop Event is not raised when dropping only to immediately equip another item (swap / ForceEquip replace). Dedicated drop input still raises On Drop Event.")]
        [SerializeField] private bool _suppressOnDropEventDuringSwap = true;

        [Header("Slot Controller")]
        [Tooltip("Central controller that owns all extinguisher slots, used areas, and replacement spawning.")]
        [SerializeField] private ExtinguisherSlotController _slotController;

        /// <summary>Slotta yedek tüp spawn (PC drop / VR pim) için; boşsa VR pim sonrası yeni tüp oluşturulamaz.</summary>
        public ExtinguisherSlotController SlotController => _slotController;

        // ── Public surface ────────────────────────────────────────────────────────

        /// <summary>The currently equipped item, or <c>null</c> if nothing is held.</summary>
        public ExtinguisherPickupItem CurrentItem { get; private set; }

        /// <summary>Hand anchor where equipped <see cref="ExtinguisherPickupItem"/> instances are parented.</summary>
        public Transform EquipAnchor
        {
            get
            {
                if (FireVrGameplayEquipAnchor.RegisteredAnchorOrNull != null)
                    return FireVrGameplayEquipAnchor.RegisteredAnchorOrNull;
                return _equipAnchor;
            }
        }

        /// <summary>Pickup için InteractionRaySource yedek kökü; genelde ana kamera.</summary>
        public Camera PlayerCamera => _playerCamera;

        /// <summary>
        /// Raised whenever the equipped extinguisher changes.
        /// Argument is the newly equipped item, or <c>null</c> when the slot becomes empty.
        /// </summary>
        public event Action<ExtinguisherPickupItem> OnExtinguisherChanged;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_inputContext == null)
            {
                Debug.LogWarning("[PlayerExtinguisherEquipment] No GameplayInputContext assigned.", this);
                return;
            }

            if (_inputContext.InteractEvent != null)
                _inputContext.InteractEvent.OnRaised += HandleInteractInput;

            if (_inputContext.EquipEvent != null && _inputContext.EquipEvent != _inputContext.InteractEvent)
                _inputContext.EquipEvent.OnRaised += HandleInteractInput;

            if (_inputContext.InteractEvent == null && _inputContext.EquipEvent == null)
                Debug.LogWarning("[PlayerExtinguisherEquipment] GameplayInputContext has no Interact or Equip event — pickup input will never fire.", this);

            _inputContext.DropEvent.OnRaised     += HandleDrop;

            if (_inputContext.PinPulling != null)
                _inputContext.PinPulling.OnRaised += HandlePinPull;
        }

        private void OnDisable()
        {
            if (_inputContext == null) return;

            if (_inputContext.InteractEvent != null)
                _inputContext.InteractEvent.OnRaised -= HandleInteractInput;

            if (_inputContext.EquipEvent != null && _inputContext.EquipEvent != _inputContext.InteractEvent)
                _inputContext.EquipEvent.OnRaised -= HandleInteractInput;

            _inputContext.DropEvent.OnRaised     -= HandleDrop;

            if (_inputContext.PinPulling != null)
                _inputContext.PinPulling.OnRaised -= HandlePinPull;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Equips the given item immediately, regardless of proximity.
        /// Useful for scene-start setups where the player begins already holding something.
        /// </summary>
        public void ForceEquip(ExtinguisherPickupItem item)
        {
            if (item == null) return;
            if (CurrentItem != null)
                PerformDrop(_suppressOnDropEventDuringSwap);

            PerformEquip(item);
        }

        /// <summary>Drops the currently equipped item if one is held.</summary>
        public void ForceDrop()
        {
            if (CurrentItem == null) return;
            PerformDrop(suppressOnDropEvent: false);
        }

        /// <summary>
        /// <see cref="VRHandExtinguisherGrabber"/> tüpü ele aldığında: reparent yapmaz (tüp zaten VR elindedir),
        /// yalnızca <see cref="CurrentItem"/> ve <see cref="OnExtinguisherChanged"/> / HUD SO ile eğitim köprülerini
        /// PC kuşanma akışıyla hizalar. Slot / <see cref="_onDropEvent"/> çağrılmaz.
        /// </summary>
        public void NotifyVrEquipped(ExtinguisherPickupItem item)
        {
            if (item == null)
                return;

            if (CurrentItem == item)
            {
                OnExtinguisherChanged?.Invoke(item);
                return;
            }

            if (CurrentItem != null && CurrentItem != item)
            {
                Debug.LogWarning(
                    $"[PlayerExtinguisherEquipment] VR equip '{item.name}' while '{CurrentItem.name}' is still tracked — switching tracked item without slot drop.",
                    this);
            }

            CurrentItem = item;
            OnExtinguisherChanged?.Invoke(item);
            RaiseChangedEvent(item);
        }

        /// <summary>
        /// VR el tüpü bıraktığında <see cref="NotifyVrEquipped"/> ile eşleşen takibi kapatır.
        /// Slot veya <see cref="_onDropEvent"/> tetiklenmez (bırakma fizik / VR tarafında).
        /// </summary>
        public void NotifyVrUnequipped(ExtinguisherPickupItem item)
        {
            if (item == null || CurrentItem != item)
                return;

            CurrentItem = null;
            OnExtinguisherChanged?.Invoke(null);
            RaiseChangedEvent(null);
        }

        // ── Input handlers (ElectricalBreakerInteractable ile aynı abonelik: Interact + ayrıysa Equip) ─────

        private void HandleInteractInput()
        {
            TryPickupOrSwapFromRay();
        }

        private void TryPickupOrSwapFromRay()
        {
            // VR ortamında (FireVrGameplayInteractionRay aktifken) ray ile PC taşıma mantığını tamamen durduruyoruz.
            // Aksi halde VRHandExtinguisherGrabber ile çakışır ve tüpü zorla PC eline (veya sol ele) çeker.
            if (FireVrGameplayInteractionRay.RegisteredRayOriginOrNull != null)
                return;

            bool suppressOnEquipEvent =
                _suppressOnEquipEventWhenAlreadyHeld
                && CurrentItem != null
                && CurrentItem.Controller != null;

            ExtinguisherPickupItem candidate = RaycastForItem();
            if (candidate == null) return;

            if (CurrentItem != null)
            {
                // Holding something — only proceed if swapping is allowed.
                if (!_allowSwap) return;
                PerformDrop(_suppressOnDropEventDuringSwap);
                if (CurrentItem != null) return;
            }

            PerformEquip(candidate, suppressOnEquipEvent);
        }

        private void HandleDrop()
        {
            if (CurrentItem == null) return;
            PerformDrop(suppressOnDropEvent: false);
        }

        private void HandlePinPull()
        {
            if (CurrentItem == null) return;

            ExtinguisherController ctrl = CurrentItem.Controller;
            if (ctrl == null) return;

            if (!ctrl.PullPin())
                return;

            ExtinguisherUsageState usageState = CurrentItem.UsageState;
            if (usageState != null)
                usageState.MarkPinPulled();

            RaiseChangedEvent(CurrentItem);
            _onPinPullSucceeded?.Invoke();
        }

        // ── Core operations ───────────────────────────────────────────────────────

        private void PerformEquip(ExtinguisherPickupItem item, bool suppressOnEquipEvent = false)
        {
            Transform activeEquipAnchor = EquipAnchor;

            if (activeEquipAnchor == null)
            {
                Debug.LogError("[PlayerExtinguisherEquipment] No equip anchor assigned — cannot equip.", this);
                return;
            }

            if (!activeEquipAnchor.gameObject.activeInHierarchy)
            {
                bool isVrAnchor = FireVrGameplayEquipAnchor.RegisteredAnchorOrNull == activeEquipAnchor;
                if (!isVrAnchor)
                {
                    Debug.LogError($"[PlayerExtinguisherEquipment] Kuşanma noktası ('{activeEquipAnchor.name}') inaktif (kapalı)! " +
                        "VR'da iseniz PC eli kapalı olduğu için bu hatayı alıyorsunuz. Sol kontrolcünüze 'ExtinguisherVrEquipAnchorRegister' eklediğinizden emin olun.", this);
                }
                else
                {
                    Debug.LogError($"[PlayerExtinguisherEquipment] VR Sol El kuşanma noktası ('{activeEquipAnchor.name}') inaktif (kapalı)! " +
                        "Kontrolcünün oyunda aktif/açık olduğundan emin olun.", this);
                }
                return;
            }

            if (!item.EquipToPlayer(activeEquipAnchor))
                return;

            CurrentItem = item;
            OnExtinguisherChanged?.Invoke(item);
            RaiseChangedEvent(item);
            if (!suppressOnEquipEvent)
                _onEquipEvent?.Raise();
        }

        private void PerformDrop(bool suppressOnDropEvent = false)
        {
            if (CurrentItem == null)
                return;

            ExtinguisherPickupItem item = CurrentItem;

            ExtinguisherUsageState usageState = item.UsageState;
            bool isUsed = false;

            if (usageState == null)
            {
                Debug.LogWarning(
                    $"[PlayerExtinguisherEquipment] '{item.name}' has no ExtinguisherUsageState; treating it as unused.",
                    item);
            }
            else
            {
                isUsed = usageState.IsUsed;
            }

            if (_slotController == null)
            {
                Debug.LogWarning(
                    "[PlayerExtinguisherEquipment] No ExtinguisherSlotController assigned — cannot drop.",
                    this);
                return;
            }

            CurrentItem = null;

            if (isUsed)
            {
                Debug.Log($"[PlayerExtinguisherEquipment] Used extinguisher '{item.name}' dropped — forwarding to SlotController.", item);
                _slotController.HandleUsedDrop(item);
            }
            else
            {
                _slotController.HandleUnusedReturn(item);
            }

            OnExtinguisherChanged?.Invoke(null);
            RaiseChangedEvent(null);
            if (!suppressOnDropEvent)
                _onDropEvent?.Raise();
        }

        // ── SO event helper ───────────────────────────────────────────────────────

        private void RaiseChangedEvent(ExtinguisherPickupItem item)
        {
            if (_extinguisherChangedEvent == null) return;

            if (item != null)
            {
                ExtinguisherController ctrl = item.Controller;
                ExtinguisherData       data = ctrl != null ? ctrl.ExtinguisherData : null;

                float maxCap        = ctrl  != null ? ctrl.MaxCapacity       : 0f;
                float normalizedCap = ctrl  != null ? ctrl.NormalizedCapacity : 1f;
                float rate          = (data != null && data.ConsumptionRate > 0f) ? data.ConsumptionRate : 1f;
                int   capacity      = Mathf.RoundToInt(normalizedCap * 100f);
                // duration = remaining absolute units / units-per-second
                float safeRate = Mathf.Max(rate, 1e-6f);
                float remainingTime = (normalizedCap * maxCap) / safeRate;

                string lang = EquipmentUiLanguage.CurrentCode();
                string subtitle = data != null ? data.GetSubtitle(lang) : string.Empty;

                _extinguisherChangedEvent.Raise(ExtinguisherChangedEventCompat.CreateEquipped(
                    item.DisplayName,
                    subtitle,
                    capacity,
                    maxCap,
                    remainingTime,
                    ctrl != null && ctrl.IsPinPulled));
            }
            else
            {
                _extinguisherChangedEvent.Raise(ExtinguisherChangedEventCompat.CreateEmpty());
            }
        }

        // ── Pickup search ─────────────────────────────────────────────────────────

        /// <summary>
        /// Şalter prefab’ında olduğu gibi <see cref="_interactionRayOrigin"/> boş olabilir; PC’de
        /// <see cref="_playerCamera"/>, alt <see cref="Camera"/>, <see cref="Camera.main"/> ile otomatik kök seçilir.
        /// </summary>
        private Transform ResolveInteractionRayOriginTransform()
        {
            if (_interactionRayOrigin != null)
                return _interactionRayOrigin;
            if (_playerCamera != null)
                return _playerCamera.transform;
            Camera childCam = GetComponentInChildren<Camera>(true);
            if (childCam != null)
                return childCam.transform;
            return Camera.main != null ? Camera.main.transform : null;
        }

        private ExtinguisherPickupItem RaycastForItem()
        {
            Transform rayOrigin = ResolveInteractionRayOriginTransform();

            if (!InteractionRaySource.TryGetWorldRay(rayOrigin, out Vector3 origin, out Vector3 dir))
                return null;

            if (!float.IsFinite(origin.x) || !float.IsFinite(origin.y) || !float.IsFinite(origin.z)
                || !float.IsFinite(dir.x) || !float.IsFinite(dir.y) || !float.IsFinite(dir.z)
                || dir.sqrMagnitude < 1e-10f)
                return null;

            if (!float.IsFinite(_pickupRange) || _pickupRange <= 0f)
                return null;

            LayerMask pickupMask = _pickupLayerMask;
            MergeExtinguisherPickupLayers(ref pickupMask);

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                dir,
                _pickupRange,
                pickupMask,
                QueryTriggerInteraction.Collide);

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                var item = hit.collider.GetComponentInParent<ExtinguisherPickupItem>();
                if (item == null || item.IsEquipped) continue;

                return item;
            }

            return null;
        }

        static void MergeExtinguisherPickupLayers(ref LayerMask mask)
        {
            TryAddLayer(ref mask, "Estinguisher");
            TryAddLayer(ref mask, "Extinguisher");
            TryAddLayer(ref mask, "Default");
        }

        static void TryAddLayer(ref LayerMask mask, string layerName)
        {
            int id = LayerMask.NameToLayer(layerName);
            if (id < 0)
                return;
            mask = mask.value | (1 << id);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform rayRoot = ResolveInteractionRayOriginTransform();

            if (rayRoot == null && FireVrGameplayInteractionRay.RegisteredRayOriginOrNull == null)
                return;

            if (InteractionRaySource.TryGetWorldRay(rayRoot, out Vector3 o, out Vector3 d))
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(o, d * _pickupRange);
            }
        }
#endif
    }
}
