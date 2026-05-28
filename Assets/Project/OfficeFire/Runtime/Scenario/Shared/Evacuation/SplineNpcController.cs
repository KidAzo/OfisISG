using System.Collections;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Moves a humanoid along an <see cref="EvacuationPath"/> spline, or plays the configured
    /// locomotion animation in place when no path is assigned.
    /// Started/stopped by <see cref="EvacuationNpcDirector"/> during evacuation.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class SplineNpcController : MonoBehaviour
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
        private float startDelay;

        [SerializeField]
        [Range(0f, 1f)]
        private float startNormalizedT;

        [Header("Locomotion")]
        [SerializeField]
        private NpcLocomotionMode locomotionMode = NpcLocomotionMode.Walk;

        [Tooltip("Uses path default speed when <= 0.")]
        [SerializeField]
        [Min(0f)]
        private float walkSpeed = -1f;

        [SerializeField]
        [Min(0.1f)]
        private float runSpeed = 4.5f;

        [Header("Animation")]
        [SerializeField]
        private Animator animator;

        [SerializeField]
        private string idleStateName = "Breathing Idle";

        [SerializeField]
        private string walkStateName = "Walking";

        [SerializeField]
        private string runStateName = "Walking";

        [SerializeField]
        [Min(0.1f)]
        private float walkAnimatorSpeed = 1.15f;

        [SerializeField]
        [Min(0.1f)]
        private float runAnimatorSpeed = 1.6f;

        [Header("Behaviour")]
        [SerializeField]
        private EndBehaviour endBehaviour = EndBehaviour.DisableGameObject;

        [SerializeField]
        private bool faceMovementDirection = true;

        [SerializeField]
        private bool keepUpright = true;

        [SerializeField]
        private bool playIdleAtPathEnd = true;

        [SerializeField]
        private bool playOnStartForTesting;

        private Vector3 _resetPosition;
        private Quaternion _resetRotation;
        private float _normalizedTime;
        private float _delayRemaining;
        private bool _isRunning;
        private bool _hasStoredResetPose;
        private bool _loggedMovementBlocked;
        private int _idleStateHash;
        private int _walkStateHash;
        private int _runStateHash;

        public EvacuationPath Path => path;

        public NpcLocomotionMode LocomotionMode => locomotionMode;

        public bool IsRunning => _isRunning;

        private bool MovesAlongPath =>
            path != null && locomotionMode != NpcLocomotionMode.Idle;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            CacheAnimationHashes();
            StoreResetPose();

            if (locomotionMode == NpcLocomotionMode.Idle)
            {
                PlayAnimation(NpcLocomotionMode.Idle);
            }
        }

        private void Start()
        {
            if (playOnStartForTesting)
            {
                StartCoroutine(BeginAfterScenarioBootstrap());
            }
        }

        private IEnumerator BeginAfterScenarioBootstrap()
        {
            yield return null;
            Begin();
        }

        private void Update()
        {
            if (!_isRunning || !MovesAlongPath)
            {
                return;
            }

            if (_delayRemaining > 0f)
            {
                _delayRemaining -= Time.deltaTime;
                return;
            }

            float speed = GetMoveSpeed();
            float length = path.GetLength();
            if (length <= 0.001f || speed <= 0.001f)
            {
                LogMovementBlockedOnce(length, speed);
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
            if (!_hasStoredResetPose)
            {
                StoreResetPose();
            }

            _normalizedTime = Mathf.Clamp01(startNormalizedT);
            _delayRemaining = startDelay;
            _isRunning = true;
            _loggedMovementBlocked = false;

            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            PlayAnimation(locomotionMode);

            if (MovesAlongPath)
            {
                ApplyPose(_normalizedTime);
            }
        }

        public void StopEvacuation(bool resetPose = true)
        {
            _isRunning = false;
            _delayRemaining = 0f;
            _loggedMovementBlocked = false;

            if (locomotionMode != NpcLocomotionMode.Idle)
            {
                PlayAnimation(NpcLocomotionMode.Idle);
            }

            if (resetPose && _hasStoredResetPose)
            {
                transform.SetPositionAndRotation(_resetPosition, _resetRotation);
            }
        }

        public void SetPath(EvacuationPath evacuationPath)
        {
            path = evacuationPath;
        }

        public void SnapToPathStart(bool storeAsResetPose = true)
        {
            if (path == null)
            {
                return;
            }

            ApplyPose(Mathf.Clamp01(startNormalizedT));

            if (storeAsResetPose)
            {
                StoreResetPose();
            }
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
                    if (playIdleAtPathEnd)
                    {
                        PlayAnimation(NpcLocomotionMode.Idle);
                    }

                    break;
            }
        }

        private float GetMoveSpeed()
        {
            return locomotionMode switch
            {
                NpcLocomotionMode.Run => runSpeed,
                NpcLocomotionMode.Walk when walkSpeed > 0f => walkSpeed,
                NpcLocomotionMode.Walk => path != null ? path.DefaultMoveSpeed : 0f,
                _ => 0f,
            };
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

        private void PlayAnimation(NpcLocomotionMode mode)
        {
            if (animator == null)
            {
                return;
            }

            int stateHash;
            float speed;

            switch (mode)
            {
                case NpcLocomotionMode.Run:
                    stateHash = _runStateHash;
                    speed = runAnimatorSpeed;
                    break;
                case NpcLocomotionMode.Walk:
                    stateHash = _walkStateHash;
                    speed = walkAnimatorSpeed;
                    break;
                default:
                    stateHash = _idleStateHash;
                    speed = 1f;
                    break;
            }

            animator.enabled = true;
            animator.speed = speed;
            animator.applyRootMotion = false;
            animator.Play(stateHash, 0, 0f);
        }

        private void CacheAnimationHashes()
        {
            _idleStateHash = Animator.StringToHash(idleStateName);
            _walkStateHash = Animator.StringToHash(walkStateName);
            _runStateHash = Animator.StringToHash(runStateName);
        }

        private void StoreResetPose()
        {
            _resetPosition = transform.position;
            _resetRotation = transform.rotation;
            _hasStoredResetPose = true;
        }

        private void LogMovementBlockedOnce(float pathLength, float speed)
        {
            if (_loggedMovementBlocked)
            {
                return;
            }

            _loggedMovementBlocked = true;
            Debug.LogWarning(
                $"[SplineNpcController] Walking animation is active but movement is blocked on '{name}'. " +
                $"pathLength={pathLength:F3}, speed={speed:F3}, pathAssigned={path != null}. " +
                "Check spline knots/length, walk speed, and that evacuation was not stopped by scenario reset.",
                this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheAnimationHashes();
        }
#endif
    }
}
