using Obvious.Soap;
using UnityEngine;

namespace Woi.Game.VFX
{
    /// <summary>
    /// Subscribes to SO spray events and drives a ParticleSystem.
    /// Does not move any transform — VFX root (e.g. NewFireEx) stays at prefab hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExtinguisherSprayVFXPresenter : MonoBehaviour
    {
        [Header("VFX")]
        [SerializeField] private ParticleSystem _sprayParticleSystem;

        [Header("SO Event Channels")]
        [SerializeField] private ScriptableEventNoParam _onSprayStartedChannel;
        [SerializeField] private ScriptableEventNoParam _onSprayStoppedChannel;

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

        private void HandleSprayStarted()
        {
            if (_sprayParticleSystem == null) return;

            _sprayParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _sprayParticleSystem.Play(true);
        }

        private void HandleSprayStopped()
        {
            if (_sprayParticleSystem == null) return;

            _sprayParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
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
