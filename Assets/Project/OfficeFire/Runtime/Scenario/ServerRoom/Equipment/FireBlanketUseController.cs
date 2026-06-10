using System;
using System.Collections;
using System.Collections.Generic;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Woi.OfficeFire;

namespace Woi.OfficeFire
{
    /// <summary>
    /// G while holding a blanket: inside a fire zone → consume (SetActive false) and gradually
    /// extinguish the target <see cref="FireSource"/>; outside → return to drop anchor.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Fire Blanket Use Controller")]
    public sealed class FireBlanketUseController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerFireBlanketEquipment blanketEquipment;

        [SerializeField]
        private FireSource fireSource;

        [SerializeField]
        private Transform distanceReference;

        [Header("Input")]
        [SerializeField]
        private Key useKey = Key.G;

        [Header("Fire zone")]
        [Tooltip("Layers used by FireTargetZone colliders (server scenes typically layer 9).")]
        [SerializeField]
        private LayerMask fireZoneLayerMask = 1 << 9;

        [SerializeField, Min(0.01f)]
        private float fireZoneProbeRadius = 3f;

        [SerializeField]
        private bool useCrosshairRayForFireZone = true;

        [SerializeField, Min(0.5f)]
        private float fireZoneRaycastDistance = 5f;

        [Header("Extinguish")]
        [SerializeField, Min(0.1f)]
        private float extinguishDuration = 4f;

        [Header("Events")]
        [Tooltip("Raised immediately when G places the blanket on a fire zone (VFX, mesh, etc.).")]
        [SerializeField]
        private UnityEvent onBlanketUsedOnFire;

        [Tooltip("Raised when the target fire source has fully burned out after the extinguish duration.")]
        [SerializeField]
        private UnityEvent onBlanketFireExtinguished;

        [SerializeField]
        private bool enableDebugLogs;

        [Header("Use Prompt")]
        [SerializeField]
        [TextArea(1, 3)]
        private string useInstructionText = "Approach the fire and press G to place the blanket";

        [SerializeField]
        [TextArea(1, 3)]
        private string useInstructionTextTurkish = "Yangına yaklaş ve G ile bırak";

        [SerializeField]
        private bool preferTurkishInstruction = true;

        /// <summary>Fired when gradual fire suppression completes (not on placement).</summary>
        public event Action BlanketFireExtinguished;

        public bool IsInsideFireZone => CheckInsideFireZone(out _);

        public bool TryGetTargetFireZone(out FireTargetZone zone) => CheckInsideFireZone(out zone);

        public bool IsExtinguishingFire { get; private set; }

        /// <summary>
        /// Returns true when the distance probe overlaps the given zone (same check used for G to place blanket).
        /// </summary>
        public bool IsPlayerInsideZone(FireTargetZone zone)
        {
            if (zone == null)
            {
                return false;
            }

            return CheckInsideFireZone(out FireTargetZone matchedZone) && matchedZone == zone;
        }

        private Coroutine _extinguishRoutine;
        private FireBlanketUseScreenPrompt _useScreenPrompt;
        private bool _usePromptVisible;

        private void Awake()
        {
            if (blanketEquipment == null)
            {
                blanketEquipment = GetComponent<PlayerFireBlanketEquipment>();
            }

            if (fireZoneProbeRadius < 2.5f)
            {
                fireZoneProbeRadius = 3f;
            }

            EnsureUseScreenPrompt();
        }

        private void OnDisable()
        {
            if (_extinguishRoutine != null)
            {
                StopCoroutine(_extinguishRoutine);
                _extinguishRoutine = null;
            }

            IsExtinguishingFire = false;
            _useScreenPrompt?.Hide();
            _usePromptVisible = false;
        }

        private void LateUpdate()
        {
            UpdateUseScreenPrompt();
        }

        private void UpdateUseScreenPrompt()
        {
            EnsureUseScreenPrompt();

            bool show = ShouldShowUseInstructionPrompt(out _);
            if (show == _usePromptVisible)
            {
                return;
            }

            _usePromptVisible = show;
            _useScreenPrompt?.SetVisible(show);
        }

        private void EnsureUseScreenPrompt()
        {
            if (_useScreenPrompt != null)
            {
                return;
            }

            _useScreenPrompt = new FireBlanketUseScreenPrompt(this);
            _useScreenPrompt.SetText(useInstructionText, useInstructionTextTurkish, preferTurkishInstruction);
        }

        private bool ShouldShowUseInstructionPrompt(out FireTargetZone zone)
        {
            zone = null;

            if (blanketEquipment == null || blanketEquipment.CurrentItem == null)
            {
                return false;
            }

            if (IsExtinguishingFire)
            {
                return false;
            }

            return true;
        }

        private void Update()
        {
            if (Keyboard.current == null || blanketEquipment == null)
            {
                return;
            }

            if (!Keyboard.current[useKey].wasPressedThisFrame)
            {
                return;
            }

            TryHandleBlanketDropOrUse();
        }

        public bool TryHandleBlanketDropOrUse()
        {
            FireBlanketPickupItem item = blanketEquipment != null ? blanketEquipment.CurrentItem : null;
            if (item == null)
            {
                Log("G ignored — no blanket equipped.");
                return false;
            }

            if (CheckInsideFireZone(out FireTargetZone zone))
            {
                return TryConsumeBlanketOnFire(item, zone);
            }

            Log("G treated as drop — player is not inside a matching fire zone (move closer or aim at the fire).");
            return TryDropBlanketToAnchor(item);
        }

        private bool TryConsumeBlanketOnFire(FireBlanketPickupItem item, FireTargetZone zone)
        {
            if (IsExtinguishingFire)
            {
                Log("G ignored — blanket extinguish already in progress.");
                return false;
            }

            item.ConsumeOnFire();
            blanketEquipment.NotifyConsumed(item);
            onBlanketUsedOnFire?.Invoke();

            FireSource targetSource = ResolveTargetFireSource(zone);
            if (targetSource != null && !targetSource.IsExtinguished)
            {
                _extinguishRoutine = StartCoroutine(ExtinguishFireSourceRoutine(targetSource));
            }
            else
            {
                CompleteBlanketExtinguish();
            }

            Log(zone != null
                ? $"Blanket placed on fire zone '{zone.name}' — extinguishing over {extinguishDuration:F1}s."
                : $"Blanket placed inside fire zone — extinguishing over {extinguishDuration:F1}s.");
            return true;
        }

        private IEnumerator ExtinguishFireSourceRoutine(FireSource source)
        {
            IsExtinguishingFire = true;

            while (source != null && !source.IsExtinguished)
            {
                bool anyActive = false;

                foreach (FireTargetZone targetZone in source.Zones)
                {
                    if (targetZone == null || targetZone.IsExtinguished)
                    {
                        continue;
                    }

                    anyActive = true;
                    float suppressionThisFrame = targetZone.MaxIntensity > 0f
                        ? (targetZone.MaxIntensity / extinguishDuration) * Time.deltaTime
                        : targetZone.CurrentIntensity;

                    targetZone.ApplySuppression(suppressionThisFrame);
                }

                if (!anyActive)
                {
                    break;
                }

                yield return null;
            }

            IsExtinguishingFire = false;
            _extinguishRoutine = null;
            CompleteBlanketExtinguish();
        }

        private void CompleteBlanketExtinguish()
        {
            onBlanketFireExtinguished?.Invoke();
            BlanketFireExtinguished?.Invoke();
            Log("Target fire source fully extinguished by blanket.");
        }

        private FireSource ResolveTargetFireSource(FireTargetZone zone)
        {
            if (zone != null)
            {
                FireSource fromZone = zone.GetComponentInParent<FireSource>();
                if (fromZone != null)
                {
                    return fromZone;
                }
            }

            if (fireSource != null)
            {
                return fireSource;
            }

            return FindFirstObjectByType<FireSource>();
        }

        private bool TryDropBlanketToAnchor(FireBlanketPickupItem item)
        {
            item.DropFromPlayer();
            blanketEquipment.NotifyDropped(item);
            Log("Blanket returned to drop anchor.");
            return true;
        }

        private bool CheckInsideFireZone(out FireTargetZone matchedZone)
        {
            if (TryMatchFireZoneFromProximity(out matchedZone))
            {
                return true;
            }

            if (TryMatchFireZoneFromFireSourceProximity(out matchedZone))
            {
                return true;
            }

            if (useCrosshairRayForFireZone && TryMatchFireZoneFromCrosshairRay(out matchedZone))
            {
                return true;
            }

            if (useCrosshairRayForFireZone && TryMatchFireZoneFromCrosshairAim(out matchedZone))
            {
                return true;
            }

            matchedZone = null;
            return false;
        }

        private bool TryMatchFireZoneFromProximity(out FireTargetZone matchedZone)
        {
            matchedZone = null;
            Transform reference = ResolveDistanceReference();
            if (reference == null)
            {
                return false;
            }

            Vector3 point = reference.position;
            Collider[] overlaps = Physics.OverlapSphere(
                point,
                fireZoneProbeRadius,
                fireZoneLayerMask,
                QueryTriggerInteraction.Collide);

            if (overlaps == null || overlaps.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider collider = overlaps[i];
                if (collider == null)
                {
                    continue;
                }

                FireTargetZone zone = ResolveFireTargetZone(collider);
                if (!IsAcceptedFireZone(zone))
                {
                    continue;
                }

                matchedZone = zone;
                return true;
            }

            return false;
        }

        private bool TryMatchFireZoneFromFireSourceProximity(out FireTargetZone matchedZone)
        {
            matchedZone = null;
            Transform reference = ResolveDistanceReference();
            if (reference == null)
            {
                return false;
            }

            Vector3 point = reference.position;
            FireSource[] sources = FindObjectsByType<FireSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            FireSource closestSource = null;
            float closestDistance = fireZoneProbeRadius;

            for (int i = 0; i < sources.Length; i++)
            {
                FireSource source = sources[i];
                if (source == null || source.IsExtinguished)
                {
                    continue;
                }

                float distance = Vector3.Distance(point, source.transform.position);
                if (distance > closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                closestSource = source;
            }

            if (closestSource == null)
            {
                return false;
            }

            return TryGetFirstActiveZone(closestSource, out matchedZone);
        }

        private bool TryMatchFireZoneFromCrosshairAim(out FireTargetZone matchedZone)
        {
            matchedZone = null;
            if (blanketEquipment == null || !blanketEquipment.TryGetCrosshairRay(out Ray ray))
            {
                return false;
            }

            if (!Physics.Raycast(ray, out RaycastHit hit, fireZoneRaycastDistance, ~0, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            FireSource[] sources = FindObjectsByType<FireSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            float aimRadius = fireZoneProbeRadius * 1.5f;
            FireSource bestSource = null;
            float bestDistance = aimRadius;

            for (int i = 0; i < sources.Length; i++)
            {
                FireSource source = sources[i];
                if (source == null || source.IsExtinguished)
                {
                    continue;
                }

                float distance = Vector3.Distance(hit.point, source.transform.position);
                if (distance > bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestSource = source;
            }

            if (bestSource == null)
            {
                return false;
            }

            return TryGetFirstActiveZone(bestSource, out matchedZone);
        }

        private static bool TryGetFirstActiveZone(FireSource source, out FireTargetZone matchedZone)
        {
            matchedZone = null;
            if (source == null)
            {
                return false;
            }

            IReadOnlyList<FireTargetZone> zones = source.Zones;
            for (int i = 0; i < zones.Count; i++)
            {
                FireTargetZone zone = zones[i];
                if (zone == null || zone.IsExtinguished)
                {
                    continue;
                }

                matchedZone = zone;
                return true;
            }

            return false;
        }

        private bool TryMatchFireZoneFromCrosshairRay(out FireTargetZone matchedZone)
        {
            matchedZone = null;
            if (blanketEquipment == null || !blanketEquipment.TryGetCrosshairRay(out Ray ray))
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                fireZoneRaycastDistance,
                fireZoneLayerMask,
                QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                FireTargetZone zone = ResolveFireTargetZone(collider);
                if (!IsAcceptedFireZone(zone))
                {
                    continue;
                }

                matchedZone = zone;
                return true;
            }

            return false;
        }

        private static FireTargetZone ResolveFireTargetZone(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            FireTargetZone zone = collider.GetComponent<FireTargetZone>();
            if (zone == null)
            {
                zone = collider.GetComponentInParent<FireTargetZone>();
            }

            return zone;
        }

        private bool IsAcceptedFireZone(FireTargetZone zone)
        {
            return zone != null && !zone.IsExtinguished;
        }

        private Transform ResolveDistanceReference()
        {
            if (distanceReference != null)
            {
                return distanceReference;
            }

            if (blanketEquipment != null
                && blanketEquipment.CurrentItem != null
                && blanketEquipment.EquipAnchor != null)
            {
                return blanketEquipment.EquipAnchor;
            }

            if (blanketEquipment != null)
            {
                return blanketEquipment.transform;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform : transform;
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.Log($"[FireBlanketUseController] {message}", this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform reference = ResolveDistanceReference();
            if (reference == null)
            {
                return;
            }

            Gizmos.color = IsInsideFireZone ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(reference.position, fireZoneProbeRadius);
        }
#endif
    }
}
