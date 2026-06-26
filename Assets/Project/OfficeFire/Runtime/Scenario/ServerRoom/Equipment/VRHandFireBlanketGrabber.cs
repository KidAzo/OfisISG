using UnityEngine;
using UnityEngine.InputSystem;
using Woi.Equipment;

namespace Woi.OfficeFire
{
    /// <summary>
    /// VR grip grab/release for <see cref="FireBlanketPickupItem"/> — ported from WOI.Shared.Global.
    /// Uses runtime XR grip binding (same pattern as <see cref="VrSelectableInteractor"/>).
    /// </summary>
    [AddComponentMenu("Woi/Office Fire/VR Hand Fire Blanket Grabber")]
    [DisallowMultipleComponent]
    public sealed class VRHandFireBlanketGrabber : MonoBehaviour
    {
        [Header("Hand Settings")]
        [SerializeField]
        private VRHandType handType;

        [SerializeField]
        private Transform holderTransform;

        [Header("Offsets")]
        [SerializeField]
        private Vector3 localPositionOffset = Vector3.zero;

        [SerializeField]
        private Vector3 localEulerRotationOffset = Vector3.zero;

        [Header("Options")]
        [SerializeField]
        private bool allowGrabIfButtonAlreadyHeld;

        [SerializeField]
        private bool enableDebugLogs;

        [Header("Detection")]
        [SerializeField]
        private float grabRadius = 0.25f;

        [SerializeField]
        private LayerMask detectionLayerMask = Physics.AllLayers;

        [Header("Release — world physics")]
        [SerializeField]
        private bool solidifyCollidersOnRelease = true;

        [SerializeField]
        private LayerMask groundProbeLayers;

        [SerializeField, Min(0f)]
        private float groundProbePaddingAboveBounds = 0.35f;

        [SerializeField, Min(0.01f)]
        private float groundProbeMaxDistance = 4f;

        [SerializeField, Min(0f)]
        private float groundClearanceSkin = 0.04f;

        [SerializeField]
        private bool useTriggerCollidersWhileHeld = true;

        [Header("Held — VR ağırlık hissi")]
        [SerializeField]
        private bool enableHeldWeightFeel = true;

        [SerializeField, Min(0.02f)]
        private float heldPositionSmoothTime = 0.13f;

        [SerializeField, Min(0.5f)]
        private float heldMaxFollowSpeed = 6f;

        [SerializeField, Min(1f)]
        private float heldRotationFollowSpeed = 14f;

        [Header("Held — teleport / büyük sıçrama")]
        [SerializeField, Min(0.05f)]
        private float heldTeleportSnapMinHolderOrGoalDeltaMeters = 0.35f;

        [SerializeField, Min(0.1f)]
        private float heldTeleportSnapMaxHeldDistanceFromGoalMeters = 0.65f;

        [Header("Equipment Reference")]
        [SerializeField]
        private PlayerFireBlanketEquipment _trainingEquipmentNotify;

        private InputAction _grabAction;
        private FireBlanketPickupItem _nearbyBlanket;
        private FireBlanketPickupItem _heldBlanket;
        private bool _isGrabButtonHeld;

        private Rigidbody _heldRigidbody;
        private bool _wasKinematic;
        private bool _usedGravity;

        private Vector3 _heldWeightPosVelocity;
        private Vector3 _prevHeldGoalWorldPos;
        private Vector3 _prevHolderWorldPos;
        private bool _heldGoalPrevValid;

        public static int GlobalHeldBlanketCount { get; private set; }

        private void Awake()
        {
            _heldBlanket = null;
            _nearbyBlanket = null;

            if (holderTransform == null)
            {
                holderTransform = transform;
                if (enableDebugLogs)
                {
                    Debug.LogWarning(
                        $"[VRHandFireBlanketGrabber] Holder transform is null on {gameObject.name}. Using self.",
                        this);
                }
            }

            ResolveTrainingEquipmentNotify();
        }

        private void OnEnable()
        {
            EnsureGrabAction();
            _grabAction.started += OnGrabStarted;
            _grabAction.canceled += OnGrabCanceled;
            _grabAction.Enable();
        }

        private void OnDisable()
        {
            if (_grabAction != null)
            {
                _grabAction.started -= OnGrabStarted;
                _grabAction.canceled -= OnGrabCanceled;
                _grabAction.Disable();
            }

            if (_heldBlanket != null)
            {
                ReleaseBlanket();
            }

            _nearbyBlanket = null;
        }

        private void OnDestroy()
        {
            if (_grabAction == null)
            {
                return;
            }

            _grabAction.started -= OnGrabStarted;
            _grabAction.canceled -= OnGrabCanceled;
            _grabAction.Dispose();
            _grabAction = null;
        }

        private void EnsureGrabAction()
        {
            if (_grabAction != null)
            {
                return;
            }

            string hand = handType == VRHandType.Left ? "LeftHand" : "RightHand";
            _grabAction = new InputAction(
                $"VrBlanketGrab{hand}",
                InputActionType.Button,
                $"<XRController>{{{hand}}}/{{GripButton}}");
        }

        private void Update()
        {
            if (_grabAction != null)
            {
                bool wasHeld = _isGrabButtonHeld;
                _isGrabButtonHeld = _grabAction.IsPressed();

                if (allowGrabIfButtonAlreadyHeld && _isGrabButtonHeld && !wasHeld)
                {
                    // just pressed this frame
                }
                else if (allowGrabIfButtonAlreadyHeld && _isGrabButtonHeld && _heldBlanket == null)
                {
                    TryGrab();
                }
            }

            UpdateNearbyBlanket();
        }

        private void LateUpdate()
        {
            if (_heldBlanket == null || !enableHeldWeightFeel)
            {
                return;
            }

            UpdateHeldWeightFollow();
        }

        private void UpdateNearbyBlanket()
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                grabRadius,
                detectionLayerMask,
                QueryTriggerInteraction.Collide);

            float closestDistance = float.MaxValue;
            FireBlanketPickupItem closestItem = null;
            Vector3 myPos = transform.position;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                FireBlanketPickupItem item = hit.GetComponentInParent<FireBlanketPickupItem>();
                if (item == null || item.IsEquipped || item == _heldBlanket || item.IsConsumed)
                {
                    continue;
                }

                float dist = Vector3.Distance(myPos, item.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closestItem = item;
                }
            }

            _nearbyBlanket = closestItem;
        }

        private void OnGrabStarted(InputAction.CallbackContext _)
        {
            TryGrab();
        }

        private void TryGrab()
        {
            if (GlobalHeldBlanketCount > 0
                || VRHandCarafeGrabber.GlobalHeldCarafeCount > 0
                || _heldBlanket != null
                || _nearbyBlanket == null)
            {
                return;
            }

            if (_nearbyBlanket.IsEquipped || _nearbyBlanket.IsConsumed)
            {
                return;
            }

            GrabBlanket(_nearbyBlanket);
        }

        private void OnGrabCanceled(InputAction.CallbackContext _)
        {
            if (_heldBlanket != null)
            {
                ReleaseBlanket();
            }
        }

        private void GrabBlanket(FireBlanketPickupItem item)
        {
            if (item == null)
            {
                return;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[VRHandFireBlanketGrabber] {handType} grabbed {item.name}", this);
            }

            _heldBlanket = item;

            _heldRigidbody = item.GetComponent<Rigidbody>();
            if (_heldRigidbody == null)
            {
                _heldRigidbody = item.GetComponentInChildren<Rigidbody>();
            }

            if (_heldRigidbody == null)
            {
                _heldRigidbody = item.gameObject.AddComponent<Rigidbody>();
                _heldRigidbody.mass = 2f;
                _wasKinematic = true;
                _usedGravity = false;
            }

            if (_heldRigidbody != null)
            {
                _wasKinematic = _heldRigidbody.isKinematic;
                _usedGravity = _heldRigidbody.useGravity;

                _heldRigidbody.linearVelocity = Vector3.zero;
                _heldRigidbody.angularVelocity = Vector3.zero;
                _heldRigidbody.isKinematic = true;
                _heldRigidbody.useGravity = false;
                _heldRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                _heldRigidbody.interpolation = RigidbodyInterpolation.None;
            }

            if (useTriggerCollidersWhileHeld)
            {
                ApplyTriggerCollidersForVrHeld(item.transform);
            }

            Physics.SyncTransforms();

            Transform itemTransform = item.transform;
            if (enableHeldWeightFeel)
            {
                itemTransform.SetParent(null, worldPositionStays: true);
                ComputeHeldGoalTransform(out Vector3 goalPos, out Quaternion goalRot);
                itemTransform.SetPositionAndRotation(goalPos, goalRot);
                if (_heldRigidbody != null)
                {
                    _heldRigidbody.position = goalPos;
                    _heldRigidbody.rotation = goalRot;
                }

                _heldWeightPosVelocity = Vector3.zero;
                _heldGoalPrevValid = false;
            }
            else
            {
                itemTransform.SetParent(holderTransform, worldPositionStays: true);
                itemTransform.localPosition = localPositionOffset;
                itemTransform.localEulerAngles = localEulerRotationOffset;
            }

            GlobalHeldBlanketCount++;
            item.SetEquippedState(true);

            ResolveTrainingEquipmentNotify();
            _trainingEquipmentNotify?.NotifyVrEquipped(item);
        }

        private void ComputeHeldGoalTransform(out Vector3 worldPos, out Quaternion worldRot)
        {
            worldPos = holderTransform.TransformPoint(localPositionOffset);
            worldRot = holderTransform.rotation * Quaternion.Euler(localEulerRotationOffset);
        }

        private void UpdateHeldWeightFollow()
        {
            if (holderTransform == null || _heldBlanket == null)
            {
                return;
            }

            ComputeHeldGoalTransform(out Vector3 goalPos, out Quaternion goalRot);
            Vector3 holderWorld = holderTransform.position;

            Transform t = _heldBlanket.transform;
            if (_heldGoalPrevValid)
            {
                float dGoal = Vector3.Distance(goalPos, _prevHeldGoalWorldPos);
                float dHolder = Vector3.Distance(holderWorld, _prevHolderWorldPos);
                float dHeldFromGoal = Vector3.Distance(t.position, goalPos);
                if (dHolder >= heldTeleportSnapMinHolderOrGoalDeltaMeters
                    || dGoal >= heldTeleportSnapMinHolderOrGoalDeltaMeters
                    || dHeldFromGoal >= heldTeleportSnapMaxHeldDistanceFromGoalMeters)
                {
                    SnapHeldToGoal(goalPos, goalRot);
                    _prevHeldGoalWorldPos = goalPos;
                    _prevHolderWorldPos = holderWorld;
                    return;
                }
            }
            else
            {
                _heldGoalPrevValid = true;
            }

            _prevHeldGoalWorldPos = goalPos;
            _prevHolderWorldPos = holderWorld;

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            Vector3 nextPos = Vector3.SmoothDamp(
                t.position,
                goalPos,
                ref _heldWeightPosVelocity,
                heldPositionSmoothTime,
                heldMaxFollowSpeed,
                dt);

            float rotT = 1f - Mathf.Exp(-heldRotationFollowSpeed * dt);
            Quaternion nextRot = Quaternion.Slerp(t.rotation, goalRot, rotT);

            t.SetPositionAndRotation(nextPos, nextRot);
        }

        private void SnapHeldToGoal(Vector3 goalPos, Quaternion goalRot)
        {
            Transform t = _heldBlanket.transform;
            t.SetPositionAndRotation(goalPos, goalRot);
            if (_heldRigidbody != null)
            {
                _heldRigidbody.position = goalPos;
                _heldRigidbody.rotation = goalRot;
            }

            _heldWeightPosVelocity = Vector3.zero;
        }

        private void ReleaseBlanket()
        {
            if (_heldBlanket == null)
            {
                return;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[VRHandFireBlanketGrabber] {handType} released {_heldBlanket.name}", this);
            }

            ResolveTrainingEquipmentNotify();

            bool placedOnFire = TryPlaceBlanketOnNearbyFire();

            if (!placedOnFire && _heldBlanket.gameObject != null)
            {
                DropBlanketAtCurrentPosition();
            }

            _trainingEquipmentNotify?.NotifyVrUnequipped(_heldBlanket);

            _heldBlanket = null;
            _heldRigidbody = null;
            _heldWeightPosVelocity = Vector3.zero;
            _heldGoalPrevValid = false;

            GlobalHeldBlanketCount--;
            if (GlobalHeldBlanketCount < 0)
            {
                GlobalHeldBlanketCount = 0;
            }
        }

        private bool TryPlaceBlanketOnNearbyFire()
        {
            FireBlanketUseController[] useControllers = FindObjectsByType<FireBlanketUseController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < useControllers.Length; i++)
            {
                FireBlanketUseController useController = useControllers[i];
                if (useController == null || !useController.isActiveAndEnabled)
                {
                    continue;
                }

                if (!useController.IsPlayerNearAssignedFireSource())
                {
                    continue;
                }

                if (useController.TryHandleBlanketDropOrUse())
                {
                    return true;
                }
            }

            return false;
        }

        private void DropBlanketAtCurrentPosition()
        {
            if (_heldBlanket == null || _heldBlanket.gameObject == null)
            {
                return;
            }

            _heldBlanket.transform.SetParent(null, worldPositionStays: true);
            SetCollidersEnabled(_heldBlanket.transform, true);

            if (solidifyCollidersOnRelease)
            {
                SolidifyCollidersForDynamicWorldDrop(_heldBlanket.transform);
            }

            Physics.SyncTransforms();

            if (_heldRigidbody != null)
            {
                NudgePickupAboveGroundIfEmbedded(_heldBlanket.transform, _heldRigidbody);

                _heldRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                _heldRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                _heldRigidbody.isKinematic = false;
                _heldRigidbody.useGravity = true;
                _heldRigidbody.WakeUp();
                _heldRigidbody.linearVelocity = Vector3.zero;
                _heldRigidbody.angularVelocity = Vector3.zero;
                Physics.SyncTransforms();
            }

            _heldBlanket.SetEquippedState(false);
            _trainingEquipmentNotify?.NotifyDropped(_heldBlanket);
        }

        private static void SolidifyCollidersForDynamicWorldDrop(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                if (collider is MeshCollider mesh && !mesh.convex)
                {
                    continue;
                }

                collider.isTrigger = false;
            }
        }

        private static void ApplyTriggerCollidersForVrHeld(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                collider.isTrigger = true;
            }
        }

        private static void SetCollidersEnabled(Transform root, bool enabled)
        {
            if (root == null)
            {
                return;
            }

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider != null)
                {
                    collider.enabled = enabled;
                }
            }
        }

        private void NudgePickupAboveGroundIfEmbedded(Transform root, Rigidbody rb)
        {
            if (root == null || rb == null)
            {
                return;
            }

            Collider[] cols = root.GetComponentsInChildren<Collider>(true);
            if (cols.Length == 0)
            {
                return;
            }

            bool any = false;
            Bounds bounds = default;
            for (int i = 0; i < cols.Length; i++)
            {
                Collider col = cols[i];
                if (col == null || !col.enabled)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = col.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }

            if (!any)
            {
                return;
            }

            int mask = groundProbeLayers.value == 0 ? Physics.DefaultRaycastLayers : groundProbeLayers;
            Vector3 from = new Vector3(bounds.center.x, bounds.max.y + groundProbePaddingAboveBounds, bounds.center.z);
            float dist = groundProbePaddingAboveBounds + bounds.size.y + groundProbeMaxDistance;

            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, dist, mask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            float clearance = hit.point.y - bounds.min.y;
            if (clearance >= groundClearanceSkin)
            {
                return;
            }

            Vector3 delta = Vector3.up * (groundClearanceSkin - clearance);
            rb.position += delta;
            Physics.SyncTransforms();
        }

        private void ResolveTrainingEquipmentNotify()
        {
            if (_trainingEquipmentNotify != null)
            {
                return;
            }

            if (holderTransform == null)
            {
                return;
            }

            _trainingEquipmentNotify = holderTransform.GetComponentInParent<PlayerFireBlanketEquipment>();
            if (_trainingEquipmentNotify != null)
            {
                return;
            }

            PlayerFireBlanketEquipment[] found =
                holderTransform.root.GetComponentsInChildren<PlayerFireBlanketEquipment>(true);
            if (found.Length == 1)
            {
                _trainingEquipmentNotify = found[0];
                return;
            }

            if (found.Length > 1)
            {
                Debug.LogWarning(
                    $"[VRHandFireBlanketGrabber] '{name}': XR kökünde birden fazla PlayerFireBlanketEquipment ({found.Length}). " +
                    "Equipment Reference alanına doğru referansı atayın.",
                    this);
            }
        }
    }
}
