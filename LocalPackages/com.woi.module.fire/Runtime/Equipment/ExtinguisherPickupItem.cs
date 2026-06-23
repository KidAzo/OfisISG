using FireExtinguisher.Core;
using FireExtinguisher.PC;
using UnityEngine;
using Woi.Game;

namespace Woi.Equipment
{
    /// <summary>
    /// Game-layer wrapper for a world-placed fire extinguisher.
    /// Owns the transition between the "loose world object" state and the
    /// "equipped by player" state by parenting/unparenting and calling into the
    /// core framework's <see cref="PCHoldStateProvider"/>.
    /// </summary>
    /// <remarks>
    /// No Rigidbody is required. Movement while equipped is handled entirely by
    /// parenting this transform under the equip anchor — it follows the player
    /// hierarchy naturally.
    /// This component does NOT contain any spray, capacity, or input logic —
    /// those remain in the core <see cref="ExtinguisherController"/>.
    /// Equip / drop are driven externally by <see cref="PlayerExtinguisherEquipment"/>.
    /// </remarks>
    [AddComponentMenu("Woi/Equipment/Extinguisher Pickup Item")]
    public sealed class ExtinguisherPickupItem : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Framework References")]
        [Tooltip("The core extinguisher controller on this GameObject or a child.")]
        [SerializeField] private ExtinguisherController _controller;

        [Tooltip("The PC hold state provider on this GameObject or a child.")]
        [SerializeField] private PCHoldStateProvider _holdStateProvider;

        [Header("Drop")]
        [Tooltip("World transform where this extinguisher returns when dropped. " +
                 "Leave empty to drop at its current world position.")]
        [SerializeField] private Transform _dropAnchor;

        [Tooltip("Optional home point used for unused returns and fresh replacement spawning.")]
        [SerializeField] private ExtinguisherHomePoint _homePoint;

        [Tooltip("Tracks whether this extinguisher has been used by pulling the safety pin.")]
        [SerializeField] private ExtinguisherUsageState _usageState;

        [Header("Display")]
        [Tooltip("Yedek başlık: Extinguisher Data’da ilgili dil + yedek dil boşsa HUD bu metni kullanır.")]
        [SerializeField] private string _displayName = "Fire Extinguisher";

        // ── Cached components ─────────────────────────────────────────────────────

        private Collider[] _colliders;

        // ── Public read-only surface ──────────────────────────────────────────────

        /// <summary>The core extinguisher controller for this item.</summary>
        public ExtinguisherController Controller => _controller;

        /// <summary>
        /// Önce <see cref="ExtinguisherController.ExtinguisherData"/> çok dilli adı
        /// (<see cref="ExtinguisherData.GetDisplayName"/> + aktif UI dili), boşsa yedek <c>_displayName</c>.
        /// </summary>
        public string DisplayName => ResolveDisplayName();

        private string ResolveDisplayName()
        {
            ExtinguisherController c = _controller != null
                ? _controller
                : GetComponentInChildren<ExtinguisherController>();

            if (c != null)
            {
                ExtinguisherData data = c.ExtinguisherData;
                if (data != null)
                {
                    string fromSo = data.GetDisplayName(EquipmentUiLanguage.CurrentCode());
                    if (!string.IsNullOrWhiteSpace(fromSo))
                        return fromSo;
                }
            }

            return _displayName;
        }

        /// <summary>Whether this item is currently equipped by a player.</summary>
        public bool IsEquipped { get; private set; }

        /// <summary>
        /// VR grab sistemi için IsEquipped durumunu doğrudan set eder.
        /// IL2CPP builds'de private setter reflection ile strip edilebileceği için
        /// bu method kullanılmalıdır.
        /// </summary>
        public void SetEquippedVr(bool value) => IsEquipped = value;

        public ExtinguisherHomePoint HomePoint => _homePoint;
        public ExtinguisherUsageState UsageState => _usageState;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);

            if (_controller == null)
                _controller = GetComponentInChildren<ExtinguisherController>();

            if (_holdStateProvider == null)
                _holdStateProvider = GetComponentInChildren<PCHoldStateProvider>();

            if (_homePoint == null)
                _homePoint = GetComponent<ExtinguisherHomePoint>();

            if (_usageState == null)
                _usageState = GetComponent<ExtinguisherUsageState>();

            if (_controller == null)
                Debug.LogError($"[ExtinguisherPickupItem] No ExtinguisherController found on '{name}'.", this);

            if (_holdStateProvider == null)
                Debug.LogError($"[ExtinguisherPickupItem] No PCHoldStateProvider found on '{name}'.", this);

            EnsureHoverOutline();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Transitions this item into an equipped state.
        /// Parents this transform under <paramref name="equipAnchor"/> with the local
        /// pose zeroed so the anchor defines the exact hand position and orientation.
        /// Colliders are disabled to prevent world collisions while held,
        /// and the core hold-state gate is opened.
        /// </summary>
        public bool EquipToPlayer(Transform equipAnchor)
        {
            if (IsEquipped)
            {
                Debug.LogWarning($"[ExtinguisherPickupItem] '{name}' is already equipped.", this);
                return false;
            }

            if (!IsUsableWorldParent(equipAnchor, out string reason))
            {
                Debug.LogError(
                    $"[ExtinguisherPickupItem] Cannot equip '{name}' — invalid anchor ({reason}). " +
                    "Assign a hand/controller transform with finite position, rotation, and non-zero scale.",
                    this);
                return false;
            }

            IsEquipped = true;

            // Parent under the equip anchor — the item now moves with the player hierarchy.
            transform.SetParent(equipAnchor, worldPositionStays: false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            // PC'de Rigidbody varsa veya VR'dan miras kaldıysa, PC'de her zaman kinematic olmalıdır.
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = true;

            // Disable colliders to prevent clipping against the world while held.
            SetCollidersEnabled(false);

            // Open the core spray gate.
            _holdStateProvider.Equip();
            return true;
        }

        static bool IsUsableWorldParent(Transform t, out string reason)
        {
            reason = string.Empty;
            if (t == null)
            {
                reason = "null";
                return false;
            }

            if (!t.gameObject.activeInHierarchy)
            {
                reason = "inactive hierarchy";
                return false;
            }

            Vector3 p = t.position;
            if (!IsFiniteVector3(p))
            {
                reason = "non-finite position";
                return false;
            }

            Quaternion r = t.rotation;
            if (!IsFiniteQuaternion(r))
            {
                reason = "non-finite rotation";
                return false;
            }

            Vector3 ls = t.lossyScale;
            if (!IsFiniteVector3(ls) || ls.x < 1e-6f || ls.y < 1e-6f || ls.z < 1e-6f)
            {
                reason = "non-finite or ~zero lossyScale";
                return false;
            }

            return true;
        }

        static bool IsFiniteVector3(Vector3 v) =>
            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        static bool IsFiniteQuaternion(Quaternion q) =>
            float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) && float.IsFinite(q.w);

        /// <summary>
        /// Transitions this item from equipped back into a world object.
        /// Places it at its own <c>_dropAnchor</c> if one is assigned,
        /// otherwise leaves it at whatever world position it currently occupies.
        /// Colliders are restored and the hold-state gate is closed.
        /// </summary>
        public void DropFromPlayer()
        {
            if (!IsEquipped)
            {
                Debug.LogWarning($"[ExtinguisherPickupItem] '{name}' is not equipped — cannot drop.", this);
                return;
            }

            IsEquipped = false;

            // Close the core spray gate before unparenting.
            _holdStateProvider.Unequip();

            // Unparent so world-space positioning applies correctly.
            transform.SetParent(null);

            // Return to this item's designated drop pose, or stay at current position.
            if (_dropAnchor != null)
            {
                transform.SetPositionAndRotation(_dropAnchor.position, _dropAnchor.rotation);
            }
            else
            {
                Debug.LogWarning($"[ExtinguisherPickupItem] '{name}' has no drop anchor — dropping in place.", this);
            }

            // PC drop işlemi sırasında, VR'daki gibi fiziksel bir serbest düşüş istenmediği için Rigidbody kinematic kalır.
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = true;

            // Re-enable colliders so the world object can be picked up again.
            SetCollidersEnabled(true);
        }

        public void ReturnUnusedToHome()
        {
            if (_homePoint != null)
            {
                _homePoint.ReturnToHome(this);
                return;
            }

            DropFromPlayer();
        }

        /// <summary>
        /// VR el tutuşu / yere bırakma: <see cref="EquipToPlayer"/> collider’ları kapatır; dünyada fizik için tekrar açılmalıdır.
        /// </summary>
        public void SetPickupCollidersEnabled(bool enabled) => SetCollidersEnabled(enabled);

        public void PlaceInWorld(Vector3 position, Quaternion rotation, Vector3 localScale, bool enableColliders)
        {
            if (IsEquipped)
            {
                IsEquipped = false;

                if (_holdStateProvider != null)
                    _holdStateProvider.Unequip();
            }

            transform.SetParent(null);
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = localScale;
            SetCollidersEnabled(enableColliders);
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private void SetCollidersEnabled(bool enabled)
        {
            foreach (Collider col in _colliders)
                col.enabled = enabled;

            if (!enabled)
                GetComponent<HoverOutline>()?.ResetHover();
        }

        void EnsureHoverOutline()
        {
            if (GetComponent<HoverOutline>() != null)
                return;

            Outline outline = GetComponent<Outline>();
            if (outline == null)
            {
                outline = gameObject.AddComponent<Outline>();
                outline.OutlineColor = new Color(1f, 0.93f, 0f, 1f);
                outline.OutlineWidth = 2f;
            }

            outline.enabled = false;
            gameObject.AddComponent<HoverOutline>();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_controller == null)
                _controller = GetComponentInChildren<ExtinguisherController>();

            if (_holdStateProvider == null)
                _holdStateProvider = GetComponentInChildren<PCHoldStateProvider>();

            if (_homePoint == null)
                _homePoint = GetComponent<ExtinguisherHomePoint>();

            if (_usageState == null)
                _usageState = GetComponent<ExtinguisherUsageState>();
        }
#endif
    }
}
