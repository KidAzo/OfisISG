using System;
using System.Collections;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.Events;
using Woi.InputSystem;

namespace Woi.Equipment
{
    /// <summary>
    /// Opens a Class C pipe valve via Interact input (raycast), with an animated handle rotation.
    /// Wire <see cref="ClassCFireValveController"/> and <see cref="FireExtinguishPrerequisiteGate"/> (ValveOpenOnly) on the fire.
    /// </summary>
    [AddComponentMenu("Woi/Equipment/Pipe Valve Interactable (Class C)")]
    public sealed class PipeValveInteractable : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Gameplay input context that exposes the existing Interact ScriptableObject event.")]
        [SerializeField] private GameplayInputContext _inputContext;

        [Tooltip("PC: kamera veya el ışın kökü. VR: etkin ExtinguisherHoverTransformRaycaster ile aynı nişan; yoksa bu transform yedek.")]
        [SerializeField] private Transform _interactionRayOrigin;

        [SerializeField, Min(0f)] private float _interactionRange = 3f;
        [SerializeField] private LayerMask _interactionLayerMask = Physics.AllLayers;
        [SerializeField] private bool _debugInteraction;

        [Header("Class C safety")]
        [SerializeField] private ClassCFireValveController _valveController;

        [Header("Valve visual")]
        [Tooltip("Transform that rotates when the valve opens (e.g. red wheel).")]
        [SerializeField] private Transform _valveHandleTransform;

        [Tooltip("Local rotation when the valve is fully open.")]
        [SerializeField] private Vector3 _openLocalEulerAngles = new Vector3(0f, 90f, 0f);

        [SerializeField, Min(0.01f)] private float _rotateDuration = 0.65f;

        [Header("Events")]
        [SerializeField] private UnityEvent _onInteractStarted = new UnityEvent();

        public UnityEvent OnInteractStarted => _onInteractStarted;

        private Coroutine _rotateRoutine;
        private bool _animationInProgress;

        private void OnEnable()
        {
            if (_inputContext == null)
            {
                Debug.LogWarning("[ClassCFireValve] Pipe valve interactable has no GameplayInputContext assigned.", this);
                return;
            }

            if (_inputContext.InteractEvent != null)
                _inputContext.InteractEvent.OnRaised += HandleInteractInput;

            if (_inputContext.EquipEvent != null && _inputContext.EquipEvent != _inputContext.InteractEvent)
                _inputContext.EquipEvent.OnRaised += HandleInteractInput;

            if (_debugInteraction)
            {
                Debug.Log(
                    $"[ClassCFireValve] Valve subscribed. InteractEvent={_inputContext.InteractEvent != null}, EquipEvent={_inputContext.EquipEvent != null}.",
                    this);
            }

            if (_inputContext.InteractEvent == null && _inputContext.EquipEvent == null)
            {
                Debug.LogWarning("[ClassCFireValve] Pipe valve interactable has no Interact or Equip event on GameplayInputContext.", this);
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
                Debug.Log("[ClassCFireValve] Valve received Interact input.", this);

            if (_valveController != null && _valveController.IsValveOpen)
            {
                if (_debugInteraction)
                    Debug.Log("[ClassCFireValve] Valve ignored because it is already open.", this);

                return;
            }

            if (_animationInProgress)
            {
                if (_debugInteraction)
                    Debug.Log("[ClassCFireValve] Valve ignored because rotation is in progress.", this);

                return;
            }

            if (_valveController == null)
            {
                Debug.LogWarning("[ClassCFireValve] Pipe valve interactable has no ClassCFireValveController assigned.", this);
                return;
            }

            if (!IsInteractionTarget())
                return;

            _onInteractStarted?.Invoke();

            if (_valveHandleTransform == null)
            {
                Debug.LogWarning("[ClassCFireValve] Pipe valve has no handle transform — opening immediately.", this);
                _valveController.MarkValveOpen();
                return;
            }

            if (_rotateRoutine != null)
                StopCoroutine(_rotateRoutine);

            _rotateRoutine = StartCoroutine(AnimateOpenValve());
        }

        private IEnumerator AnimateOpenValve()
        {
            _animationInProgress = true;
            Transform t = _valveHandleTransform;
            Quaternion from = t.localRotation;
            Quaternion to = Quaternion.Euler(_openLocalEulerAngles);
            float duration = _rotateDuration;

            if (duration <= 0f)
            {
                t.localRotation = to;
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float u = Mathf.Clamp01(elapsed / duration);
                    float smooth = Mathf.SmoothStep(0f, 1f, u);
                    t.localRotation = Quaternion.Slerp(from, to, smooth);
                    yield return null;
                }

                t.localRotation = to;
            }

            _valveController.MarkValveOpen();
            _animationInProgress = false;
            _rotateRoutine = null;
        }

        private bool IsInteractionTarget()
        {
            if (!InteractionRaySource.TryGetWorldRay(_interactionRayOrigin, out Vector3 rayOrigin, out Vector3 rayDir))
            {
                Debug.LogWarning("[ClassCFireValve] Pipe valve interactable has no interaction ray origin assigned.", this);
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
                    Debug.Log("[ClassCFireValve] Valve raycast hit nothing.", this);

                return false;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                PipeValveInteractable hitValve =
                    hit.collider != null ? hit.collider.GetComponentInParent<PipeValveInteractable>() : null;

                if (_debugInteraction)
                {
                    string hitName = hit.collider != null ? hit.collider.name : "null";
                    Debug.Log(
                        $"[ClassCFireValve] Valve raycast hit '{hitName}' at {hit.distance:F2}m. Matches this valve: {hitValve == this}.",
                        this);
                }

                if (hitValve == this)
                    return true;
            }

            return false;
        }
    }
}
