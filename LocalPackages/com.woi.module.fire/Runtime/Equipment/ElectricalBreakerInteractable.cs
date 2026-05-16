using System;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.Events;
using Woi.InputSystem;

namespace Woi.Equipment
{
    /// <summary>
    /// Uses the active gameplay Interact ScriptableObject event to turn off the electrical breaker.
    /// </summary>
    [AddComponentMenu("Woi/Equipment/Electrical Breaker Interactable")]
    public sealed class ElectricalBreakerInteractable : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Gameplay input context that exposes the existing Interact ScriptableObject event.")]
        [SerializeField] private GameplayInputContext _inputContext;

        [Tooltip("PC: kamera veya el ışın kökü. VR: sahnede etkin bir ExtinguisherHoverTransformRaycaster kayıtlıysa aynı kontrolcü ışını kullanılır; yoksa bu transform (ör. HMD) yedek olarak kalır.")]
        [SerializeField] private Transform _interactionRayOrigin;

        [SerializeField, Min(0f)] private float _interactionRange = 3f;
        [SerializeField] private LayerMask _interactionLayerMask = Physics.AllLayers;
        [SerializeField] private bool _debugInteraction;

        [Header("Safety")]
        [SerializeField] private ElectricalFireSafetyController _controller;

        [Header("Switch Visual")]
        [Tooltip("Visual switch object to rotate when the breaker is successfully turned off.")]
        [SerializeField] private Transform _switchCaseTransform;

        [SerializeField] private Vector3 _switchedOffLocalEulerAngles = new Vector3(-180f, 0f, 0f);

        [Header("Events")]
        [SerializeField] private UnityEvent _onInteracted = new UnityEvent();

        public UnityEvent OnInteracted => _onInteracted;

        private void OnEnable()
        {
            if (_inputContext == null)
            {
                Debug.LogWarning("[ElectricalFireSafety] Breaker interactable has no GameplayInputContext assigned.", this);
                return;
            }

            if (_inputContext.InteractEvent != null)
                _inputContext.InteractEvent.OnRaised += HandleInteractInput;

            if (_inputContext.EquipEvent != null && _inputContext.EquipEvent != _inputContext.InteractEvent)
                _inputContext.EquipEvent.OnRaised += HandleInteractInput;

            if (_debugInteraction)
            {
                Debug.Log(
                    $"[ElectricalFireSafety] Breaker subscribed. InteractEvent={_inputContext.InteractEvent != null}, EquipEvent={_inputContext.EquipEvent != null}.",
                    this);
            }

            if (_inputContext.InteractEvent == null && _inputContext.EquipEvent == null)
            {
                Debug.LogWarning("[ElectricalFireSafety] Breaker interactable has no Interact or Equip event on GameplayInputContext.", this);
            }
        }

        private void OnDisable()
        {
            if (_inputContext == null)
                return;

            if (_inputContext.InteractEvent != null)
                _inputContext.InteractEvent.OnRaised -= HandleInteractInput;

            if (_inputContext.EquipEvent != null && _inputContext.EquipEvent != _inputContext.InteractEvent)
                _inputContext.EquipEvent.OnRaised -= HandleInteractInput;
        }

        private void HandleInteractInput()
        {
            if (_debugInteraction)
                Debug.Log("[ElectricalFireSafety] Breaker received Interact input.", this);

            if (_controller == null)
            {
                Debug.LogWarning("[ElectricalFireSafety] Breaker interactable has no safety controller assigned.", this);
                return;
            }

            if (_controller.IsBreakerOff)
            {
                if (_debugInteraction)
                    Debug.Log("[ElectricalFireSafety] Breaker ignored because it is already off.", this);

                return;
            }

            if (!IsInteractionTarget())
                return;

            RotateSwitchCaseToOffPosition();
            _onInteracted?.Invoke();
            _controller.TurnOffBreaker();
        }

        public void RotateSwitchCaseToOffPosition()
        {
            if (_switchCaseTransform == null)
            {
                Debug.LogWarning("[ElectricalFireSafety] Breaker has no switch case transform assigned.", this);
                return;
            }

            _switchCaseTransform.localEulerAngles = _switchedOffLocalEulerAngles;
        }

        private bool IsInteractionTarget()
        {
            if (!InteractionRaySource.TryGetWorldRay(_interactionRayOrigin, out Vector3 rayOrigin, out Vector3 rayDir))
            {
                Debug.LogWarning("[ElectricalFireSafety] Breaker interactable has no interaction ray origin assigned.", this);
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                    rayOrigin,
                    rayDir,
                    _interactionRange,
                    _interactionLayerMask,
                    QueryTriggerInteraction.Collide);

            if (hits.Length == 0)
            {
                if (_debugInteraction)
                    Debug.Log("[ElectricalFireSafety] Breaker raycast hit nothing.", this);

                return false;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                ElectricalBreakerInteractable hitInteractable =
                    hit.collider != null ? hit.collider.GetComponentInParent<ElectricalBreakerInteractable>() : null;

                if (_debugInteraction)
                {
                    string hitName = hit.collider != null ? hit.collider.name : "null";
                    Debug.Log(
                        $"[ElectricalFireSafety] Breaker raycast hit '{hitName}' at {hit.distance:F2}m. Matches this breaker: {hitInteractable == this}.",
                        this);
                }

                if (hitInteractable == this)
                    return true;
            }

            return false;
        }
    }
}
