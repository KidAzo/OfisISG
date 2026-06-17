using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace WoiUtils.AudioSystem
{
    /// <summary>
    /// Designer-friendly, zero-code audio trigger.
    /// Bridges scene events (OnEnable/Trigger/Collision/UI) to AudioSystem.Play().
    /// Call <see cref="StopInstances"/> from UnityEvent / SOAP to stop all voices for this trigger's sound(s).
    /// All behavior rules (Queue, Cooldown, SingleGlobal, MaxVoices, etc.) are driven by SoundDefinition.
    /// Multiple instances may exist on the same GameObject (e.g. different UnityEvent sources or sounds).
    /// </summary>
    public class AudioTrigger : MonoBehaviour
    {
        public enum FireMode
        {
            Manual,          // Call Play() from UI button or other UnityEvent
            OnEnable,
            OnDisable,
            OnTriggerEnter,
            OnTriggerExit,
            OnCollisionEnter,
            OnCollisionExit
        }

        public enum SpatialMode
        {
            UseSoundDefinition,   // No context override. Spatial settings come from SoundDefinition.
            WorldPosition,        // Play at a specific world position
            FollowTransform       // Follow a transform (3D audio)
        }

        [Header("Target")]
        [Tooltip("Optional: holds EN + TR SoundDefinitions; picks one from LocalizationService. When set, overrides single Sound below.")]
        [SerializeField] private LocalizedSoundDefinition localizedSound;

        [Tooltip(
            "Single SoundDefinition, or fallback when Localized Sound is used but one language slot is empty. " +
            "For overlapping / stacked playback of the same asset, set Instance Mode to Multiple on that SoundDefinition.")]
        [SerializeField] private SoundDefinition sound;

        [Header("Trigger")]
        [Tooltip("When should this component fire?")]
        [SerializeField] private FireMode fireMode = FireMode.OnEnable;

        [Tooltip("Extra per-trigger anti-spam cooldown (in seconds). " +
                 "This is independent from SoundDefinition.cooldown. " +
                 "Not applied to Play() / PlayWithNoCooldown (UnityEvent / UI).")]
        [SerializeField] private float triggerCooldown = 0f;

        [Header("Clip Override (Optional)")]
        [Tooltip("If enabled, plays a specific clip index from SoundDefinition.")]
        [SerializeField] private bool overrideClipIndex = false;

        [SerializeField] private int clipIndex = 0;


        [Tooltip("If enabled, prevents multiple fires within the same frame.")]
        [SerializeField] private bool blockSameFrame = true;

        [Header("Spatial")]
        [Tooltip("How to determine where the sound should play.")]
        [SerializeField] private SpatialMode spatialMode = SpatialMode.UseSoundDefinition;

        [Tooltip("Used in WorldPosition mode. If null, uses this transform position.")]
        [SerializeField] private Transform positionSource;

        [Tooltip("Used in FollowTransform mode. If null, follows this transform.")]
        [SerializeField] private Transform followTarget;

        [Header("Multipliers (Optional)")]
        [Tooltip("Volume multiplier (0 = mute, 1 = default, up to 1.5 for safe boost).")]
        [Range(0f, 1.5f)]
        [SerializeField] private float volumeMul = 1f;


        [Tooltip("Pitch multiplier applied via PlayContext. Set to 1 for no change.")]
        [SerializeField] private float pitchMul = 1f;

        [Header("Events (Optional)")]
        [Tooltip("Invoked after a successful fire (a Play call is issued).")]
        public UnityEvent onPlayed;

        [Tooltip("Invoked if the trigger was blocked (missing refs, cooldown, etc.).")]
        public UnityEvent onBlocked;

        private float lastTriggerTime = -999f;
        private int lastFrame = -1;

        /// <summary>Cached instance from <see cref="AudioSystem.TryGetFromServiceLocator"/> (no scene reference).</summary>
        private AudioSystem _cachedSystem;
        private bool _loggedSceneAudioFallback;

        /// <summary>Serialized single sound (when <see cref="localizedSound"/> is null).</summary>
        public SoundDefinition Sound => sound;

        /// <summary><see cref="LocalizedSoundDefinition"/> when assigned.</summary>
        public LocalizedSoundDefinition LocalizedSound => localizedSound;

        /// <summary>Resolved <see cref="AudioSystem"/> from ServiceLocator, if any.</summary>
        public AudioSystem AudioSystem => _cachedSystem;

        /// <summary>Sound that will play for the current UI language (localized pair or single <see cref="sound"/>).</summary>
        public SoundDefinition ResolvePlaySound() =>
            localizedSound != null ? localizedSound.ResolveForCurrentLanguage() : sound;

        private void Start()
        {
            ResolveAudioSystem();
        }

        private void ResolveAudioSystem()
        {
            if (_cachedSystem != null)
                return;

            if (AudioSystem.TryGetFromServiceLocator(out _cachedSystem) && _cachedSystem != null)
                return;

            // ServiceLocator registration happens in AudioSystem.Awake — UI may fire earlier or bootstrap order may differ.
            _cachedSystem = UnityEngine.Object.FindFirstObjectByType<AudioSystem>();

            if (_cachedSystem != null && !_loggedSceneAudioFallback)
            {
                _loggedSceneAudioFallback = true;
                Debug.LogWarning(
                    "[AudioTrigger] AudioSystem was not on ServiceLocator; using first AudioSystem in loaded scenes. " +
                    "Prefer registering AudioSystem on ServiceLocator (bootstrap / earlier scene).",
                    this);
            }
        }

        private bool TryResolveLiveAudioSystem(out AudioSystem system)
        {
            system = null;

            if (AudioSystem.IsShuttingDown)
                return false;

            ResolveAudioSystem();
            system = _cachedSystem;
            return system != null;
        }

        private void OnEnable()
        {
            _cachedSystem = null;

            if (fireMode == FireMode.OnEnable)
                StartCoroutine(FireNextFrame());
        }

        private IEnumerator FireNextFrame()
        {
            yield return null;
            TryFire();
        }

        private void OnDisable()
        {
            if (fireMode == FireMode.OnDisable)
                TryFire();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (fireMode == FireMode.OnTriggerEnter)
                TryFire();
        }

        private void OnTriggerExit(Collider other)
        {
            if (fireMode == FireMode.OnTriggerExit)
                TryFire();
        }

        private void OnCollisionEnter(Collision other)
        {
            if (fireMode == FireMode.OnCollisionEnter)
                TryFire();
        }

        private void OnCollisionExit(Collision other)
        {
            if (fireMode == FireMode.OnCollisionExit)
                TryFire();
        }

        /// <summary>
        /// Manual fire from UI / UnityEvent. Matches editor "Test Fire" behavior: bypasses sound cooldown and trigger anti-spam.
        /// </summary>
        public void Play() => TryFire(ignoreCooldowns: true, manualFromUnityEvent: true);

        public void PlayWithNoCooldown() => TryFire(ignoreCooldowns: true, manualFromUnityEvent: true);

        /// <summary>
        /// Stops every active voice playing this trigger's <see cref="SoundDefinition"/>(s).
        /// When <see cref="localizedSound"/> is set, stops both English and Turkish definitions so either slot cannot keep looping after a language switch.
        /// Safe to wire from UnityEvent or SOAP (no trigger cooldown / same-frame blocking).
        /// </summary>
        public void StopInstances()
        {
            if (AudioSystem.IsShuttingDown)
                return;

            ResolveAudioSystem();

            if (_cachedSystem == null)
            {
                Debug.LogWarning(
                    "[AudioTrigger:" + name + "] StopInstances: No AudioSystem — nothing registered on ServiceLocator and no instance found in loaded scenes.",
                    this);
                return;
            }

            if (localizedSound != null)
            {
                if (localizedSound.english != null)
                    _cachedSystem.StopAllInstances(localizedSound.english);
                if (localizedSound.turkish != null)
                    _cachedSystem.StopAllInstances(localizedSound.turkish);
                return;
            }

            if (sound != null)
                _cachedSystem.StopAllInstances(sound);
        }

        private void TryFire(bool ignoreCooldowns = false, bool manualFromUnityEvent = false)
        {
            if (!TryResolveLiveAudioSystem(out AudioSystem liveSystem))
            {
                if (AudioSystem.IsShuttingDown)
                {
                    LogBlocked("AudioSystem is shutting down.");
                }
                else
                {
                    LogBlocked(
                        "No AudioSystem — nothing registered on ServiceLocator and no instance found in loaded scenes. " +
                        "Add AudioSystem (bootstrap), enable Register With Service Locator, or load it before this UI.");
                }

                onBlocked?.Invoke();
                return;
            }

            SoundDefinition playSound = ResolvePlaySound();
            if (playSound == null)
            {
                LogBlocked(
                    localizedSound != null
                        ? "Localized Sound resolved to null — assign English and/or Turkish SoundDefinitions inside that asset."
                        : "No SoundDefinition — assign Localized Sound or Sound Definition.");
                onBlocked?.Invoke();
                return;
            }

            _cachedSystem = liveSystem;

            // Trigger-level anti-spam (independent from SoundDefinition.cooldown).
            float now = Time.unscaledTime;

            if (!manualFromUnityEvent && blockSameFrame && Time.frameCount == lastFrame)
            {
                LogBlocked("Blocked: block same frame (two Play calls in one frame).");
                onBlocked?.Invoke();
                return;
            }

            if (!manualFromUnityEvent && triggerCooldown > 0f && (now - lastTriggerTime) < triggerCooldown)
            {
                LogBlocked("Blocked: trigger cooldown.");
                onBlocked?.Invoke();
                return;
            }

            lastFrame = Time.frameCount;
            lastTriggerTime = now;

            // Build context (position/follow + multipliers).
            var ctx = BuildContext();
            // All rules (Queue, SingleGlobal, Cooldown, MaxVoices, etc.) are handled inside AudioSystem.
           
            ctx.ignoreCooldowns = ignoreCooldowns;

           // Spatial routing
            switch (spatialMode)
            {
                case SpatialMode.WorldPosition:
                {
                    var pos = positionSource != null ? positionSource.position : transform.position;

                    //pass ctx so clipIndex/volume/pitch/ignoreCooldowns are preserved
                    _cachedSystem.PlayAt(playSound, pos, ctx);
                    break;
                }
                case SpatialMode.FollowTransform:
                {
                    var t = followTarget != null ? followTarget : transform;

                    //pass ctx so clipIndex/volume/pitch/ignoreCooldowns are preserved
                    _cachedSystem.PlayFollow(playSound, t, ctx);
                    break;
                }
                default:
                    _cachedSystem.Play(playSound, ctx);
                    break;
            }

            onPlayed?.Invoke();
        }

        private void LogBlocked(string reason)
        {
            Debug.LogWarning($"[AudioTrigger:{name}] {reason}", this);
        }
 
        private PlayContext BuildContext()
        {
            var ctx = PlayContext.Default;

            ctx.volumeMul = volumeMul;
            ctx.pitchMul  = pitchMul;

            if (overrideClipIndex)
                ctx = ctx.SetClipIndex(clipIndex);

            return ctx;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (volumeMul < 0f) volumeMul = 0f;
            if (pitchMul <= 0f) pitchMul = 1f;
            if (triggerCooldown < 0f) triggerCooldown = 0f;
        }
#endif
    }
}
