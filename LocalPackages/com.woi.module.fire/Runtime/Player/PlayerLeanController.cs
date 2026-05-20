using Obvious.Soap;
using UnityEngine;

namespace Woi.Player
{
    /// <summary>
    /// Applies lateral camera lean from gameplay input (-1 left, +1 right).
    /// Optionally drives a humanoid <see cref="Animator"/> lean state for a visible body mesh.
    /// </summary>
    public sealed class PlayerLeanController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField]
        private ScriptableEventFloat leanInputEvent;

        [Header("Camera")]
        [SerializeField]
        private Transform cameraPivot;

        [SerializeField]
        private float maxLateralOffset = 0.35f;

        [SerializeField]
        private float maxRollDegrees = 8f;

        [SerializeField]
        private float leanBlendSpeed = 10f;

        [Header("Body (optional)")]
        [SerializeField]
        private Animator bodyAnimator;

        [SerializeField]
        private string leanStateName = "Leaning";

        [SerializeField]
        private int leanLayer;

        private Vector3 _cameraPivotBaseLocalPosition;
        private Quaternion _cameraPivotBaseLocalRotation;
        private float _targetLean;
        private float _currentLean;
        private bool _bodyLeanActive;
        private int _leanStateHash;

        private void Awake()
        {
            if (cameraPivot != null)
            {
                _cameraPivotBaseLocalPosition = cameraPivot.localPosition;
                _cameraPivotBaseLocalRotation = cameraPivot.localRotation;
            }

            _leanStateHash = Animator.StringToHash(leanStateName);
        }

        private void OnEnable()
        {
            if (leanInputEvent != null)
            {
                leanInputEvent.OnRaised += OnLeanInput;
            }
        }

        private void OnDisable()
        {
            if (leanInputEvent != null)
            {
                leanInputEvent.OnRaised -= OnLeanInput;
            }

            _targetLean = 0f;
            _currentLean = 0f;
            ApplyCameraLean(0f);
            SetBodyLeanActive(false);
        }

        private void Update()
        {
            _currentLean = Mathf.MoveTowards(_currentLean, _targetLean, leanBlendSpeed * Time.deltaTime);
            ApplyCameraLean(_currentLean);

            bool wantsBodyLean = Mathf.Abs(_targetLean) > 0.05f;
            if (wantsBodyLean)
            {
                SetBodyLeanActive(true);
            }
            else if (Mathf.Abs(_currentLean) < 0.02f)
            {
                SetBodyLeanActive(false);
            }
        }

        private void OnLeanInput(float leanAxis)
        {
            _targetLean = Mathf.Clamp(leanAxis, -1f, 1f);
        }

        private void ApplyCameraLean(float lean)
        {
            if (cameraPivot == null)
            {
                return;
            }

            cameraPivot.localPosition = _cameraPivotBaseLocalPosition + Vector3.right * (lean * maxLateralOffset);
            cameraPivot.localRotation = _cameraPivotBaseLocalRotation * Quaternion.Euler(0f, 0f, -lean * maxRollDegrees);
        }

        private void SetBodyLeanActive(bool active)
        {
            if (bodyAnimator == null || _bodyLeanActive == active)
            {
                return;
            }

            _bodyLeanActive = active;

            if (active)
            {
                bodyAnimator.enabled = true;
                bodyAnimator.Play(_leanStateHash, leanLayer, 0f);
            }
            else
            {
                bodyAnimator.enabled = false;
            }
        }

        public void SetCameraPivot(Transform pivot)
        {
            cameraPivot = pivot;
            if (cameraPivot != null)
            {
                _cameraPivotBaseLocalPosition = cameraPivot.localPosition;
                _cameraPivotBaseLocalRotation = cameraPivot.localRotation;
            }
        }

        public void SetBodyAnimator(Animator animator)
        {
            bodyAnimator = animator;
            _bodyLeanActive = false;
        }
    }
}
