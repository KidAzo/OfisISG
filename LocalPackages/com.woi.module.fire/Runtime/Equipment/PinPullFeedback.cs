using System.Collections;
using FireExtinguisher.Core;
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
        const float PinOutlineWidth = 3f;

        [Header("Source")]
        [Tooltip("Optional gameplay controller. When assigned, feedback plays when its pin is successfully pulled.")]
        [SerializeField] ExtinguisherController _extinguisherController;

        [Header("Visuals")]
        [Tooltip("Quick Outline on Pim_low (auto-filled). Either Outline script copy works.")]
        [SerializeField] MonoBehaviour _outline;
        [SerializeField] Transform _pinTransform;
        [SerializeField] Vector3 _localPullOffset = new Vector3(0.06f, 0f, 0f);
        [SerializeField] float _pullMoveDuration = 0.12f;
        [SerializeField] float _scalePunchAmount = 1.08f;
        [SerializeField] float _scalePunchDuration = 0.08f;
        [SerializeField] float _shrinkDuration = 0.12f;
        [SerializeField] bool _allowReplay;

        [Header("Woi Audio")]
        [SerializeField] SoundDefinition _pinPullSound;
        [Tooltip("Optional. Uses ServiceLocator after AudioSystem.Start, then falls back to FindFirstObjectByType.")]
        [SerializeField] AudioSystem _audioSystem;

        Transform PinTransform => _pinTransform != null ? _pinTransform : transform;

        Vector3 _initialLocalPosition;
        Vector3 _initialScale;
        bool _played;
        Coroutine _feedbackRoutine;

        void Awake()
        {
            if (_pinTransform == null)
                _pinTransform = transform;

            _initialLocalPosition = PinTransform.localPosition;
            _initialScale = PinTransform.localScale;

            if (_extinguisherController == null)
                _extinguisherController = GetComponentInParent<ExtinguisherController>(true);

            QuickOutlineBehaviour.Ensure(ref _outline, gameObject);
            QuickOutlineBehaviour.Hide(_outline);
            BindPinPullEvent(true);
        }

        void Start() => ResolveAudioSystem();

        void OnEnable()
        {
            QuickOutlineBehaviour.Ensure(ref _outline, gameObject);
            QuickOutlineBehaviour.Hide(_outline);

            if (_extinguisherController == null)
                _extinguisherController = GetComponentInParent<ExtinguisherController>(true);

            BindPinPullEvent(true);
        }

        void OnDisable()
        {
            BindPinPullEvent(false);
            QuickOutlineBehaviour.Hide(_outline);
        }

        void BindPinPullEvent(bool bind)
        {
            if (_extinguisherController == null)
                return;

            if (bind)
                _extinguisherController.OnPinPulled += PlayFeedback;
            else
                _extinguisherController.OnPinPulled -= PlayFeedback;
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
            QuickOutlineBehaviour.Hide(_outline);
        }

        void PlaySound()
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

        void PlayVisualSequence()
        {
            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
                StopActiveTweens();
            }

            _feedbackRoutine = StartCoroutine(FeedbackRoutine());
        }

        IEnumerator FeedbackRoutine()
        {
            Transform target = PinTransform;
            Vector3 pulledPosition = _initialLocalPosition + _localPullOffset;
            Vector3 punchedScale = _initialScale * Mathf.Max(1f, _scalePunchAmount);

            target.localPosition = _initialLocalPosition;
            target.localScale = _initialScale;

            EnablePinPullOutline();

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
            QuickOutlineBehaviour.Hide(_outline);
            _feedbackRoutine = null;
        }

        void EnablePinPullOutline()
        {
            QuickOutlineBehaviour.Ensure(ref _outline, gameObject);

            if (_outline == null)
            {
                Debug.LogWarning("[PinPullFeedback] No Outline on Pim_low.", this);
                return;
            }

            ExtinguisherPickupItem pickup = GetComponentInParent<ExtinguisherPickupItem>();
            if (pickup != null)
                pickup.GetComponent<HoverOutline>()?.ResetHover();

            QuickOutlineBehaviour.Show(_outline, Color.yellow, PinOutlineWidth);
        }

        void StopActiveTweens() => Tween.StopAll(PinTransform);

        void ResolveAudioSystem()
        {
            if (_audioSystem != null)
                return;

            if (AudioSystem.TryGetFromServiceLocator(out var sys))
                _audioSystem = sys;

            if (_audioSystem == null)
                _audioSystem = FindFirstObjectByType<AudioSystem>();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            QuickOutlineBehaviour.Ensure(ref _outline, gameObject);

            if (_extinguisherController == null)
                _extinguisherController = GetComponentInParent<ExtinguisherController>(true);
        }
#endif
    }
}
