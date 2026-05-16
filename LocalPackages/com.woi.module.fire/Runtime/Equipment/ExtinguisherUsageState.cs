using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.Equipment
{
    /// <summary>
    /// Tracks whether an extinguisher has been used by pulling its safety pin.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Equipment/Extinguisher Usage State")]
    public sealed class ExtinguisherUsageState : MonoBehaviour
    {
        [SerializeField] private ExtinguisherController _controller;

        public bool IsPinPulled { get; private set; }
        public bool IsUsed => IsPinPulled;

        private void Awake()
        {
            if (_controller == null)
                _controller = GetComponentInChildren<ExtinguisherController>();
        }

        private void OnEnable()
        {
            if (_controller != null)
                _controller.OnPinPulled += MarkPinPulled;
        }

        private void OnDisable()
        {
            if (_controller != null)
                _controller.OnPinPulled -= MarkPinPulled;
        }

        public void MarkPinPulled()
        {
            IsPinPulled = true;
        }

        public void ResetUsageState()
        {
            IsPinPulled = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_controller == null)
                _controller = GetComponentInChildren<ExtinguisherController>();
        }
#endif
    }
}
