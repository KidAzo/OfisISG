using UnityEngine;
using UnityEngine.Events;

namespace FireExtinguisher.Core
{
    /// <summary>
    /// Tracks the Class C (flammable liquid / gas) pipe valve prerequisite:
    /// extinguishing stays blocked until <see cref="MarkValveOpen"/> runs
    /// (typically after the valve rotation animation completes).
    /// </summary>
    [AddComponentMenu("Fire Extinguisher/Safety/Class C Fire Valve Controller")]
    public sealed class ClassCFireValveController : MonoBehaviour
    {
        private const string LogPrefix = "[ClassCFireValve]";

        [Header("Events")]
        [Tooltip("Raised once when the valve reaches the fully open state.")]
        [SerializeField] private UnityEvent _onValveOpened = new UnityEvent();

        public bool IsValveOpen { get; private set; }

        public UnityEvent OnValveOpened => _onValveOpened;

        /// <summary>
        /// Idempotent — call from the valve interactable when the open animation finishes.
        /// </summary>
        public void MarkValveOpen()
        {
            if (IsValveOpen)
            {
                Debug.Log($"{LogPrefix} Valve already open.", this);
                return;
            }

            IsValveOpen = true;
            Debug.Log($"{LogPrefix} Valve open — Class C extinguishing allowed.", this);
            _onValveOpened?.Invoke();
        }

        /// <summary>
        /// Resets prerequisite state (e.g. scenario restart). Does not move visuals.
        /// </summary>
        public void ResetValveState()
        {
            IsValveOpen = false;
        }
    }
}
