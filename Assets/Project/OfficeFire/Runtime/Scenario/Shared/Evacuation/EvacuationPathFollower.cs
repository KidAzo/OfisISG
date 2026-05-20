using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Moves a humanoid along an <see cref="EvacuationPath"/> spline. Started/stopped by
    /// <see cref="EvacuationNpcDirector"/> during assembly-area evacuation.
    /// </summary>
    public sealed class EvacuationPathFollower : MonoBehaviour
    {
        private enum EndBehaviour
        {
            StopAtEnd = 0,
            Loop = 1,
            DisableGameObject = 2,
        }

        [Header("Path")]
        [SerializeField]
        private EvacuationPath path;

        [SerializeField]
        [Min(0f)]
        private float moveSpeed = -1f;

        [SerializeField]
        [Min(0f)]
        private float startDelay;

        [SerializeField]
        [Range(0f, 1f)]
        private float startNormalizedT;

        [Header("Animation")]
        [SerializeField]
        private Animator animator;

        [SerializeField]
        private string locomotionStateName = "Walking";

        [SerializeField]
        [Min(0.1f)]
        private float animatorSpeed = 1.15f;

        [Header("Behaviour")]
        [SerializeField]
        private EndBehaviour endBehaviour = EndBehaviour.StopAtEnd;

        [SerializeField]
        private bool faceMovementDirection = true;

        [SerializeField]
        private bool keepUpright = true;

        [SerializeField]
        private bool playOnStartForTesting;

        private Vector3 _resetPosition;
        private Quaternion _resetRotation;
        private float _normalizedTime;
        private float _delayRemaining;
        private bool _isRunning;
        private bool _hasStoredResetPose;
        private int _locomotionStateHash;

        public EvacuationPath Path => path;

        public bool IsRunning => _isRunning;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            _locomotionStateHash = Animator.StringToHash(locomotionStateName);
            StoreResetPose();
        }

        private void Start()
        {
            if (playOnStartForTesting)
            {
                Begin();
            }
        }

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            if (_delayRemaining > 0f)
            {
                _delayRemaining -= Time.deltaTime;
                return;
            }

            if (path == null)
            {
                StopEvacuation(resetPose: false);
                return;
            }

            float speed = moveSpeed > 0f ? moveSpeed : path.DefaultMoveSpeed;
            float length = path.GetLength();
            if (length <= 0.001f || speed <= 0.001f)
            {
                return;
            }

            _normalizedTime += (speed / length) * Time.deltaTime;

            if (_normalizedTime >= 1f)
            {
                HandlePathEnd();
                return;
            }

            ApplyPose(_normalizedTime);
        }

        public void Begin()
        {
            if (path == null)
            {
                Debug.LogWarning("[EvacuationPathFollower] Path is not assigned.", this);
                return;
            }

            if (!_hasStoredResetPose)
            {
                StoreResetPose();
            }

            _normalizedTime = Mathf.Clamp01(startNormalizedT);
            _delayRemaining = startDelay;
            _isRunning = true;

            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            PlayLocomotion();
            ApplyPose(_normalizedTime);
        }

        public void StopEvacuation(bool resetPose = true)
        {
            _isRunning = false;
            _delayRemaining = 0f;

            if (resetPose && _hasStoredResetPose)
            {
                transform.SetPositionAndRotation(_resetPosition, _resetRotation);
            }
        }

        public void SetPath(EvacuationPath evacuationPath)
        {
            path = evacuationPath;
        }

        private void HandlePathEnd()
        {
            _normalizedTime = 1f;
            ApplyPose(1f);

            switch (endBehaviour)
            {
                case EndBehaviour.Loop:
                    _normalizedTime = 0f;
                    break;
                case EndBehaviour.DisableGameObject:
                    _isRunning = false;
                    gameObject.SetActive(false);
                    break;
                default:
                    _isRunning = false;
                    break;
            }
        }

        private void ApplyPose(float normalizedTime)
        {
            if (!path.TrySample(normalizedTime, out Vector3 position, out Vector3 tangent))
            {
                return;
            }

            transform.position = position;

            if (!faceMovementDirection)
            {
                return;
            }

            Vector3 forward = tangent;
            if (keepUpright)
            {
                forward.y = 0f;
                if (forward.sqrMagnitude < 1e-6f)
                {
                    forward = transform.forward;
                    forward.y = 0f;
                }
            }

            if (forward.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }
        }

        private void PlayLocomotion()
        {
            if (animator == null)
            {
                return;
            }

            animator.enabled = true;
            animator.speed = animatorSpeed;
            animator.applyRootMotion = false;
            animator.Play(_locomotionStateHash, 0, 0f);
        }

        private void StoreResetPose()
        {
            _resetPosition = transform.position;
            _resetRotation = transform.rotation;
            _hasStoredResetPose = true;
        }
    }
}
