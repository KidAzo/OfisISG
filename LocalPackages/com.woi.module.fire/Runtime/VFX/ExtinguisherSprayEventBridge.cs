using Obvious.Soap;
using UnityEngine;
using FireExtinguisher.Core;

namespace Woi.Game.VFX
{
    /// <summary>
    /// Thin adapter that lives on the extinguisher GameObject.
    /// Translates ExtinguisherController C# events → ScriptableObject event channels.
    /// Contains zero VFX or gameplay logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExtinguisherSprayEventBridge : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private ExtinguisherController _controller;

        [Header("SO Event Channels")]
        [SerializeField] private ScriptableEventNoParam _onSprayStartedChannel;
        [SerializeField] private ScriptableEventNoParam _onSprayStoppedChannel;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_controller == null)
            {
                Debug.LogWarning($"[{nameof(ExtinguisherSprayEventBridge)}] ExtinguisherController not assigned on {gameObject.name}.", this);
                return;
            }

            _controller.OnSprayStarted      += HandleSprayStarted;
            _controller.OnSprayStopped      += HandleSprayStopped;
            _controller.OnExtinguisherDepleted += HandleDepleted;
        }

        private void OnDisable()
        {
            if (_controller == null) return;

            _controller.OnSprayStarted      -= HandleSprayStarted;
            _controller.OnSprayStopped      -= HandleSprayStopped;
            _controller.OnExtinguisherDepleted -= HandleDepleted;
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private void HandleSprayStarted()    => _onSprayStartedChannel?.Raise();
        private void HandleSprayStopped()    => _onSprayStoppedChannel?.Raise();
        private void HandleDepleted()        => _onSprayStoppedChannel?.Raise();
    }
}
