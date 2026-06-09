using System.Collections;
using FireExtinguisher.Core;
using FireExtinguisher.VR;
using PrimeTween;
using UnityEngine;
using Woi.Equipment;
using WoiUtils.AudioSystem;

namespace Woi.Game
{
    /// <summary>
    /// Presentation-only feedback for a successful fire extinguisher safety pin pull.
    /// </summary>
    [AddComponentMenu("Woi/Feedback/Pin Pull Feedback")]
    public sealed class PinPullFeedback : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Optional gameplay controller. When assigned, feedback plays when its pin is successfully pulled.")]
        [SerializeField] private ExtinguisherController _extinguisherController;

        [Header("Visuals")]
        [SerializeField] private Outline _outline;
        [SerializeField] private Transform _pinTransform;
        [SerializeField] private Vector3 _localPullOffset = new Vector3(0.06f, 0f, 0f);
        [SerializeField] private float _pullMoveDuration = 0.12f;
        [SerializeField] private float _scalePunchAmount = 1.08f;
        [SerializeField] private float _scalePunchDuration = 0.08f;
        [SerializeField] private float _shrinkDuration = 0.12f;
        [SerializeField] private bool _allowReplay;

        [Header("Woi Audio")]
        [SerializeField] private SoundDefinition _pinPullSound;
        [Tooltip("Optional. Uses ServiceLocator after AudioSystem.Start, then falls back to FindFirstObjectByType.")]
        [SerializeField] private AudioSystem _audioSystem;

        private Transform PinTransform => _pinTransform != null ? _pinTransform : transform;

        private Vector3 _initialLocalPosition;
        private Vector3 _initialScale;
        private bool _played;
        private Coroutine _feedbackRoutine;

        private void Awake()
        {
            if (_pinTransform == null)
                _pinTransform = transform;

            _initialLocalPosition = PinTransform.localPosition;
            _initialScale = PinTransform.localScale;
            DisableOutline();
        }

        private void Start()
        {
            ResolveAudioSystem();
        }

        private void ResolveAudioSystem()
        {
            if (_audioSystem != null)
                return;

            if (AudioSystem.TryGetFromServiceLocator(out var sys))
                _audioSystem = sys;

            if (_audioSystem == null)
                _audioSystem = FindFirstObjectByType<AudioSystem>();
        }

        private void OnEnable()
        {
            DisableOutline();

            if (_extinguisherController != null)
                _extinguisherController.OnPinPulled += PlayFeedback;
        }

        private void OnDisable()
        {
            if (_extinguisherController != null)
                _extinguisherController.OnPinPulled -= PlayFeedback;

            DisableOutline();
        }

        public void PlayFeedback()
        {
            if (_played && !_allowReplay)
                return;

            _played = true;

            PlaySound();
            PlayVisualSequence();
        }

        public void ResetFeedback()
        {
            _played = false;

            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
                _feedbackRoutine = null;
            }

            StopActiveTweens();
            PinTransform.localPosition = _initialLocalPosition;
            PinTransform.localScale = _initialScale;
            DisableOutline();
        }

        private void PlaySound()
        {
            if (_pinPullSound == null)
            {
                Debug.LogWarning("[PinPullFeedback] No pin pull sound assigned.", this);
                return;
            }

            ResolveAudioSystem();

            if (_audioSystem == null)
            {
                Debug.LogWarning("[PinPullFeedback] No Woi AudioSystem found in the scene.", this);
                return;
            }

            _audioSystem.PlayFollow(_pinPullSound, PinTransform);
        }

        private void PlayVisualSequence()
        {
            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
                StopActiveTweens();
            }

            _feedbackRoutine = StartCoroutine(FeedbackRoutine());
        }

        private IEnumerator FeedbackRoutine()
        {
            Transform target = PinTransform;
            Vector3 pulledPosition = _initialLocalPosition + _localPullOffset;
            Vector3 punchedScale = _initialScale * Mathf.Max(1f, _scalePunchAmount);

            target.localPosition = _initialLocalPosition;
            target.localScale = _initialScale;

            if (!ShouldSuppressTubeOutline())
            {
                EnableYellowOutline();
            }

            float moveDuration = Mathf.Max(0.01f, _pullMoveDuration);
            Tween.LocalPosition(target, pulledPosition, moveDuration, Ease.OutQuad);
            yield return new WaitForSeconds(moveDuration);

            float punchDuration = Mathf.Max(0.01f, _scalePunchDuration);
            Tween.Scale(target, punchedScale, punchDuration, Ease.OutBack);
            yield return new WaitForSeconds(punchDuration);

            float shrinkDuration = Mathf.Max(0.01f, _shrinkDuration);
            Tween.Scale(target, Vector3.zero, shrinkDuration, Ease.OutQuad);
            yield return new WaitForSeconds(shrinkDuration);

            target.localPosition = pulledPosition;
            target.localScale = Vector3.zero;
            DisableOutline();
            _feedbackRoutine = null;
        }

        private static bool ShouldSuppressTubeOutline()
        {
            if (VRHandExtinguisherGrabber.GlobalHeldExtinguisherCount > 0)
            {
                return true;
            }

            PlayerExtinguisherEquipment equipment =
                Object.FindFirstObjectByType<PlayerExtinguisherEquipment>(FindObjectsInactive.Exclude);
            return equipment != null && equipment.CurrentItem != null;
        }

        private void EnableYellowOutline()
        {
            if (ShouldSuppressTubeOutline())
            {
                return;
            }

            ExtinguisherPickupItem pickup = GetComponentInParent<ExtinguisherPickupItem>();
            if (pickup != null && pickup.IsEquipped)
            {
                return;
            }

            if (_outline == null)
            {
                Debug.LogWarning("[PinPullFeedback] No Quick Outline component assigned.", this);
                return;
            }

            _outline.OutlineColor = Color.yellow;
            _outline.enabled = true;
        }

        private void StopActiveTweens()
        {
            Tween.StopAll(PinTransform);
        }

        private void DisableOutline()
        {
            if (_outline != null)
                _outline.enabled = false;
        }
    }
}
