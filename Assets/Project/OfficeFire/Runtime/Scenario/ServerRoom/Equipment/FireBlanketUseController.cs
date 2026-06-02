using System;
using System.Collections;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

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
        private float fireZoneProbeRadius = 0.25f;

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

        /// <summary>Fired when gradual fire suppression completes (not on placement).</summary>
        public event Action BlanketFireExtinguished;

        public bool IsInsideFireZone => CheckInsideFireZone(out _);

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

        private void Start()
        {
            EnsureFireZoneUsePrompts();
        }

        private void EnsureFireZoneUsePrompts()
        {
            FireSource source = fireSource;
            if (source == null)
            {
                return;
            }

            foreach (FireTargetZone zone in source.Zones)
            {
                if (zone == null || zone.GetComponent<FireBlanketFireZoneUsePrompt>() != null)
                {
                    continue;
                }

                zone.gameObject.AddComponent<FireBlanketFireZoneUsePrompt>();
            }
        }

        private void OnDisable()
        {
            if (_extinguishRoutine != null)
            {
                StopCoroutine(_extinguishRoutine);
                _extinguishRoutine = null;
            }

            IsExtinguishingFire = false;
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
            if (fireSource != null)
            {
                return fireSource;
            }

            if (zone != null)
            {
                FireSource fromZone = zone.GetComponentInParent<FireSource>();
                if (fromZone != null)
                {
                    return fromZone;
                }
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

                FireTargetZone zone = collider.GetComponent<FireTargetZone>();
                if (zone == null)
                {
                    zone = collider.GetComponentInParent<FireTargetZone>();
                }

                if (zone == null)
                {
                    continue;
                }

                if (fireSource != null && !zone.transform.IsChildOf(fireSource.transform))
                {
                    continue;
                }

                matchedZone = zone;
                return true;
            }

            return false;
        }

        private Transform ResolveDistanceReference()
        {
            if (distanceReference != null)
            {
                return distanceReference;
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
