using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.Game.VFX
{
    /// <summary>
    /// Handles two per-tick VFX responsibilities:
    ///
    ///   1. IMPACT — At <see cref="ExtinguishResult.HitPoint"/> (clamped to zone collider when present).
    ///      On spray release emission stops without clearing so particles finish their lifetime (smooth tail-off).
    ///      Miss-while-spraying still clears impact immediately so hit points do not jump. Next spray start clears + plays from zero.
    ///
    ///   2. STREAM — startSpeed per tick for nozzle→hit (or miss) length.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExtinguisherImpactVFXPresenter : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private ExtinguisherController _controller;

        // ── Stream ─────────────────────────────────────────────────────────────

        [Header("Stream (length-matched to hit distance)")]
        [Tooltip("Stream ParticleSystems whose length should match the hit distance. " +
                 "Each PS uses its own startLifetime to compute the correct startSpeed per tick.")]
        [SerializeField] private ParticleSystem[] _streamParticleSystems;

        [Tooltip("Stream distance used when the spray is not hitting anything (miss / sweep).")]
        [SerializeField, Min(0.1f)] private float _missStreamDistance = 3f;

        // ── Impact ─────────────────────────────────────────────────────────────

        [Header("Impact effects (positioned at hit point)")]
        [Tooltip("ParticleSystems that play at the fire zone hit point (Impact, Dust, Smoke, etc.). " +
                 "Moved to the hit point and played only while actively hitting.")]
        [SerializeField] private ParticleSystem[] _impactParticleSystems;

        // ── Cached modules ─────────────────────────────────────────────────────

        private ParticleSystem.MainModule[] _streamMains;
        private float[]                     _streamLifetimes;

        // ── State ──────────────────────────────────────────────────────────────

        // Tracks whether we were hitting last tick.
        // Allows us to detect miss→hit and hit→miss transitions cleanly.
        private bool _impactActive;

        // Tracks whether stream PS are currently emitting.
        // Started on the first spray-evaluated frame; stopped when spray stops.
        private bool _streamActive;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (_streamParticleSystems != null)
            {
                int count        = _streamParticleSystems.Length;
                _streamMains     = new ParticleSystem.MainModule[count];
                _streamLifetimes = new float[count];

                for (int i = 0; i < count; i++)
                {
                    if (_streamParticleSystems[i] == null) continue;
                    _streamMains[i]     = _streamParticleSystems[i].main;
                    _streamLifetimes[i] = Mathf.Max(0.01f, _streamMains[i].startLifetime.constant);
                }
            }

            // Stop and clear all effects at startup regardless of Inspector state.
            StopStreamEffects(clear: true);
            StopImpactEffects(clear: true);

            WarnIfNoParticleReferences();
        }

        private void WarnIfNoParticleReferences()
        {
            bool anyStream = false;
            if (_streamParticleSystems != null)
            {
                foreach (var ps in _streamParticleSystems)
                {
                    if (ps != null) { anyStream = true; break; }
                }
            }

            bool anyImpact = false;
            if (_impactParticleSystems != null)
            {
                foreach (var ps in _impactParticleSystems)
                {
                    if (ps != null) { anyImpact = true; break; }
                }
            }

            if (!anyStream && !anyImpact)
                Debug.LogWarning(
                    $"[{nameof(ExtinguisherImpactVFXPresenter)}] '{name}' has no ParticleSystem references " +
                    "(Stream + Impact arrays empty or all null) — assign PS assets on the extinguisher / nozzle prefab.",
                    this);
            else if (!anyImpact)
                Debug.LogWarning(
                    $"[{nameof(ExtinguisherImpactVFXPresenter)}] '{name}' has no Impact ParticleSystems — " +
                    "hit-point VFX will not show when DidHitZone is true.",
                    this);
        }

        private void OnEnable()
        {
            if (_controller == null)
            {
                Debug.LogWarning($"[{nameof(ExtinguisherImpactVFXPresenter)}] ExtinguisherController not assigned on {gameObject.name}.", this);
                return;
            }

            _controller.OnSprayEvaluated += HandleSprayEvaluated;
            _controller.OnSprayStopped   += HandleSprayStopped;
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnSprayEvaluated -= HandleSprayEvaluated;
                _controller.OnSprayStopped   -= HandleSprayStopped;
            }

            StopStreamEffects(clear: true);
            StopImpactEffects(clear: true);
            _streamActive = false;
            _impactActive = false;
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private void HandleSprayEvaluated(ExtinguishResult result)
        {
            // Start stream on the first evaluated frame after a stop.
            if (!_streamActive)
            {
                StartStreamEffects();
                _streamActive = true;
            }

            if (result.DidHitZone)
            {
                UpdateStreamLength(result.Distance);
                UpdateImpactEffects(result);
            }
            else
            {
                UpdateStreamLength(_missStreamDistance);

                if (_impactActive)
                {
                    // Hâlâ sıkıyorken isabet→ıska: hit noktası değişeceği için impact’i anında temizle.
                    StopImpactEffects(clear: true);
                    _impactActive = false;
                }
            }
        }

        private void HandleSprayStopped()
        {
            // Bırakınca: yeni parça üretme durur; sahadakiler lifetime ile söner (ani clear yok).
            StopStreamEffects(clear: false);
            StopImpactEffects(clear: false);
            _streamActive = false;
            _impactActive = false;
        }

        // ── Stream start / stop ───────────────────────────────────────────────

        private void StartStreamEffects()
        {
            if (_streamParticleSystems == null) return;

            foreach (ParticleSystem ps in _streamParticleSystems)
            {
                if (ps == null) continue;
                // Her yeni sıkışta sıfırdan: önceki tick'te isPlaying takılı kalsa bile Play atlanmaz.
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }

        /// <param name="clear">
        /// True  → StopEmittingAndClear (Awake, OnDisable, hit→miss while spraying).
        /// False → StopEmitting only (spray release — existing particles fade out by lifetime).
        /// </param>
        private void StopStreamEffects(bool clear)
        {
            if (_streamParticleSystems == null) return;

            var behaviour = clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

            foreach (ParticleSystem ps in _streamParticleSystems)
            {
                if (ps == null) continue;
                ps.Stop(true, behaviour);
            }
        }

        // ── Stream length ─────────────────────────────────────────────────────

        private void UpdateStreamLength(float distance)
        {
            if (_streamMains == null) return;

            float safeDistance = Mathf.Max(0.01f, distance);

            for (int i = 0; i < _streamMains.Length; i++)
            {
                if (_streamParticleSystems[i] == null) continue;

                float speed = safeDistance / _streamLifetimes[i];
                _streamMains[i].startSpeed = speed;
            }
        }

        // ── Impact effects ────────────────────────────────────────────────────

        private void UpdateImpactEffects(ExtinguishResult result)
        {
            if (_impactParticleSystems == null) return;

            Vector3 worldHitPoint = ResolveImpactWorldPosition(result);

            bool wasInactive = !_impactActive;

            foreach (ParticleSystem ps in _impactParticleSystems)
            {
                if (ps == null) continue;

                // Yeni vuruş serisi / sıkış arası: önce temizle, sonra sıfırdan Play.
                if (wasInactive)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ps.transform.position = worldHitPoint;

                if (!ps.isPlaying)
                    ps.Play(true);
            }

            _impactActive = true;
        }

        /// <summary>
        /// Uses evaluator hit point (spherecast). If the struck zone has a collider, clamps to
        /// the closest point on that collider so impact VFX sit on the fire/intervention surface.
        /// </summary>
        static Vector3 ResolveImpactWorldPosition(in ExtinguishResult result)
        {
            Vector3 p = result.HitPoint;
            FireTargetZone zone = result.HitZone;
            if (zone == null || !zone.TryGetComponent<Collider>(out Collider col) || !col.enabled)
                return p;

            return col.ClosestPoint(p);
        }

        /// <param name="clear">True: hard clear (Awake, disable, hit→miss while spraying). False: stop emitting only (spray release).</param>
        private void StopImpactEffects(bool clear)
        {
            if (_impactParticleSystems == null) return;

            var behaviour = clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

            foreach (ParticleSystem ps in _impactParticleSystems)
            {
                if (ps == null) continue;
                ps.Stop(true, behaviour);
            }
        }
    }
}
