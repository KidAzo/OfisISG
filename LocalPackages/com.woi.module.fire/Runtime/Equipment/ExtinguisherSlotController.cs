using System;
using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.Equipment
{
    /// <summary>
    /// One entry in the slot list. Fill this in the Inspector for each extinguisher holder.
    /// </summary>
    [Serializable]
    public class ExtinguisherSlot
    {
        [Tooltip("The extinguisher scene instance that currently occupies this slot. " +
                 "Drag the scene object here — do NOT drag the prefab asset.")]
        public ExtinguisherPickupItem CurrentExtinguisher;

        [Tooltip("Prefab to instantiate as a fresh replacement. " +
                 "Must match the type of extinguisher for this slot (CO2, Foam, etc.).")]
        public GameObject Prefab;

        [Tooltip("Spawn position/rotation for the replacement. " +
                 "Leave empty to use the initial world position of CurrentExtinguisher.")]
        public Transform SpawnPoint;

        [Tooltip("Optional parent transform for spawned replacements. " +
                 "Leave empty to spawn at scene root.")]
        public Transform SpawnParent;

        // ── Runtime-only (not serialized) ─────────────────────────────────────
        [NonSerialized] public Vector3    CachedPosition;
        [NonSerialized] public Quaternion CachedRotation;
        [NonSerialized] public Vector3    CachedLocalScale;
        [NonSerialized] public bool       ReplacementInProgress;
    }

    /// <summary>
    /// Central controller that owns every extinguisher slot in the scene.
    /// Handles used-drop → send to used area + spawn replacement,
    /// and unused-drop → return extinguisher to slot position.
    /// </summary>
    [AddComponentMenu("Woi/Equipment/Extinguisher Slot Controller")]
    public sealed class ExtinguisherSlotController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Slots")]
        [Tooltip("One entry per extinguisher holder in the scene.")]
        [SerializeField] private ExtinguisherSlot[] _slots;

        [Header("Used Area")]
        [Tooltip("The area where used (pin-pulled) extinguishers are placed after the player drops them.")]
        [SerializeField] private UsedExtinguisherArea _usedExtinguisherArea;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (_slots == null) return;

            foreach (ExtinguisherSlot slot in _slots)
                CacheSlotPose(slot);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called when a USED (pin-pulled) extinguisher is dropped.
        /// Sends the used item to the used area and spawns a fresh replacement at its slot.
        /// </summary>
        public void HandleUsedDrop(ExtinguisherPickupItem item)
        {
            ExtinguisherSlot slot = FindSlotFor(item);

            string slotInfo = slot != null
                ? "Prefab=" + (slot.Prefab != null ? slot.Prefab.name : "NOT ASSIGNED") + " Pos=" + slot.CachedPosition
                : "NOT FOUND - assign this extinguisher in ExtinguisherSlotController";

            Debug.Log($"[SlotController] Used drop: '{item.name}'. Slot: {slotInfo}.", item);

            // Move the used extinguisher to the used area first.
            if (_usedExtinguisherArea != null)
                _usedExtinguisherArea.PlaceUsedExtinguisher(item);
            else
                item.PlaceInWorld(item.transform.position, item.transform.rotation, item.transform.localScale, enableColliders: false);

            if (slot == null)
            {
                Debug.LogWarning(
                    $"[SlotController] '{item.name}': no slot found. " +
                    $"Add this extinguisher to a slot entry in ExtinguisherSlotController.",
                    item);
                return;
            }

            if (slot.ReplacementInProgress)
            {
                Debug.Log($"[SlotController] Replacement already in progress for this slot — skipping.", this);
                return;
            }

            slot.ReplacementInProgress = true;
            SpawnReplacement(slot);
        }

        /// <summary>
        /// Called when an UNUSED (no pin pull) extinguisher is dropped.
        /// Returns it to its original slot position.
        /// </summary>
        public void HandleUnusedReturn(ExtinguisherPickupItem item)
        {
            ExtinguisherSlot slot = FindSlotFor(item);

            if (slot != null)
            {
                Debug.Log($"[SlotController] Returning unused '{item.name}' to slot at {slot.CachedPosition}.", item);
                item.PlaceInWorld(slot.CachedPosition, slot.CachedRotation, slot.CachedLocalScale, enableColliders: true);
            }
            else
            {
                Debug.LogWarning(
                    $"[SlotController] '{item.name}': no slot found for unused return — dropping in place.",
                    item);
                item.PlaceInWorld(item.transform.position, item.transform.rotation, item.transform.localScale, enableColliders: true);
            }
        }

        /// <summary>
        /// VR: Pim çekildiğinde PC’de <c>G</c> ile kullanılmış düşürmedeki gibi slotta <b>yeni tüp</b> oluşturur.
        /// Eldeki kullanılmış örnek sahne kökünde kalır (yere bırakılınca fizik + tekrar tutuş için).
        /// </summary>
        public bool TrySpawnReplacementAfterVrPinPull(ExtinguisherPickupItem heldItem)
        {
            if (heldItem == null)
                return false;

            ExtinguisherSlot slot = FindSlotFor(heldItem);
            if (slot == null)
            {
                Debug.LogWarning(
                    $"[SlotController] VR pin pull: '{heldItem.name}' için slot bulunamadı — " +
                    "ExtinguisherSlotController’da Current Extinguisher bu örneğe atanmış mı kontrol edin.",
                    heldItem);
                return false;
            }

            if (slot.ReplacementInProgress)
            {
                Debug.Log($"[SlotController] VR pin pull: bu slot için replacement zaten sürüyor — atlanıyor.", this);
                return false;
            }

            slot.ReplacementInProgress = true;
            SpawnReplacement(slot);
            return true;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private ExtinguisherSlot FindSlotFor(ExtinguisherPickupItem item)
        {
            if (_slots == null || item == null) return null;

            foreach (ExtinguisherSlot slot in _slots)
                if (slot.CurrentExtinguisher == item)
                    return slot;

            return null;
        }

        private static void CacheSlotPose(ExtinguisherSlot slot)
        {
            if (slot == null) return;

            // Prefer the explicit SpawnPoint; fall back to the extinguisher's current world pose.
            Transform origin = slot.SpawnPoint != null
                ? slot.SpawnPoint
                : slot.CurrentExtinguisher != null ? slot.CurrentExtinguisher.transform : null;

            if (origin == null)
            {
                Debug.LogWarning("[SlotController] A slot has no SpawnPoint and no CurrentExtinguisher — cannot cache pose.");
                return;
            }

            slot.CachedPosition   = origin.position;
            slot.CachedRotation   = origin.rotation;
            slot.CachedLocalScale = origin.localScale;
        }

        private void SpawnReplacement(ExtinguisherSlot slot)
        {
            if (slot.Prefab == null)
            {
                Debug.LogWarning(
                    $"[SlotController] Slot has no Prefab assigned — cannot spawn replacement. " +
                    $"Assign the correct prefab in the Extinguisher Slot Controller Inspector.",
                    this);
                slot.ReplacementInProgress = false;
                return;
            }

            Debug.Log(
                $"[SlotController] Spawning '{slot.Prefab.name}' at {slot.CachedPosition} " +
                $"(parent: {( slot.SpawnParent != null ? slot.SpawnParent.name : "scene root" )}).",
                this);

            GameObject replacement = Instantiate(
                slot.Prefab,
                slot.CachedPosition,
                slot.CachedRotation,
                slot.SpawnParent);

            replacement.transform.localScale = slot.CachedLocalScale;

            ResetExtinguisherState(replacement);

            // Update the slot to track the new live instance.
            ExtinguisherPickupItem newItem = replacement.GetComponentInChildren<ExtinguisherPickupItem>(true);
            if (newItem != null)
                slot.CurrentExtinguisher = newItem;

            slot.ReplacementInProgress = false;
        }

        private static void ResetExtinguisherState(GameObject obj)
        {
            if (obj == null) return;

            var usage = obj.GetComponentInChildren<ExtinguisherUsageState>(true);
            if (usage != null) usage.ResetUsageState();

            var ctrl = obj.GetComponentInChildren<ExtinguisherController>(true);
            if (ctrl != null) ctrl.ResetAfterWorldDrop();

            obj.SendMessage("ResetFeedback", SendMessageOptions.DontRequireReceiver);
            obj.SendMessage("ResetHover",    SendMessageOptions.DontRequireReceiver);
        }
    }
}
