using System;
using System.Collections;
using FireExtinguisher.Core;
using UnityEngine;
using WoiUtils.AudioSystem;

namespace Woi.Game.VFX
{
    /// <summary>
    /// Sıkma: önce <b>start</b> (one-shot) biter, hâlâ basılıysa <b>loop</b> başlar.
    /// Bırakınca loop kesilir; yalnızca loop gerçekten çalmışsa <b>end</b> çalar.
    /// Start süresi boyunca bırakırsan end çalmaz, start kesilir.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/VFX/Extinguisher Spray Loop Audio")]
    public sealed class ExtinguisherSprayLoopAudio : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private ExtinguisherController _controller;

        [Tooltip("Boşsa ServiceLocator / sahne içi ilk AudioSystem.")]
        [SerializeField] private AudioSystem _audioSystem;

        [Tooltip("Nozzle veya kök; null ise controller transform.")]
        [SerializeField] private Transform _followTransform;

        [Header("Sounds")]
        [Tooltip("Sıkma başında; bitince loop girer. Boşsa doğrudan loop.")]
        [SerializeField] private SoundDefinition _sprayStartSound;

        [Tooltip("Start bittikten sonra, basılı tutarken; asset'te Loop açık olmalı.")]
        [SerializeField] private SoundDefinition _sprayLoopSound;

        [Tooltip("Yalnızca loop çaldıktan sonra bırakınca çalar.")]
        [SerializeField] private SoundDefinition _sprayEndSound;

        [Header("Play context")]
        [Tooltip("Sıkma tekrarlarında SoundDefinition cooldown'ını atla.")]
        [SerializeField] private bool _ignoreCooldowns = true;

        [Tooltip(
            "Açıkken: (1) Single Per Category ön kesimi atlanır. (2) Ses havuzu doluysa yalnızca aynı **custom category key** " +
            "olan eski bir ses çalınır; genel SFX enum kategorisindeki diğer seslere dokunulmaz. " +
            "Start / Loop / End asset'lerinde Use Custom Category + aynı key (ör. extinguisher_spray) kullan.")]
        [SerializeField] private bool _suppressSameCategorySteal = true;

        private bool _loggedMissingController;
        private bool _loggedMissingAudio;

        /// <summary>True from <see cref="HandleSprayStarted"/> until <see cref="HandleSprayStopped"/>.</summary>
        private bool _spraySessionActive;

        private bool _loopPlaying;

        private AudioVoice _startVoice;
        private Action<int> _startCompletedHandler;

        private Coroutine _startFallbackRoutine;

        private void OnEnable()
        {
            if (!ValidateController())
                return;

            _controller.OnSprayStarted += HandleSprayStarted;
            _controller.OnSprayStopped += HandleSprayStopped;
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnSprayStarted -= HandleSprayStarted;
                _controller.OnSprayStopped -= HandleSprayStopped;
            }

            CleanupAllSprayAudio(playEnd: false);
        }

        private void HandleSprayStarted()
        {
            if (AudioSystem.IsShuttingDown)
                return;

            if (!ResolveAudioSystem())
                return;

            CleanupAllSprayAudio(playEnd: false);

            _spraySessionActive = true;
            _loopPlaying = false;

            Transform follow = ResolveFollow();
            PlayContext ctx = BuildContext();

            if (_sprayStartSound == null)
            {
                TryBeginLoop();
                return;
            }

            AudioVoice voice = _audioSystem.PlayFollow(_sprayStartSound, follow, ctx);
            if (voice != null)
            {
                _startVoice = voice;
                int capturedGen = voice.Generation;
                _startCompletedHandler = gen =>
                {
                    if (gen != capturedGen)
                        return;

                    UnhookStartVoice();
                    OnStartSoundFinishedNaturally();
                };
                _startVoice.OnCompleted += _startCompletedHandler;
                return;
            }

            // Play() null (ör. kuyruk) — ilk clip süresiyle yaklaşık bekleme
            _startFallbackRoutine = StartCoroutine(CoWaitStartClipThenMaybeLoop());
        }

        private void HandleSprayStopped()
        {
            if (AudioSystem.IsShuttingDown)
                return;

            _spraySessionActive = false;

            UnhookStartVoice();
            if (_startFallbackRoutine != null)
            {
                StopCoroutine(_startFallbackRoutine);
                _startFallbackRoutine = null;
            }

            if (_loopPlaying)
            {
                StopLoopOnly();
                _loopPlaying = false;

                if (ResolveAudioSystem() && _sprayEndSound != null)
                    _audioSystem.PlayFollow(_sprayEndSound, ResolveFollow(), BuildContext());
            }
            else
            {
                if (ResolveAudioSystem() && _sprayStartSound != null)
                    _audioSystem.StopAllInstances(_sprayStartSound);
            }
        }

        private void OnStartSoundFinishedNaturally()
        {
            if (!_spraySessionActive)
                return;

            TryBeginLoop();
        }

        private void TryBeginLoop()
        {
            if (!_spraySessionActive || _loopPlaying)
                return;

            if (_sprayLoopSound == null)
                return;

            if (!ResolveAudioSystem())
                return;

            AudioVoice loopVoice = _audioSystem.PlayFollow(_sprayLoopSound, ResolveFollow(), BuildContext());
            if (loopVoice != null)
                _loopPlaying = true;
        }

        private IEnumerator CoWaitStartClipThenMaybeLoop()
        {
            float t = EstimateStartClipDurationSeconds();
            if (t > 0f)
                yield return new WaitForSecondsRealtime(t);

            _startFallbackRoutine = null;
            OnStartSoundFinishedNaturally();
        }

        private float EstimateStartClipDurationSeconds()
        {
            if (_sprayStartSound == null || _sprayStartSound.clips == null || _sprayStartSound.clips.Count == 0)
                return 0.05f;

            AudioClip c = _sprayStartSound.clips[0].clip;
            if (c == null)
                return 0.05f;

            float len = c.length;
            return len > 0f ? len : 0.05f;
        }

        private void UnhookStartVoice()
        {
            if (_startVoice != null && _startCompletedHandler != null)
            {
                _startVoice.OnCompleted -= _startCompletedHandler;
                _startCompletedHandler = null;
                _startVoice = null;
            }
        }

        private void CleanupAllSprayAudio(bool playEnd)
        {
            UnhookStartVoice();
            if (_startFallbackRoutine != null)
            {
                StopCoroutine(_startFallbackRoutine);
                _startFallbackRoutine = null;
            }

            if (_audioSystem != null)
            {
                if (_sprayLoopSound != null)
                    _audioSystem.StopAllInstances(_sprayLoopSound);

                if (_sprayStartSound != null)
                    _audioSystem.StopAllInstances(_sprayStartSound);
            }

            if (playEnd && _audioSystem != null && _sprayEndSound != null)
                _audioSystem.PlayFollow(_sprayEndSound, ResolveFollow(), BuildContext());

            _spraySessionActive = false;
            _loopPlaying = false;
        }

        private void StopLoopOnly()
        {
            if (_audioSystem == null || _sprayLoopSound == null)
                return;

            _audioSystem.StopAllInstances(_sprayLoopSound);
        }

        private Transform ResolveFollow() =>
            _followTransform != null ? _followTransform : _controller.transform;

        private PlayContext BuildContext()
        {
            PlayContext ctx = PlayContext.Follow(ResolveFollow());
            if (_ignoreCooldowns)
                ctx.ignoreCooldowns = true;
            // Without a shared custom key, suppress would block pool steal and Play() can return null when the pool is full.
            if (_suppressSameCategorySteal && SpraySoundsHaveSharedCustomCategoryKey())
                ctx.suppressSameCategorySteal = true;
            // QueueAll / queue routing returns null — spray start/loop/end must always resolve a clip immediately.
            ctx.forceImmediatePlay = true;
            return ctx;
        }

        /// <summary>
        /// True when every non-null spray def uses the same non-empty <see cref="SoundDefinition.customCategoryKey"/>.
        /// </summary>
        private bool SpraySoundsHaveSharedCustomCategoryKey()
        {
            SoundDefinition[] defs = { _sprayStartSound, _sprayLoopSound, _sprayEndSound };
            string key = null;
            int assigned = 0;

            foreach (SoundDefinition d in defs)
            {
                if (d == null)
                    continue;

                assigned++;
                if (!d.useCustomCategory || string.IsNullOrEmpty(d.customCategoryKey))
                    return false;

                if (key == null)
                    key = d.customCategoryKey;
                else if (!string.Equals(key, d.customCategoryKey, StringComparison.Ordinal))
                    return false;
            }

            return assigned > 0;
        }

        private bool ValidateController()
        {
            if (_controller != null)
                return true;

            if (!_loggedMissingController)
            {
                Debug.LogWarning($"[{nameof(ExtinguisherSprayLoopAudio)}] Assign ExtinguisherController on {name}.", this);
                _loggedMissingController = true;
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
                Debug.LogWarning($"[{nameof(ExtinguisherSprayLoopAudio)}] No AudioSystem — assign or register on ServiceLocator.", this);
                _loggedMissingAudio = true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_sprayStartSound != null && _sprayStartSound.loop)
            {
                Debug.LogWarning(
                    $"[{nameof(ExtinguisherSprayLoopAudio)}] Spray Start Sound '{_sprayStartSound.name}' has Loop enabled — start should be one-shot so loop can begin after it ends.",
                    this);
            }

            if (_sprayLoopSound != null && !_sprayLoopSound.loop)
            {
                Debug.LogWarning(
                    $"[{nameof(ExtinguisherSprayLoopAudio)}] Spray Loop Sound '{_sprayLoopSound.name}' has Loop disabled in SoundDefinition — enable Loop for continuous spray audio.",
                    this);
            }

            if (_suppressSameCategorySteal && !SpraySoundsHaveSharedCustomCategoryKey())
                WarnIfSpraySoundsLackSharedCustomCategory();
        }

        private void WarnIfSpraySoundsLackSharedCustomCategory()
        {
            SoundDefinition[] defs = { _sprayStartSound, _sprayLoopSound, _sprayEndSound };
            string key = null;
            foreach (SoundDefinition d in defs)
            {
                if (d == null)
                    continue;
                if (!d.useCustomCategory || string.IsNullOrEmpty(d.customCategoryKey))
                {
                    Debug.LogWarning(
                        $"[{nameof(ExtinguisherSprayLoopAudio)}] '{d.name}' should use Custom Category + key (same on start/loop/end) so " +
                        $"Suppress Same Category Steal can avoid cutting other SFX and only recycle this spray group when the voice pool is full.",
                        this);
                    return;
                }

                if (key == null)
                    key = d.customCategoryKey;
                else if (!string.Equals(key, d.customCategoryKey, StringComparison.Ordinal))
                {
                    Debug.LogWarning(
                        $"[{nameof(ExtinguisherSprayLoopAudio)}] Spray sounds use different customCategoryKey values — pool recycle may not replace the right voice.",
                        this);
                    return;
                }
            }
        }
#endif
    }
}
