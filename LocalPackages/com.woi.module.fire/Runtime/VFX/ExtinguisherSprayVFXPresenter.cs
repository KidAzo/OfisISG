using Obvious.Soap;
using UnityEngine;

namespace Woi.Game.VFX
{
    /// <summary>
    /// Presentation-layer component that lives on the extinguisher GameObject (or a child).
    /// Subscribes to SO event channels and drives a ParticleSystem from the nozzle transform.
    /// Spray start clears and plays; spray stop uses stop-emission so particles finish smoothly.
    /// Contains zero gameplay logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExtinguisherSprayVFXPresenter : MonoBehaviour
    {
        [Header("VFX")]
        [SerializeField] private ParticleSystem _sprayParticleSystem;

        [Tooltip("VR modunda Nozzle_low altına taşınacak olan tüm VFX'lerin ana objesi (Örn: NewFireEx).")]
        [SerializeField] private Transform _vfxRoot;

        [Header("Nozzle")]
        [Tooltip("The nozzle/spawn point transform. The particle system position and rotation are matched here on play.")]
        [SerializeField] private Transform _nozzleTransform;

        [Header("SO Event Channels")]
        [SerializeField] private ScriptableEventNoParam _onSprayStartedChannel;
        [SerializeField] private ScriptableEventNoParam _onSprayStoppedChannel;

        // Orijinal durumu saklamak için
        private Transform _originalNozzle;
        private Transform _originalParticleParent;
        private Transform _activeVfxRoot;

        private void Awake()
        {
            _originalNozzle = _nozzleTransform;
            
            // Eğer inspector'da bir root atanmadıysa ve ParticleSystem varsa, 
            // ParticleSystem'in parent'ını (NewFireEx) root olarak kabul ederiz.
            if (_vfxRoot != null)
                _activeVfxRoot = _vfxRoot;
            else if (_sprayParticleSystem != null)
                _activeVfxRoot = _sprayParticleSystem.transform.parent;

            if (_activeVfxRoot != null)
            {
                _originalParticleParent = _activeVfxRoot.parent;
            }
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (!ValidateReferences()) return;

            _onSprayStartedChannel.OnRaised += HandleSprayStarted;
            _onSprayStoppedChannel.OnRaised += HandleSprayStopped;
        }

        private void OnDisable()
        {
            if (_onSprayStartedChannel != null) _onSprayStartedChannel.OnRaised -= HandleSprayStarted;
            if (_onSprayStoppedChannel != null) _onSprayStoppedChannel.OnRaised -= HandleSprayStopped;

            ForceStop();
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private void HandleSprayStarted()
        {
            if (_sprayParticleSystem == null) return;

            AlignToNozzle();
            // Her basışta sıfırdan: önceki emisyon / yarım kalmış parçacık kalmamalı.
            _sprayParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _sprayParticleSystem.Play(true);
        }

        private void HandleSprayStopped()
        {
            if (_sprayParticleSystem == null) return;

            _sprayParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ── VR Support ────────────────────────────────────────────────────────

        public void SetVRNozzle(Transform vrNozzle)
        {
            if (vrNozzle == null) return;
            
            _nozzleTransform = vrNozzle;
            if (_activeVfxRoot != null)
            {
                _activeVfxRoot.SetParent(vrNozzle, false);
                _activeVfxRoot.localPosition = Vector3.zero;
                _activeVfxRoot.localRotation = Quaternion.identity;
            }
        }

        public void RestoreOriginalNozzle()
        {
            _nozzleTransform = _originalNozzle;
            if (_activeVfxRoot != null && _originalParticleParent != null)
            {
                _activeVfxRoot.SetParent(_originalParticleParent, false);
                AlignToNozzle();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void AlignToNozzle()
        {
            if (_nozzleTransform == null || _activeVfxRoot == null) return;

            _activeVfxRoot.SetPositionAndRotation(
                _nozzleTransform.position,
                _nozzleTransform.rotation);
        }

        private void ForceStop()
        {
            if (_sprayParticleSystem == null) return;
            if (!_sprayParticleSystem.isPlaying && !_sprayParticleSystem.isPaused) return;

            _sprayParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private bool ValidateReferences()
        {
            if (_sprayParticleSystem == null)
            {
                Debug.LogWarning($"[{nameof(ExtinguisherSprayVFXPresenter)}] ParticleSystem not assigned on {gameObject.name}.", this);
                return false;
            }

            if (_onSprayStartedChannel == null || _onSprayStoppedChannel == null)
            {
                Debug.LogWarning($"[{nameof(ExtinguisherSprayVFXPresenter)}] One or more SO event channels not assigned on {gameObject.name}.", this);
                return false;
            }

            return true;
        }
    }
}
