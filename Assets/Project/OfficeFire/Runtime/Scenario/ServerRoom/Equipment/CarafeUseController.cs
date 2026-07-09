using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Woi.OfficeFire
{
    /// <summary>
    /// G while holding a carafe: inside a fire zone → consume (SetActive false) and make the target
    /// <see cref="FireSource"/> grow bigger; outside → return to drop anchor.
    /// Mirrors <see cref="FireBlanketUseController"/> 1:1 except the fire is intensified, not extinguished.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Carafe Use Controller")]
    public sealed class CarafeUseController : MonoBehaviour
    {
        private const int OverlapBufferSize = 32;
        private const int RaycastBufferSize = 32;
        private const float FireSourceCacheRefreshSeconds = 1f;

        [Header("References")]
        [SerializeField]
        private PlayerCarafeEquipment carafeEquipment;

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

        [Header("Fire growth")]
        [Tooltip("Final visual scale of the fire relative to its starting scale when the carafe is used.")]
        [SerializeField, Min(1f)]
        private float fireGrowMultiplier = 1.6f;

        [Tooltip("Seconds over which the fire grows to its bigger size.")]
        [SerializeField, Min(0.05f)]
        private float growDuration = 1.2f;

        [Header("Reset after use")]
        [Tooltip("Seconds after the carafe is poured before CarafeAndVfx is turned off and the Carafe object is shown again (unusable).")]
        [SerializeField, Min(0f)]
        private float vfxResetDelaySeconds = 4f;

        [Header("Events")]
        [Tooltip("Raised immediately when G places the carafe on a fire zone (enable CarafeAndVfx, etc.).")]
        [SerializeField]
        private UnityEvent onCarafeUsedOnFire;

        [Tooltip("Raised when the target fire has finished growing to its bigger size.")]
        [SerializeField]
        private UnityEvent onCarafeFireGrown;

        [Tooltip("Raised after the reset delay (turn CarafeAndVfx off). The Carafe object itself is restored automatically.")]
        [SerializeField]
        private UnityEvent onCarafeReset;

        [SerializeField]
        private bool enableDebugLogs;

        [Header("Use Prompt")]
        [SerializeField]
        [TextArea(1, 3)]
        private string useInstructionText = "Approach the fire and press G to pour the carafe";

        [SerializeField]
        [TextArea(1, 3)]
        private string useInstructionTextTurkish = "Yangına yaklaş ve G ile dök";

        [SerializeField]
        private bool preferTurkishInstruction = true;

        /// <summary>Fired when the fire has finished growing (not on placement).</summary>
        public event Action CarafeFireGrown;

        public bool IsInsideFireZone => TryGetTargetFireZone(out _);

        public bool TryGetTargetFireZone(out FireTargetZone zone)
        {
            EnsureFireZoneCacheForFrame();
            zone = _cachedMatchedZone;
            return _cachedInsideFireZone;
        }

        public bool IsGrowingFire { get; private set; }

        /// <summary>
        /// VR API — true when the player probe is inside a fire zone (grip release pour).
        /// </summary>
        public bool IsPlayerNearAssignedFireSource() => TryGetTargetFireZone(out _);

        public bool IsPlayerInsideZone(FireTargetZone zone)
        {
            if (zone == null)
            {
                return false;
            }

            return TryGetTargetFireZone(out FireTargetZone matchedZone) && matchedZone == zone;
        }

        private Coroutine _growRoutine;
        private Coroutine _resetRoutine;
        private FireBlanketUseScreenPrompt _useScreenPrompt;
        private bool _usePromptVisible;
        private WaitForSeconds _cachedResetWait;

        private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];
        private readonly RaycastHit[] _raycastBuffer = new RaycastHit[RaycastBufferSize];
        private FireSource[] _cachedFireSources = Array.Empty<FireSource>();
        private float _fireSourcesCacheTime = -999f;
        private Transform _cachedPlayerTransform;
        private bool _playerTransformResolved;

        private int _fireZoneCacheFrame = -1;
        private bool _cachedInsideFireZone;
        private FireTargetZone _cachedMatchedZone;

        private static MethodInfo _applyIntensificationMethod;
        private static readonly object[] IntensificationArgs = new object[1];

        private void Awake()
        {
            if (carafeEquipment == null)
            {
                carafeEquipment = GetComponent<PlayerCarafeEquipment>();
            }

            if (fireZoneProbeRadius < 2.5f)
            {
                fireZoneProbeRadius = 3f;
            }

            EnsureUseScreenPrompt();
            CacheResetWait();
            RefreshFireSourcesCache(force: true);
            ResolvePlayerTransform();
        }

        private void OnEnable()
        {
            InvalidateFireZoneCache();
            RefreshFireSourcesCache(force: true);
        }

        private void OnDisable()
        {
            if (_growRoutine != null)
            {
                StopCoroutine(_growRoutine);
                _growRoutine = null;
            }

            if (_resetRoutine != null)
            {
                StopCoroutine(_resetRoutine);
                _resetRoutine = null;
            }

            IsGrowingFire = false;
            _useScreenPrompt?.Hide();
            _usePromptVisible = false;
            InvalidateFireZoneCache();
        }

        private void LateUpdate()
        {
            UpdateUseScreenPrompt();
        }

        private void UpdateUseScreenPrompt()
        {
            EnsureUseScreenPrompt();

            bool show = ShouldShowUseInstructionPrompt();
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

        private bool ShouldShowUseInstructionPrompt()
        {
            if (carafeEquipment == null || carafeEquipment.CurrentItem == null)
            {
                return false;
            }

            if (IsGrowingFire)
            {
                return false;
            }

            return true;
        }

        private void Update()
        {
            if (carafeEquipment == null)
            {
                return;
            }

            if (!OfficeFireUseKeyInput.WasUseKeyPressedThisFrame(useKey))
            {
                return;
            }

            TryHandleCarafeDropOrUse();
        }

        public bool TryHandleCarafeDropOrUse()
        {
            CarafePickupItem item = carafeEquipment != null ? carafeEquipment.CurrentItem : null;
            if (item == null)
            {
                Log("G ignored — no carafe equipped.");
                return false;
            }

            if (TryGetTargetFireZone(out FireTargetZone zone))
            {
                return TryConsumeCarafeOnFire(item, zone);
            }

            Log("G treated as drop — player is not inside a matching fire zone (move closer or aim at the fire).");
            return TryDropCarafeToAnchor(item);
        }

        private bool TryConsumeCarafeOnFire(CarafePickupItem item, FireTargetZone zone)
        {
            if (IsGrowingFire)
            {
                Log("G ignored — carafe fire growth already in progress.");
                return false;
            }

            item.ConsumeOnFire();
            carafeEquipment.NotifyConsumed(item);
            onCarafeUsedOnFire?.Invoke();

            FireSource targetSource = ResolveTargetFireSource(zone);
            if (targetSource != null)
            {
                _growRoutine = StartCoroutine(GrowFireSourceRoutine(targetSource));
            }
            else
            {
                CompleteCarafeFireGrow();
            }

            if (_resetRoutine != null)
            {
                StopCoroutine(_resetRoutine);
            }

            _resetRoutine = StartCoroutine(ResetAfterDelayRoutine(item));

            Log(zone != null
                ? $"Carafe poured on fire zone '{zone.name}' — growing over {growDuration:F1}s."
                : $"Carafe poured inside fire zone — growing over {growDuration:F1}s.");
            return true;
        }

        private IEnumerator ResetAfterDelayRoutine(CarafePickupItem consumed)
        {
            if (vfxResetDelaySeconds > 0f)
            {
                CacheResetWait();
                yield return _cachedResetWait;
            }

            if (consumed != null)
            {
                consumed.RestoreAfterUse();
            }

            onCarafeReset?.Invoke();
            _resetRoutine = null;
            Log($"Carafe reset after {vfxResetDelaySeconds:F1}s — CarafeAndVfx off, Carafe restored (unusable).");
        }

        private void CacheResetWait()
        {
            if (_cachedResetWait != null)
            {
                return;
            }

            if (vfxResetDelaySeconds > 0f)
            {
                _cachedResetWait = new WaitForSeconds(vfxResetDelaySeconds);
            }
        }

        private IEnumerator GrowFireSourceRoutine(FireSource source)
        {
            IsGrowingFire = true;

            Transform visualRoot = ResolveFireVisualRoot(source);
            Vector3 startScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            Vector3 targetScale = startScale * fireGrowMultiplier;

            // Re-ignite zones to full so the fire visibly flares up rather than dying down.
            RestoreZonesToFull(source);

            float elapsed = 0f;
            while (visualRoot != null && elapsed < growDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / growDuration);
                visualRoot.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            if (visualRoot != null)
            {
                visualRoot.localScale = targetScale;
            }

            IsGrowingFire = false;
            _growRoutine = null;
            CompleteCarafeFireGrow();
        }

        private void CompleteCarafeFireGrow()
        {
            onCarafeFireGrown?.Invoke();
            CarafeFireGrown?.Invoke();
            Log("Target fire finished growing from carafe.");
        }

        private static Transform ResolveFireVisualRoot(FireSource source)
        {
            if (source == null)
            {
                return null;
            }

            Transform effects = source.transform.Find("Effects");
            if (effects != null)
            {
                return effects;
            }

            Transform root = source.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.name == "Effects")
                {
                    return child;
                }
            }

            return source.transform;
        }

        private static void RestoreZonesToFull(FireSource source)
        {
            if (source == null)
            {
                return;
            }

            IReadOnlyList<FireTargetZone> zones = source.Zones;
            int zoneCount = zones.Count;
            for (int i = 0; i < zoneCount; i++)
            {
                FireTargetZone zone = zones[i];
                if (zone == null || zone.MaxIntensity <= 0f)
                {
                    continue;
                }

                float missing = zone.MaxIntensity - zone.CurrentIntensity;
                if (missing > 0f)
                {
                    InvokeApplyIntensification(zone, missing);
                }
            }
        }

        private static void InvokeApplyIntensification(FireTargetZone zone, float amount)
        {
            if (_applyIntensificationMethod == null)
            {
                _applyIntensificationMethod = typeof(FireTargetZone).GetMethod(
                    "ApplyIntensification",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic
                    | BindingFlags.Public);
            }

            if (_applyIntensificationMethod == null)
            {
                return;
            }

            IntensificationArgs[0] = amount;
            _applyIntensificationMethod.Invoke(zone, IntensificationArgs);
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

            RefreshFireSourcesCache(force: false);
            for (int i = 0; i < _cachedFireSources.Length; i++)
            {
                FireSource source = _cachedFireSources[i];
                if (source != null)
                {
                    return source;
                }
            }

            return null;
        }

        private bool TryDropCarafeToAnchor(CarafePickupItem item)
        {
            item.DropFromPlayer();
            carafeEquipment.NotifyDropped(item);
            Log("Carafe returned to drop anchor.");
            return true;
        }

        private void EnsureFireZoneCacheForFrame()
        {
            int frame = Time.frameCount;
            if (_fireZoneCacheFrame == frame)
            {
                return;
            }

            _fireZoneCacheFrame = frame;
            _cachedInsideFireZone = CheckInsideFireZone(out _cachedMatchedZone);
        }

        private void InvalidateFireZoneCache()
        {
            _fireZoneCacheFrame = -1;
            _cachedInsideFireZone = false;
            _cachedMatchedZone = null;
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

            int hitCount = Physics.OverlapSphereNonAlloc(
                reference.position,
                fireZoneProbeRadius,
                _overlapBuffer,
                fireZoneLayerMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = _overlapBuffer[i];
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
            RefreshFireSourcesCache(force: false);

            FireSource closestSource = null;
            float closestDistance = fireZoneProbeRadius;

            for (int i = 0; i < _cachedFireSources.Length; i++)
            {
                FireSource source = _cachedFireSources[i];
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
            if (carafeEquipment == null || !carafeEquipment.TryGetCrosshairRay(out Ray ray))
            {
                return false;
            }

            if (!Physics.Raycast(ray, out RaycastHit hit, fireZoneRaycastDistance, ~0, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            RefreshFireSourcesCache(force: false);

            float aimRadius = fireZoneProbeRadius * 1.5f;
            FireSource bestSource = null;
            float bestDistance = aimRadius;

            for (int i = 0; i < _cachedFireSources.Length; i++)
            {
                FireSource source = _cachedFireSources[i];
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
            int zoneCount = zones.Count;
            for (int i = 0; i < zoneCount; i++)
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
            if (carafeEquipment == null || !carafeEquipment.TryGetCrosshairRay(out Ray ray))
            {
                return false;
            }

            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _raycastBuffer,
                fireZoneRaycastDistance,
                fireZoneLayerMask,
                QueryTriggerInteraction.Collide);

            if (hitCount <= 0)
            {
                return false;
            }

            float bestDistance = float.MaxValue;
            FireTargetZone bestZone = null;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _raycastBuffer[i];
                Collider collider = hit.collider;
                if (collider == null)
                {
                    continue;
                }

                FireTargetZone zone = ResolveFireTargetZone(collider);
                if (!IsAcceptedFireZone(zone))
                {
                    continue;
                }

                if (hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                bestZone = zone;
            }

            if (bestZone == null)
            {
                return false;
            }

            matchedZone = bestZone;
            return true;
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

            if (carafeEquipment != null
                && carafeEquipment.CurrentItem != null
                && carafeEquipment.EquipAnchor != null)
            {
                return carafeEquipment.EquipAnchor;
            }

            if (carafeEquipment != null)
            {
                return carafeEquipment.transform;
            }

            return ResolvePlayerTransform();
        }

        private Transform ResolvePlayerTransform()
        {
            if (_playerTransformResolved && _cachedPlayerTransform != null)
            {
                return _cachedPlayerTransform;
            }

            if (!_playerTransformResolved)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                _cachedPlayerTransform = player != null ? player.transform : transform;
                _playerTransformResolved = true;
            }

            return _cachedPlayerTransform != null ? _cachedPlayerTransform : transform;
        }

        private void RefreshFireSourcesCache(bool force)
        {
            float now = Time.unscaledTime;
            if (!force
                && _cachedFireSources != null
                && _cachedFireSources.Length > 0
                && now - _fireSourcesCacheTime < FireSourceCacheRefreshSeconds
                && !HasDestroyedFireSource())
            {
                return;
            }

            _cachedFireSources = FindObjectsByType<FireSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                ?? Array.Empty<FireSource>();
            _fireSourcesCacheTime = now;
        }

        private bool HasDestroyedFireSource()
        {
            for (int i = 0; i < _cachedFireSources.Length; i++)
            {
                if (_cachedFireSources[i] == null)
                {
                    return true;
                }
            }

            return false;
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.Log($"[CarafeUseController] {message}", this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform reference = ResolveDistanceReference();
            if (reference == null)
            {
                return;
            }

            Gizmos.color = IsInsideFireZone ? Color.red : Color.cyan;
            Gizmos.DrawWireSphere(reference.position, fireZoneProbeRadius);
        }
#endif
    }
}
