using FireExtinguisher.Core;
using UnityEngine;
using WoiUtils.AudioSystem;

namespace Woi.Game.VFX
{
    /// <summary>
    /// Döngü yangın sesini <see cref="FireSource.CurrentNormalizedIntensity"/> ile orantılı kısar.
    /// <see cref="TrainingFireSelectionState"/> (veya benzeri) UnityEvent’te <see cref="AudioTrigger.Play"/> yerine
    /// <see cref="Play"/> / <see cref="Stop"/> kullanın; aynı clip için eski <see cref="AudioTrigger"/>’ı devre dışı bırakın.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/VFX/Fire Source Intensity Loop Audio")]
    public sealed class FireSourceIntensityLoopAudio : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Boşsa bu obje veya parent’ta FireSource aranır.")]
        [SerializeField] private FireSource _fireSource;

        [Tooltip("Boşsa ServiceLocator / sahne içi ilk AudioSystem.")]
        [SerializeField] private AudioSystem _audioSystem;

        [Tooltip("3D takip; boşsa FireSource transform.")]
        [SerializeField] private Transform _followTransform;

        [Header("Sounds")]
        [Tooltip("AudioTrigger’daki gibi; doluysa SoundDefinition yerine dil seçimine göre çalar.")]
        [SerializeField] private LocalizedSoundDefinition _localizedSound;

        [SerializeField] private SoundDefinition _sound;

        [Header("Play context")]
        [Tooltip("Ses tanımındaki volume ile çarpılır (AudioTrigger volumeMul ile aynı rol).")]
        [Range(0f, 1.5f)]
        [SerializeField] private float _volumeMul = 1f;

        [Tooltip("Pitch çarpanı (PlayContext).")]
        [SerializeField] private float _pitchMul = 1f;

        [SerializeField] private bool _ignoreCooldowns = true;

        [Tooltip(
            "Açıkken: havuz doluyken yalnızca aynı custom category key’li sesler yeniden kullanılır. " +
            "SoundDefinition’da Use Custom Category + benzersiz key (ör. fire_loop_A) kullanın.")]
        [SerializeField] private bool _suppressSameCategorySteal = true;

        [Header("Lifecycle")]
        [Tooltip("Tamamen sönünce döngüyü durdur (OnFullyExtinguished).")]
        [SerializeField] private bool _stopWhenFullyExtinguished = true;

        private AudioVoice _voice;
        private bool _loggedMissingFire;
        private bool _loggedMissingAudio;
        private bool _loggedMissingSound;

        private void Awake()
        {
            if (_fireSource == null)
                _fireSource = GetComponent<FireSource>() ?? GetComponentInParent<FireSource>();
        }

        private void OnEnable()
        {
            if (_fireSource == null)
                return;

            _fireSource.OnIntensityChanged += HandleIntensityChanged;

            if (_stopWhenFullyExtinguished)
                _fireSource.OnFullyExtinguished += HandleFullyExtinguished;
        }

        private void OnDisable()
        {
            if (_fireSource != null)
            {
                _fireSource.OnIntensityChanged -= HandleIntensityChanged;
                _fireSource.OnFullyExtinguished -= HandleFullyExtinguished;
            }

            StopTrackedVoiceOnly();
        }

        /// <summary>UnityEvent / seçim: yangın döngüsünü başlatır; yoğunluğa göre ses seviyesini günceller.</summary>
        public void Play()
        {
            if (AudioSystem.IsShuttingDown)
                return;

            if (!ValidateFireSource())
                return;

            if (!ResolveAudioSystem())
                return;

            SoundDefinition playSound = ResolvePlaySound();
            if (playSound == null)
            {
                if (!_loggedMissingSound)
                {
                    Debug.LogWarning(
                        $"[{nameof(FireSourceIntensityLoopAudio)}] No SoundDefinition (assign Localized Sound slots or Sound) on {name}.",
                        this);
                    _loggedMissingSound = true;
                }

                return;
            }

            if (!playSound.loop)
            {
                Debug.LogWarning(
                    $"[{nameof(FireSourceIntensityLoopAudio)}] '{playSound.name}' is not a looping SoundDefinition — enable Loop for continuous fire audio.",
                    this);
            }

            if (_voice != null && _voice.IsPlaying())
            {
                ApplyVolumeForIntensity(_fireSource.CurrentNormalizedIntensity);
                return;
            }

            StopTrackedVoiceOnly();

            Transform follow = _followTransform != null ? _followTransform : _fireSource.transform;
            PlayContext ctx = BuildContext(playSound);

            _voice = _audioSystem.PlayFollow(playSound, follow, ctx);
            if (_voice == null)
            {
                Debug.LogWarning(
                    $"[{nameof(FireSourceIntensityLoopAudio)}] PlayFollow returned null (queue / pool). Check SoundDefinition schedule and voice limits.",
                    this);
                return;
            }

            ApplyVolumeForIntensity(_fireSource.CurrentNormalizedIntensity);
        }

        /// <summary>UnityEvent: döngüyü keser (AudioTrigger.StopInstances benzeri, yalnızca bu bileşenin sesi).</summary>
        public void Stop() => StopTrackedVoiceOnly();

        private void HandleIntensityChanged(float normalizedIntensity) =>
            ApplyVolumeForIntensity(normalizedIntensity);

        private void HandleFullyExtinguished()
        {
            if (_stopWhenFullyExtinguished)
                StopTrackedVoiceOnly();
        }

        private void ApplyVolumeForIntensity(float normalizedIntensity)
        {
            if (_voice == null || !_voice.IsPlaying())
                return;

            SoundDefinition data = _voice.Data;
            if (data == null)
                return;

            float baseMax = Mathf.Clamp(data.volume * _volumeMul, 0f, 1.5f);
            float v = baseMax * Mathf.Clamp01(normalizedIntensity);
            _voice.SetVolume(v, 0f);
        }

        private void StopTrackedVoiceOnly()
        {
            if (_voice != null)
            {
                _voice.Stop();
                _voice = null;
            }
        }

        private SoundDefinition ResolvePlaySound() =>
            _localizedSound != null ? _localizedSound.ResolveForCurrentLanguage() : _sound;

        private PlayContext BuildContext(SoundDefinition playSound)
        {
            Transform follow = _followTransform != null ? _followTransform : _fireSource.transform;
            PlayContext ctx = PlayContext.Follow(follow);
            ctx.volumeMul = _volumeMul;
            ctx.pitchMul = _pitchMul <= 0f ? 1f : _pitchMul;

            if (_ignoreCooldowns)
                ctx.ignoreCooldowns = true;

            if (_suppressSameCategorySteal && LoopSoundUsesCustomCategory(playSound))
                ctx.suppressSameCategorySteal = true;

            ctx.forceImmediatePlay = true;
            return ctx;
        }

        private static bool LoopSoundUsesCustomCategory(SoundDefinition def) =>
            def != null && def.useCustomCategory && !string.IsNullOrEmpty(def.customCategoryKey);

        private bool ValidateFireSource()
        {
            if (_fireSource != null)
                return true;

            if (!_loggedMissingFire)
            {
                Debug.LogWarning($"[{nameof(FireSourceIntensityLoopAudio)}] Assign FireSource (or place on same object / parent).", this);
                _loggedMissingFire = true;
            }

            return false;
        }

        private bool ResolveAudioSystem()
        {
            if (_audioSystem != null)
                return true;

            if (AudioSystem.TryGetFromServiceLocator(out AudioSystem sys) && sys != null)
            {
                _audioSystem = sys;
                return true;
            }

            _audioSystem = FindFirstObjectByType<AudioSystem>();
            if (_audioSystem != null)
                return true;

            if (!_loggedMissingAudio)
            {
                Debug.LogWarning(
                    $"[{nameof(FireSourceIntensityLoopAudio)}] No AudioSystem — assign or register on ServiceLocator.",
                    this);
                _loggedMissingAudio = true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_pitchMul <= 0f)
                _pitchMul = 1f;

            if (_suppressSameCategorySteal && ResolvePlaySound() != null && !LoopSoundUsesCustomCategory(ResolvePlaySound()))
            {
                Debug.LogWarning(
                    $"[{nameof(FireSourceIntensityLoopAudio)}] Suppress Same Category Steal is on but '{ResolvePlaySound().name}' " +
                    "has no Custom Category + key — pool steal may cut unrelated SFX or Play may return null when the pool is full.",
                    this);
            }
        }
#endif
    }
}
