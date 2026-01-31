using UnityEngine;
using UnityEngine.Events;

namespace WoiUtils.AudioSystem
{
    /// <summary>
    /// Designer-friendly, zero-code audio trigger.
    /// Bridges scene events (OnEnable/Trigger/Collision/UI) to AudioSystem.Play().
    /// All behavior rules (Queue, Cooldown, SingleGlobal, MaxVoices, etc.) are driven by SoundDefinition.
    /// </summary>
    [DisallowMultipleComponent]
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
        [Tooltip("SoundDefinition to play when this trigger fires.")]
        [SerializeField] private SoundDefinition sound;

        [Header("Trigger")]
        [Tooltip("When should this component fire?")]
        [SerializeField] private FireMode fireMode = FireMode.OnEnable;

        [Tooltip("Extra per-trigger anti-spam cooldown (in seconds). " +
                 "This is independent from SoundDefinition.cooldown.")]
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

        [Header("Audio System Reference")]
        [Tooltip("If not assigned, the component will try to find an AudioSystem in the scene.")]
        [SerializeField] private AudioSystem audioSystem;

        [Header("Events (Optional)")]
        [Tooltip("Invoked after a successful fire (a Play call is issued).")]
        public UnityEvent onPlayed;

        [Tooltip("Invoked if the trigger was blocked (missing refs, cooldown, etc.).")]
        public UnityEvent onBlocked;

        private float lastTriggerTime = -999f;
        private int lastFrame = -1;

        //public pro
        public SoundDefinition Sound => sound;
        public AudioSystem AudioSystem => audioSystem;

        private void Awake()
        {
            // Designer-friendly: auto-resolve if not assigned.
            if (audioSystem == null)
                audioSystem = FindFirstObjectByType<AudioSystem>();
        }

        private void OnEnable()
        {
            if (fireMode == FireMode.OnEnable)
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
        /// Manual fire entry point. Use this in UI Buttons or any UnityEvent.
        /// </summary>
        public void Play() => TryFire();
        public void PlayWithNoCooldown() => TryFire(true);

        private void TryFire(bool ignoreCooldowns = false)
        {
            if (AudioSystem.IsShuttingDown) { onBlocked?.Invoke(); return; }
            if (sound == null) { onBlocked?.Invoke(); return; }

            if (audioSystem == null)
                audioSystem = FindFirstObjectByType<AudioSystem>();

            if (audioSystem == null) { onBlocked?.Invoke(); return; }

            // Trigger-level anti-spam (independent from SoundDefinition.cooldown).
            float now = Time.unscaledTime;

            if (blockSameFrame && Time.frameCount == lastFrame)
            {
                onBlocked?.Invoke();
                return;
            }

            if (triggerCooldown > 0f && (now - lastTriggerTime) < triggerCooldown)
            {
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
                    audioSystem.PlayAt(sound, pos);
                    break;
                }
                case SpatialMode.FollowTransform:
                {
                    var t = followTarget != null ? followTarget : transform;
                    audioSystem.PlayFollow(sound, t);
                    break;
                }
                default:
                    audioSystem.Play(sound, ctx);
                    break;
            }

            onPlayed?.Invoke();
        }
 
        private PlayContext BuildContext()
        {
            var ctx = PlayContext.Default;

            // Multipliers
            ctx.volumeMul = volumeMul;
            ctx.pitchMul = pitchMul;

            // Clip index override
           if (overrideClipIndex)
                ctx = ctx.SetClipIndex(clipIndex);

            // Spatial override
            switch (spatialMode)
            {
                case SpatialMode.UseSoundDefinition:
                    ctx.hasPosition = false;
                    ctx.hasFollow = false;
                    ctx.follow = null;
                    break;

                case SpatialMode.WorldPosition:
                    ctx.hasFollow = false;
                    ctx.follow = null;

                    ctx.hasPosition = true;
                    ctx.position = positionSource != null ? positionSource.position : transform.position;
                    break;

                case SpatialMode.FollowTransform:
                    ctx.hasPosition = false;

                    ctx.hasFollow = true;
                    ctx.follow = followTarget != null ? followTarget : transform;
                    break;
            }

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
