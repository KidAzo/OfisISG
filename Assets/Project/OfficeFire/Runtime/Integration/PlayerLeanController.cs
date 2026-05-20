using System;
using Obvious.Soap;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Applies downward lean on the camera child transform (hold Ctrl = peek down).
    /// Uses the camera under the player, not the look pivot, so look rotation is not overwritten each frame.
    /// </summary>
    [AddComponentMenu("Woi/Office Fire/Player Lean Controller")]
    [DefaultExecutionOrder(200)]
    public sealed class PlayerLeanController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField]
        private ScriptableEventFloat leanInputEvent;

        [Header("Camera")]
        [Tooltip("Usually PlayerCamera (child of CameraPivot). Leave empty to auto-find.")]
        [SerializeField]
        private Transform leanCameraTransform;

        [Tooltip("Local Y offset applied at full lean.")]
        [SerializeField]
        private float maxDownOffset = 0.45f;

        [Tooltip("Extra pitch down at full lean (degrees).")]
        [SerializeField]
        private float maxPitchDegrees = 18f;

        [SerializeField]
        private float leanBlendSpeed = 10f;

        [Header("Body (optional)")]
        [SerializeField]
        private Animator bodyAnimator;

        [SerializeField]
        private string leanStateName = "Leaning";

        [SerializeField]
        private int leanLayer;

        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private float _targetLean;
        private float _currentLean;
        private bool _bodyLeanActive;
        private bool _wasLeaning;
        private int _leanStateHash;

        public event Action LeanStarted;
        public event Action LeanEnded;

        public float CurrentLean => _currentLean;

        private void Awake()
        {
            ResolveLeanCameraTransform();
            CacheBasePose();
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
            _wasLeaning = false;
            ApplyCameraLean(0f);
            SetBodyLeanActive(false);
        }

        private void LateUpdate()
        {
            _currentLean = Mathf.MoveTowards(_currentLean, _targetLean, leanBlendSpeed * Time.deltaTime);
            ApplyCameraLean(_currentLean);
            NotifyLeanLifecycle();

            bool wantsBodyLean = _targetLean > 0.05f;
            if (wantsBodyLean)
            {
                SetBodyLeanActive(true);
            }
            else if (_currentLean < 0.02f)
            {
                SetBodyLeanActive(false);
            }
        }

        private void OnLeanInput(float leanAxis)
        {
            _targetLean = leanAxis > 0.01f ? 1f : 0f;
        }

        private void NotifyLeanLifecycle()
        {
            bool isLeaning = _targetLean > 0.05f || _currentLean > 0.05f;
            if (isLeaning && !_wasLeaning)
            {
                LeanStarted?.Invoke();
            }
            else if (!isLeaning && _wasLeaning)
            {
                LeanEnded?.Invoke();
            }

            _wasLeaning = isLeaning;
        }

        private void ApplyCameraLean(float lean)
        {
            if (leanCameraTransform == null)
            {
                return;
            }

            float t = Mathf.Clamp01(lean);
            leanCameraTransform.localPosition = _baseLocalPosition + Vector3.down * (t * maxDownOffset);
            leanCameraTransform.localRotation = _baseLocalRotation * Quaternion.Euler(t * maxPitchDegrees, 0f, 0f);
        }

        private void ResolveLeanCameraTransform()
        {
            if (leanCameraTransform != null && leanCameraTransform.GetComponent<Camera>() == null)
            {
                Camera nestedCamera = leanCameraTransform.GetComponentInChildren<Camera>(true);
                if (nestedCamera != null)
                {
                    leanCameraTransform = nestedCamera.transform;
                }
            }

            if (leanCameraTransform != null)
            {
                return;
            }

            Camera camera = GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                leanCameraTransform = camera.transform;
            }
        }

        private void CacheBasePose()
        {
            if (leanCameraTransform == null)
            {
                return;
            }

            _baseLocalPosition = leanCameraTransform.localPosition;
            _baseLocalRotation = leanCameraTransform.localRotation;
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

        public void SetLeanCameraTransform(Transform cameraTransform)
        {
            leanCameraTransform = cameraTransform;
            CacheBasePose();
        }

        public void SetBodyAnimator(Animator animator)
        {
            bodyAnimator = animator;
            _bodyLeanActive = false;
        }
    }
}
