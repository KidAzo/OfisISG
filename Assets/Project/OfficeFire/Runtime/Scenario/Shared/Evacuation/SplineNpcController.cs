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

        [Tooltip("Uses path default speed when <= 0.")]
        [SerializeField]
        [Min(0f)]
        private float runSpeed = -1f;

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

        [Tooltip("Meters ahead on the path used for facing. Longer = less left/right wobble on straight segments.")]
        [SerializeField]
        [Min(0.1f)]
        private float rotationLookAheadDistance = 1.5f;

        [SerializeField]
        private bool playIdleAtPathEnd = true;

        [SerializeField]
        private bool playOnStartForTesting;

        private Vector3 _resetPosition;
        private Quaternion _resetRotation;
        private Quaternion _animatorInitialLocalRotation;
        private Vector3 _lastPathPosition;
        private bool _hasLastPathPosition;
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
            if (animator != null && animator.transform != transform)
            {
                _animatorInitialLocalRotation = animator.transform.localRotation;
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

        private void LateUpdate()
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

            SyncAnimatorPlaybackSpeed(speed);
            ApplyPose(_normalizedTime, speed, false);
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
            _hasLastPathPosition = false;

            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            PlayAnimation(locomotionMode);

            if (MovesAlongPath)
            {
                float speed = GetMoveSpeed();
                SyncAnimatorPlaybackSpeed(speed);
                ApplyPose(_normalizedTime, speed, true);
            }
        }

        public void StopEvacuation(bool resetPose = true)
        {
            _isRunning = false;
            _delayRemaining = 0f;
            _loggedMovementBlocked = false;
            _hasLastPathPosition = false;

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

            ApplyPose(Mathf.Clamp01(startNormalizedT), GetMoveSpeed(), true);

            if (storeAsResetPose)
            {
                StoreResetPose();
            }
        }

        private void HandlePathEnd()
        {
            _normalizedTime = 1f;
            ApplyPose(1f, GetMoveSpeed(), false);

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
                NpcLocomotionMode.Run when runSpeed > 0f => runSpeed,
                NpcLocomotionMode.Run => path != null ? path.DefaultMoveSpeed * 1.6f : 0f,
                NpcLocomotionMode.Walk when walkSpeed > 0f => walkSpeed,
                NpcLocomotionMode.Walk => path != null ? path.DefaultMoveSpeed : 0f,
                _ => 0f,
            };
        }

        private float GetReferenceMoveSpeed()
        {
            if (locomotionMode == NpcLocomotionMode.Run)
            {
                return runSpeed > 0f ? runSpeed : path != null ? path.DefaultMoveSpeed * 1.6f : 0f;
            }

            return walkSpeed > 0f ? walkSpeed : path != null ? path.DefaultMoveSpeed : 0f;
        }

        private void SyncAnimatorPlaybackSpeed(float moveSpeed)
        {
            if (animator == null || locomotionMode == NpcLocomotionMode.Idle)
            {
                return;
            }

            float baseSpeed = locomotionMode == NpcLocomotionMode.Run ? runAnimatorSpeed : walkAnimatorSpeed;
            float referenceSpeed = GetReferenceMoveSpeed();
            if (referenceSpeed <= 0.001f || moveSpeed <= 0.001f)
            {
                animator.speed = baseSpeed;
                return;
            }

            animator.speed = baseSpeed * (moveSpeed / referenceSpeed);
        }

        private void ApplyPose(float normalizedTime, float moveSpeed, bool instantRotation = false)
        {
            if (!path.TrySample(normalizedTime, out Vector3 position, out Vector3 tangent))
            {
                return;
            }

            transform.position = position;

            if (!faceMovementDirection)
            {
                _lastPathPosition = position;
                _hasLastPathPosition = true;
                return;
            }

            Vector3 forward = ComputeFacingDirection(normalizedTime, position, tangent, moveSpeed);
            if (forward.sqrMagnitude <= 1e-6f)
            {
                return;
            }

            Quaternion targetRot = Quaternion.LookRotation(forward.normalized, Vector3.up);
            if (instantRotation || !Application.isPlaying)
            {
                transform.rotation = targetRot;
            }
            else
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 25f);
            }

            // Forcefully prevent the Run animation clip from twisting the child mesh's local rotation
            if (Application.isPlaying && animator != null && animator.transform != transform)
            {
                animator.transform.localRotation = _animatorInitialLocalRotation;
            }
        }

        private Vector3 ComputeFacingDirection(
            float normalizedTime,
            Vector3 position,
            Vector3 tangentFallback,
            float moveSpeed)
        {
            if (_hasLastPathPosition)
            {
                Vector3 travel = position - _lastPathPosition;
                if (travel.sqrMagnitude > 1e-6f)
                {
                    _lastPathPosition = position;
                    return FlattenForward(travel);
                }
            }

            _lastPathPosition = position;
            _hasLastPathPosition = true;
            return ComputeStableForward(normalizedTime, position, tangentFallback, moveSpeed);
        }

        private Vector3 ComputeStableForward(
            float normalizedTime,
            Vector3 position,
            Vector3 tangentFallback,
            float moveSpeed)
        {
            float pathLength = path.GetLength();
            if (pathLength > 0.1f)
            {
                float lookAhead = Mathf.Max(rotationLookAheadDistance, moveSpeed * 0.45f);
                float lookAheadT = Mathf.Clamp01(normalizedTime + (lookAhead / pathLength));
                if (lookAheadT > normalizedTime + 1e-5f
                    && path.TrySample(lookAheadT, out Vector3 aheadPos, out _))
                {
                    Vector3 delta = aheadPos - position;
                    if (delta.sqrMagnitude > 1e-4f)
                    {
                        return FlattenForward(delta);
                    }
                }
            }

            return FlattenForward(tangentFallback);
        }

        private Vector3 FlattenForward(Vector3 forward)
        {
            if (!keepUpright)
            {
                return forward;
            }

            forward.y = 0f;
            if (forward.sqrMagnitude > 1e-6f)
            {
                return forward;
            }

            forward = transform.forward;
            forward.y = 0f;
            return forward;
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
