using System;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.Events;
using Woi.InputSystem;

namespace Woi.Equipment
{
    /// <summary>
    /// Uses the active gameplay Interact ScriptableObject event to press the electrical emergency button.
    /// </summary>
    [AddComponentMenu("Woi/Equipment/Electrical Emergency Button Interactable")]
    public sealed class ElectricalEmergencyButtonInteractable : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Gameplay input context that exposes the existing Interact ScriptableObject event.")]
        [SerializeField] private GameplayInputContext _inputContext;

        [Tooltip("PC: kamera veya el ışın kökü. VR: etkin ExtinguisherHoverTransformRaycaster ile aynı nişan; yoksa bu transform yedek.")]
        [SerializeField] private Transform _interactionRayOrigin;

        [Tooltip("Optional collider/root object accepted as this button target. Use this if the hit collider is not under this component.")]
        [SerializeField] private Transform _buttonHitRoot;

        [SerializeField, Min(0f)] private float _interactionRange = 3f;
        [SerializeField] private LayerMask _interactionLayerMask = Physics.AllLayers;
        [SerializeField] private bool _debugInteraction;

        [Header("Safety")]
        [SerializeField] private ElectricalFireSafetyController _controller;

        [Header("Button Visual")]
        [Tooltip("Visual button object to move when pressed.")]
        [SerializeField] private Transform _buttonVisualTransform;

        [Tooltip("Local direction the button moves when pressed.")]
        [SerializeField] private Vector3 _pressLocalDirection = Vector3.down;

        [SerializeField, Min(0f)] private float _pressDistance = 0.03f;
        [SerializeField, Min(0.01f)] private float _pressDuration = 0.08f;
        [SerializeField, Min(0f)] private float _returnDelay = 0.08f;
        [SerializeField] private bool _returnAfterPress = true;
        [SerializeField] private bool _disableInteractionDuringPress = true;

        [Header("Events")]
        [SerializeField] private UnityEvent _onInteracted = new UnityEvent();

        private Vector3 _buttonStartLocalPosition;
        private Coroutine _buttonPressRoutine;
        private bool _pressInProgress;

        public UnityEvent OnInteracted => _onInteracted;

        private void Awake()
        {
            if (_buttonVisualTransform != null)
                _buttonStartLocalPosition = _buttonVisualTransform.localPosition;
        }

        private void OnEnable()
        {
            if (_inputContext == null)
            {
                Debug.LogWarning("[ElectricalFireSafety] Emergency button interactable has no GameplayInputContext assigned.", this);
                return;
            }

            if (_inputContext.InteractEvent != null)
                _inputContext.InteractEvent.OnRaised += HandleInteractInput;

            if (_inputContext.EquipEvent != null && _inputContext.EquipEvent != _inputContext.InteractEvent)
                _inputContext.EquipEvent.OnRaised += HandleInteractInput;

            if (_debugInteraction)
            {
                Debug.Log(
                    $"[ElectricalFireSafety] Emergency button subscribed. InteractEvent={_inputContext.InteractEvent != null}, EquipEvent={_inputContext.EquipEvent != null}.",
                    this);
            }

            if (_inputContext.InteractEvent == null && _inputContext.EquipEvent == null)
            {
                Debug.LogWarning("[ElectricalFireSafety] Emergency button interactable has no Interact or Equip event on GameplayInputContext.", this);
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
                Debug.Log("[ElectricalFireSafety] Emergency button received Interact input.", this);

            if (_disableInteractionDuringPress && _pressInProgress)
            {
                if (_debugInteraction)
                    Debug.Log("[ElectricalFireSafety] Emergency button ignored because press animation is already running.", this);

                return;
            }

            if (_controller == null)
            {
                Debug.LogWarning("[ElectricalFireSafety] Emergency button interactable has no safety controller assigned.", this);
                return;
            }

            if (!IsInteractionTarget())
                return;

            _pressInProgress = true;
            PlayButtonPressVisual();

            if (_controller.AreAllMonitoredFiresAlreadyExtinguished())
                return;

            _onInteracted?.Invoke();

            _controller.PressEmergencyButton();

            if (_buttonPressRoutine == null)
                _pressInProgress = false;
        }

        public void PlayButtonPressVisual()
        {
            if (_buttonVisualTransform == null)
            {
                Debug.LogWarning("[ElectricalFireSafety] Emergency button has no button visual transform assigned.", this);
                _pressInProgress = false;
                return;
            }

            if (_buttonPressRoutine != null)
                StopCoroutine(_buttonPressRoutine);

            _buttonPressRoutine = StartCoroutine(AnimateButtonPress());
        }

        private System.Collections.IEnumerator AnimateButtonPress()
        {
            Vector3 pressDirection = _pressLocalDirection.sqrMagnitude > 0f
                ? _pressLocalDirection.normalized
                : Vector3.down;

            Vector3 pressedPosition = _buttonStartLocalPosition + pressDirection * _pressDistance;

            yield return MoveButton(_buttonVisualTransform.localPosition, pressedPosition, _pressDuration);

            if (_returnAfterPress)
            {
                if (_returnDelay > 0f)
                    yield return new WaitForSeconds(_returnDelay);

                yield return MoveButton(_buttonVisualTransform.localPosition, _buttonStartLocalPosition, _pressDuration);
            }

            _buttonPressRoutine = null;
            _pressInProgress = false;
        }

        private System.Collections.IEnumerator MoveButton(Vector3 from, Vector3 to, float duration)
        {
            if (duration <= 0f)
            {
                _buttonVisualTransform.localPosition = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _buttonVisualTransform.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            _buttonVisualTransform.localPosition = to;
        }

        private bool IsInteractionTarget()
        {
            if (!InteractionRaySource.TryGetWorldRay(_interactionRayOrigin, out Vector3 rayOrigin, out Vector3 rayDir))
            {
                Debug.LogWarning("[ElectricalFireSafety] Emergency button interactable has no interaction ray origin assigned.", this);
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
                    Debug.Log("[ElectricalFireSafety] Emergency button raycast hit nothing.", this);

                return false;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                ElectricalEmergencyButtonInteractable hitInteractable =
                    hit.collider != null ? hit.collider.GetComponentInParent<ElectricalEmergencyButtonInteractable>() : null;

                bool matchesButtonHitRoot = IsUnderButtonHitRoot(hit.collider);
                bool matchesThisButton = hitInteractable == this || matchesButtonHitRoot;

                if (_debugInteraction)
                {
                    string hitName = hit.collider != null ? hit.collider.name : "null";
                    string layerName = hit.collider != null ? LayerMask.LayerToName(hit.collider.gameObject.layer) : "None";
                    Debug.Log(
                        $"[ElectricalFireSafety] Emergency button raycast hit '{hitName}' on layer '{layerName}' at {hit.distance:F2}m. Matches this button: {matchesThisButton}.",
                        this);
                }

                if (matchesThisButton)
                    return true;
            }

            return false;
        }

        private bool IsUnderButtonHitRoot(Collider hitCollider)
        {
            if (_buttonHitRoot == null || hitCollider == null)
                return false;

            Transform hitTransform = hitCollider.transform;
            return hitTransform == _buttonHitRoot || hitTransform.IsChildOf(_buttonHitRoot);
        }
    }
}
