using FireExtinguisher.Core;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Events;

namespace Woi.Training
{
    /// <summary>
    /// Bu <see cref="GameObject"/> üzerindeki <see cref="FireSource"/> tamamen söndüğünde
    /// (<see cref="FireSource.OnFullyExtinguished"/>) atanmış SOAP eventini bir kez Raise eder.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Training/Fire Source Extinguished Soap Bridge")]
    public sealed class FireSourceExtinguishedSoapBridge : MonoBehaviour
    {
        [Tooltip("Boşsa aynı GameObject'ten alınır.")]
        [SerializeField]
        private FireSource _fireSource;

        [SerializeField]
        private ScriptableEventNoParam _onFireFullyExtinguished;

        [SerializeField] private UnityEvent _onFireFullyExtinguishedUnityEvent;

        private void OnEnable()
        {
            if (_fireSource == null)
                _fireSource = GetComponent<FireSource>();

            if (_fireSource == null)
            {
                Debug.LogWarning(
                    $"[{nameof(FireSourceExtinguishedSoapBridge)}] No {nameof(FireSource)} on '{name}'.",
                    this);
                return;
            }

            _fireSource.OnFullyExtinguished += HandleFullyExtinguished;
            _fireSource.OnFullyExtinguished += _onFireFullyExtinguishedUnityEvent.Invoke;
        }

        private void OnDisable()
        {
            if (_fireSource != null)
                _fireSource.OnFullyExtinguished -= HandleFullyExtinguished;

            _fireSource.OnFullyExtinguished -= _onFireFullyExtinguishedUnityEvent.Invoke;
        }

        private void HandleFullyExtinguished()
        {
            TrainingVrTransientSoapFireContext.LastFullyExtinguishedFire = _fireSource;
            _onFireFullyExtinguished?.Raise();
        }
    }
}
