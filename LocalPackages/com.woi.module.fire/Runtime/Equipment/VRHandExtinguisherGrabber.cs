using FireExtinguisher.PC;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Woi.Equipment
{
    public enum VRHandType { Left, Right }

    [AddComponentMenu("Woi/Equipment/VR Hand Extinguisher Grabber")]
    public class VRHandExtinguisherGrabber : MonoBehaviour
    {
        [Header("Hand Settings")]
        public VRHandType handType;
        public Transform holderTransform;
        
        [Header("Offsets")]
        public Vector3 localPositionOffset = Vector3.zero;
        public Vector3 localEulerRotationOffset = Vector3.zero;

        [Header("Input")]
        [Tooltip("Direct reference to the VR controller's Grab action (e.g., Grip or Trigger).")]
        public InputActionReference grabInput;

        [Header("Options")]
        public bool allowGrabIfButtonAlreadyHeld = false;
        public bool enableDebugLogs = false;

        [Header("Detection")]
        [Tooltip("Elinizin tüpü algılayabileceği maksimum mesafe (yarıçap)")]
        public float grabRadius = 0.25f;
        
        [Tooltip("Sadece tüplerin bulunduğu layer'ı seçerseniz performans artar.")]
        public LayerMask detectionLayerMask = Physics.AllLayers;

        [Header("Release — world physics")]
        [Tooltip("Bırakınca tüp collider'larında isTrigger kapatılır (Box/Sphere/Capsule + convex Mesh). " +
                 "Non-convex MeshCollider atlanır — zemin için prefab'ta convex küçük mesh veya primitive collider ekleyin.")]
        [SerializeField]
        private bool solidifyCollidersOnRelease = true;

        [Tooltip("Zemine gömülme düzeltmesi için ray maske; boş (0) ise DefaultRaycastLayers.")]
        [SerializeField]
        private LayerMask groundProbeLayers;

        [SerializeField, Min(0f)]
        private float groundProbePaddingAboveBounds = 0.35f;

        [SerializeField, Min(0.01f)]
        private float groundProbeMaxDistance = 4f;

        [SerializeField, Min(0f)]
        private float groundClearanceSkin = 0.04f;

        [Tooltip("Eldeyken collider'ları trigger yapar (bırakınca solidify ile zemin). Dynamic RB + solid mesh el/vücut ile çakışmayı keser.")]
        [SerializeField]
        private bool useTriggerCollidersWhileHeld = true;

        [Header("Held — VR ağırlık hissi")]
        [Tooltip("Kapalı: tüp holder'a parent — sıfır gecikme (balon hissi). Açık: parent yok; pozisyon/rotasyon el hedefini SmoothDamp ile takip eder.")]
        [SerializeField]
        private bool enableHeldWeightFeel = true;

        [Tooltip("Düşük = daha yapışkan, yüksek = daha ağır / geride kalır.")]
        [SerializeField, Min(0.02f)]
        private float heldPositionSmoothTime = 0.13f;

        [Tooltip("El çok hızlanınca tüpün saniyede katedebileceği üst sınır; düşürürseniz sallanırken daha çok geride kalır.")]
        [SerializeField, Min(0.5f)]
        private float heldMaxFollowSpeed = 6f;

        [Tooltip("Rotasyonun ele yetişme hızı (büyük = daha sıkı tutuş).")]
        [SerializeField, Min(1f)]
        private float heldRotationFollowSpeed = 14f;

        [Header("Held — teleport / büyük sıçrama")]
        [Tooltip(
            "Teleport veya rig tek karede büyük atlayınca SmoothDamp tüpü geride bırakır. El (holder) veya hedef pozisyonu " +
            "bu kadar metreden fazla değiştiyse tüpü bir karede hedefe yapıştır.")]
        [SerializeField, Min(0.05f)]
        private float heldTeleportSnapMinHolderOrGoalDeltaMeters = 0.35f;

        [Tooltip(
            "El hedefi ile tüp merkezi arası bu kadardan fazlaysa (teleport sonrası birikmiş gecikme) hedefe snap — mesafe tabanlı yakalama.")]
        [SerializeField, Min(0.1f)]
        private float heldTeleportSnapMaxHeldDistanceFromGoalMeters = 0.65f;

        [Header("Debug")]
        [Tooltip(
            "VR tutuşunda PlayerExtinguisherEquipment.CurrentItem güncellenir (SOAP, proximity, kayıt). " +
            "Boşsa holder üstünde veya kökte tek PlayerExtinguisherEquipment aranır; birden fazlaysa Inspector’dan atayın.")]
        [SerializeField]
        private PlayerExtinguisherEquipment _trainingEquipmentNotify;

        [SerializeField] private ExtinguisherPickupItem nearbyExtinguisher;
        [SerializeField] private ExtinguisherPickupItem heldExtinguisher;
        public ExtinguisherPickupItem HeldExtinguisher => heldExtinguisher;
        [SerializeField] private bool isGrabButtonHeld;
        [SerializeField] private Vector3 lastVRDropPosition;

        private bool _wasGrabHeldOnEnter;

        // Saved state for drop
        private Rigidbody _heldRigidbody;
        private bool _wasKinematic;
        private bool _usedGravity;

        private Vector3 _heldWeightPosVelocity;
        private Vector3 _prevHeldGoalWorldPos;
        private Vector3 _prevHolderWorldPos;
        private bool _heldGoalPrevValid;

        public bool IsHoldingExtinguisher => heldExtinguisher != null;

        // Statik değişken: Sahne genelinde herhangi bir elin tüp tutup tutmadığını takip eder.
        public static int GlobalHeldExtinguisherCount { get; private set; }

        private void Awake()
        {
            if (holderTransform == null)
            {
                holderTransform = transform;
                if (enableDebugLogs)
                    Debug.LogWarning($"[VRHandExtinguisherGrabber] Holder transform is null on {gameObject.name}. Using self.");
            }

            ResolveTrainingEquipmentNotify();
        }

        private void OnEnable()
        {
            if (grabInput != null && grabInput.action != null)
            {
                grabInput.action.Enable();
                grabInput.action.started += OnGrabStarted;
                grabInput.action.canceled += OnGrabCanceled;
            }
        }

        private void OnDisable()
        {
            if (grabInput != null && grabInput.action != null)
            {
                grabInput.action.started -= OnGrabStarted;
                grabInput.action.canceled -= OnGrabCanceled;
            }

            if (heldExtinguisher != null)
            {
                ReleaseExtinguisher();
            }
            
            nearbyExtinguisher = null;
        }

        private void Update()
        {
            if (grabInput != null && grabInput.action != null)
            {
                bool wasHeld = isGrabButtonHeld;
                isGrabButtonHeld = grabInput.action.IsPressed();

                // Eğer "allowGrabIfButtonAlreadyHeld" açıksa ve butona zaten basılı tutarak 
                // tüpe yaklaştıysak tüpü otomatik kapmak için kontrol ediyoruz.
                if (allowGrabIfButtonAlreadyHeld && isGrabButtonHeld && !wasHeld)
                {
                    // just pressed this frame, standard grab handles it via OnGrabStarted
                }
                else if (allowGrabIfButtonAlreadyHeld && isGrabButtonHeld && heldExtinguisher == null)
                {
                    TryGrab();
                }
            }

            UpdateNearbyExtinguisher();
        }

        private void LateUpdate()
        {
            if (heldExtinguisher == null || !enableHeldWeightFeel)
                return;

            UpdateHeldWeightFollow();
        }

        private void UpdateNearbyExtinguisher()
        {
            // Trigger yerine belirlediğimiz bir küre alanı içindeki tüm collider'ları tararız.
            Collider[] hits = Physics.OverlapSphere(transform.position, grabRadius, detectionLayerMask, QueryTriggerInteraction.Collide);

            float closestDistance = float.MaxValue;
            ExtinguisherPickupItem closestItem = null;
            Vector3 myPos = transform.position;

            foreach (var hit in hits)
            {
                var item = hit.GetComponentInParent<ExtinguisherPickupItem>();
                if (item == null) continue;

                // Eğer tüp zaten PC veya başka VR eli tarafından tutuluyorsa atla
                if (item.IsEquipped || item == heldExtinguisher)
                    continue;

                float dist = Vector3.Distance(myPos, item.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closestItem = item;
                }
            }

            nearbyExtinguisher = closestItem;
        }

        private void OnGrabStarted(InputAction.CallbackContext ctx)
        {
            TryGrab();
        }

        private void TryGrab()
        {
            // Eğer diğer el zaten bir tüp tutuyorsa, bu el tüp alamaz.
            if (GlobalHeldExtinguisherCount > 0)
                return;

            if (heldExtinguisher != null)
                return;

            if (nearbyExtinguisher == null)
                return;

            if (nearbyExtinguisher.IsEquipped)
                return;

            GrabExtinguisher(nearbyExtinguisher);
        }

        private void OnGrabCanceled(InputAction.CallbackContext ctx)
        {
            if (heldExtinguisher != null)
            {
                ReleaseExtinguisher();
            }
        }

        private void GrabExtinguisher(ExtinguisherPickupItem item)
        {
            if (item == null) return;

            if (enableDebugLogs) Debug.Log($"[VRHandExtinguisherGrabber] {handType} grabbed {item.name}");

            heldExtinguisher = item;

            _heldRigidbody = item.GetComponent<Rigidbody>();
            if (_heldRigidbody == null)
                _heldRigidbody = item.GetComponentInChildren<Rigidbody>();

            if (_heldRigidbody == null)
            {
                _heldRigidbody = item.gameObject.AddComponent<Rigidbody>();
                _heldRigidbody.mass = 5f;
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
                ApplyTriggerCollidersForVrHeld(item.transform);

            Physics.SyncTransforms();

            // Parent & offsets (RB artık kinematic — el ile çakışan dynamic fizik yok)
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

            GlobalHeldExtinguisherCount++;

            // Collider'ları tamamen kapatmıyoruz: VRExtinguisherPinPuller pim araması OverlapSphere + collider ile yapılıyor.

            // Open spray gate
            var holdStateProvider = item.GetComponentInChildren<PCHoldStateProvider>();
            if (holdStateProvider != null)
            {
                holdStateProvider.Equip();
            }

            var sprayProvider = item.GetComponentInChildren<PCSprayInputProvider>();
            if (sprayProvider != null)
            {
                sprayProvider.OverrideVrHandNode = handType == VRHandType.Left ? UnityEngine.XR.XRNode.LeftHand : UnityEngine.XR.XRNode.RightHand;
            }

            // Reflection to set IsEquipped = true without modifying PC code
            SetIsEquipped(item, true);

            ResolveTrainingEquipmentNotify();
            _trainingEquipmentNotify?.NotifyVrEquipped(item);

            TryScheduleVrNozzleResnapIfPinAlreadyPulled(item);
        }

        void TryScheduleVrNozzleResnapIfPinAlreadyPulled(ExtinguisherPickupItem item)
        {
            if (item?.Controller == null || !item.Controller.IsPinPulled)
                return;

            VRExtinguisherPinPuller[] pullers = FindObjectsByType<VRExtinguisherPinPuller>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (pullers == null || pullers.Length == 0)
                return;

            // Nozzle sağ elde olmalı: önce sağ eldeki PinPuller (anchor genelde orada), yoksa tüpü tutmayan el.
            VRExtinguisherPinPuller rightHandPuller = null;
            VRExtinguisherPinPuller otherThanThisGrabber = null;
            for (int i = 0; i < pullers.Length; i++)
            {
                VRExtinguisherPinPuller p = pullers[i];
                if (p == null)
                    continue;

                VRHandExtinguisherGrabber g = p.myGrabber;
                if (g != null && g.handType == VRHandType.Right)
                    rightHandPuller = p;
                if (g != null && !ReferenceEquals(g, this))
                    otherThanThisGrabber ??= p;
            }

            VRExtinguisherPinPuller chosen = rightHandPuller ?? otherThanThisGrabber ?? pullers[0];
            chosen.ScheduleSnapNozzleIfPinAlreadyPulled(item);
        }

        void ComputeHeldGoalTransform(out Vector3 worldPos, out Quaternion worldRot)
        {
            worldPos = holderTransform.TransformPoint(localPositionOffset);
            worldRot = holderTransform.rotation * Quaternion.Euler(localEulerRotationOffset);
        }

        void UpdateHeldWeightFollow()
        {
            if (holderTransform == null || heldExtinguisher == null)
                return;

            ComputeHeldGoalTransform(out Vector3 goalPos, out Quaternion goalRot);
            Vector3 holderWorld = holderTransform.position;

            Transform t = heldExtinguisher.transform;
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
                _heldGoalPrevValid = true;

            _prevHeldGoalWorldPos = goalPos;
            _prevHolderWorldPos = holderWorld;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

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

        void SnapHeldToGoal(Vector3 goalPos, Quaternion goalRot)
        {
            Transform t = heldExtinguisher.transform;
            t.SetPositionAndRotation(goalPos, goalRot);
            if (_heldRigidbody != null)
            {
                _heldRigidbody.position = goalPos;
                _heldRigidbody.rotation = goalRot;
            }

            _heldWeightPosVelocity = Vector3.zero;
        }

        private void ReleaseExtinguisher()
        {
            if (heldExtinguisher == null) return;

            if (enableDebugLogs) Debug.Log($"[VRHandExtinguisherGrabber] {handType} released {heldExtinguisher.name}");

            ResolveTrainingEquipmentNotify();
            _trainingEquipmentNotify?.NotifyVrUnequipped(heldExtinguisher);

            // Eğer obje destroy edildiyse SetParent hata verir.
            // Unity'nin == null kontrolü bunu yakalar ama yine de null check yapıyoruz.
            if (heldExtinguisher.gameObject != null)
            {
                lastVRDropPosition = heldExtinguisher.transform.position;
                heldExtinguisher.transform.SetParent(null, worldPositionStays: true);

                heldExtinguisher.SetPickupCollidersEnabled(true);

                if (solidifyCollidersOnRelease)
                    SolidifyCollidersForDynamicWorldDrop(heldExtinguisher.transform);

                Physics.SyncTransforms();

                if (_heldRigidbody != null)
                {
                    NudgePickupAboveGroundIfEmbedded(heldExtinguisher.transform, _heldRigidbody);

                    _heldRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    _heldRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                    _heldRigidbody.isKinematic = false;
                    _heldRigidbody.useGravity = true;
                    _heldRigidbody.WakeUp();
                    _heldRigidbody.linearVelocity = Vector3.zero;
                    _heldRigidbody.angularVelocity = Vector3.zero;
                    Physics.SyncTransforms();
                }

                // Close spray gate
                var holdStateProvider = heldExtinguisher.GetComponentInChildren<PCHoldStateProvider>();
                if (holdStateProvider != null)
                {
                    holdStateProvider.Unequip();
                }

                var sprayProvider = heldExtinguisher.GetComponentInChildren<PCSprayInputProvider>();
                if (sprayProvider != null)
                {
                    sprayProvider.OverrideVrHandNode = null;
                }

                SetIsEquipped(heldExtinguisher, false);
            }

            heldExtinguisher = null;
            _heldRigidbody = null;
            _heldWeightPosVelocity = Vector3.zero;
            _heldGoalPrevValid = false;

            GlobalHeldExtinguisherCount--;
            if (GlobalHeldExtinguisherCount < 0) GlobalHeldExtinguisherCount = 0;
        }

        static void SolidifyCollidersForDynamicWorldDrop(Transform root)
        {
            if (root == null)
                return;

            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c == null || !c.enabled)
                    continue;

                if (c is MeshCollider mesh && !mesh.convex)
                    continue;

                c.isTrigger = false;
            }
        }

        static void ApplyTriggerCollidersForVrHeld(Transform root)
        {
            if (root == null)
                return;

            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c == null || !c.enabled)
                    continue;

                c.isTrigger = true;
            }
        }

        void NudgePickupAboveGroundIfEmbedded(Transform root, Rigidbody rb)
        {
            if (root == null || rb == null)
                return;

            Collider[] cols = root.GetComponentsInChildren<Collider>(true);
            if (cols.Length == 0)
                return;

            bool any = false;
            Bounds b = default;
            foreach (Collider col in cols)
            {
                if (col == null || !col.enabled)
                    continue;

                if (!any)
                {
                    b = col.bounds;
                    any = true;
                }
                else
                    b.Encapsulate(col.bounds);
            }

            if (!any)
                return;

            int mask = groundProbeLayers.value == 0 ? Physics.DefaultRaycastLayers : groundProbeLayers;
            Vector3 from = new Vector3(b.center.x, b.max.y + groundProbePaddingAboveBounds, b.center.z);
            float dist = groundProbePaddingAboveBounds + b.size.y + groundProbeMaxDistance;

            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, dist, mask, QueryTriggerInteraction.Ignore))
                return;

            float clearance = hit.point.y - b.min.y;
            if (clearance >= groundClearanceSkin)
                return;

            Vector3 delta = Vector3.up * (groundClearanceSkin - clearance);
            rb.position += delta;
            Physics.SyncTransforms();
        }

        private void SetIsEquipped(ExtinguisherPickupItem item, bool value)
        {
            item.SetEquippedVr(value);
        }

        private void ResolveTrainingEquipmentNotify()
        {
            if (_trainingEquipmentNotify != null)
                return;

            if (holderTransform == null)
                return;

            _trainingEquipmentNotify = holderTransform.GetComponentInParent<PlayerExtinguisherEquipment>();
            if (_trainingEquipmentNotify != null)
                return;

            PlayerExtinguisherEquipment[] found =
                holderTransform.root.GetComponentsInChildren<PlayerExtinguisherEquipment>(true);
            if (found.Length == 1)
            {
                _trainingEquipmentNotify = found[0];
                return;
            }

            if (found.Length > 1)
            {
                Debug.LogWarning(
                    $"[VRHandExtinguisherGrabber] '{name}': XR kökünde birden fazla PlayerExtinguisherEquipment ({found.Length}). " +
                    "Eğitim SOAP / proximity için Debug bölümündeki Training Equipment Notify alanına doğru referansı atayın.",
                    this);
            }
        }
    }
}
