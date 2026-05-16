using Woi.Game;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using WOI.Modules.SDK;

namespace Woi.Equipment
{
    [AddComponentMenu("Woi/Equipment/VR Extinguisher Pin Puller")]
    public class VRExtinguisherPinPuller : MonoBehaviour
    {
        [Header("Hand Setting")]
        [Tooltip("Kendi elimizdeki Grabber referansı. Eğer bu el tüp tutuyorsa pimi ÇEKEMEZ.")]
        public VRHandExtinguisherGrabber myGrabber;

        [Tooltip("Nozzle bu transforma parent edilir. Boşsa bu bileşenin transform'u (genelde pim çeken kontrolcü). Sağ ele snap için sağ kontrolcü anchor'ını buraya verin.")]
        [SerializeField]
        private Transform _nozzleSnapHandAnchor;

        // Nozzle snap: pim çekildikten / zaten çekili kuşanımdan sonra boş el nozzle'a yaklaşınca parent + spline.
        private Transform _snappedNozzle;
        private Transform _originalNozzleParent;
        private ExtinguisherPickupItem _trackedExtinguisher;
        private MonoBehaviour _hoseDriverMono;
        private Coroutine _snapCoroutine;

        /// <summary>
        /// Pim çekildi (veya zaten çekili tüp yeniden kuşanıldı) ama henüz nozzle yaklaşma mesafesine girilmedi.
        /// </summary>
        ExtinguisherPickupItem _pendingNozzleSnapItem;

        static bool s_loggedMissingNozzleSnapGripAction;

        [Header("Nozzle Grab Offsets")]
        [Tooltip("Boş ele gelen nozzle'ın (hortum ucunun) pozisyon ofseti.")]
        public Vector3 nozzleLocalPositionOffset = Vector3.zero;
        
        [Tooltip("Boş ele gelen nozzle'ın rotasyon ofseti. (Elde düzgün durması için x: -90 genelde iyidir).")]
        public Vector3 nozzleLocalEulerRotationOffset = new Vector3(-90f, 0f, 0f);

        [Header("Detection")]
        [Tooltip("El ile pim arasındaki maksimum mesafe (ör: 0.15f = 15cm)")]
        public float pullRadius = 0.15f;
        public LayerMask detectionLayerMask = Physics.AllLayers;
        
        [Tooltip("Pimin bulunduğu objeye verdiğiniz Tag ismi")]
        public string pinTag = "Pin";

        [Tooltip("VR spray / SphereCast kökü: tüp hiyerarşisinde bu isimli Transform (yoksa hose nozzleRoot kullanılır). PC'deki Spray World Origin aynı kalır.")]
        public string vrSprayDetectionTransformName = "Nozzle_low";

        [Header("Nozzle snap (pim çekildikten sonra)")]
        [Tooltip("Pim çekilince otomatik snap yok; boş el (_nozzleSnapHandAnchor veya bu PinPuller) tüp köküne bu kadar yaklaşınca snap + spline yenilenir. " +
                 "(Nozzle_low spline altında kapalı olduğu için mesafe her zaman ExtinguisherPickupItem köküne göre; snap yine hortum nozzle transform’una parent eder.)")]
        [SerializeField, Min(0.02f)]
        float _nozzleSnapProximityRadius = 0.18f;

        [Tooltip("Açıkken hortum/nozzle yalnızca grip basılıyken elde snap olur (yalnızca mesafeye girmek yetmez). Kapalı: eski davranış.")]
        [SerializeField]
        bool _requireGripHoldForNozzleSnap = true;

        [Tooltip("Hortum snap için kullanılan grip. Boşsa myGrabber.grabInput (hortum elindeki VRHandExtinguisherGrabber).")]
        [SerializeField]
        InputActionReference _nozzleSnapGripHold;

        [Header("Input")]
        [Tooltip("PC'deki R tuşuna denk gelen VR pimi çekme butonu")]
        public InputActionReference pullInput;

        private void OnEnable()
        {
            if (pullInput != null && pullInput.action != null)
            {
                pullInput.action.Enable();
                pullInput.action.started += OnPullStarted;
            }
        }

        private void OnDisable()
        {
            if (pullInput != null && pullInput.action != null)
            {
                pullInput.action.started -= OnPullStarted;
            }

            if (_snapCoroutine != null)
            {
                StopCoroutine(_snapCoroutine);
                _snapCoroutine = null;
            }

            RestoreNozzle();
            _pendingNozzleSnapItem = null;
        }

        Transform ResolveNozzleSnapParent() =>
            _nozzleSnapHandAnchor != null ? _nozzleSnapHandAnchor : transform;

        /// <summary>
        /// Pim zaten çekili tüp kuşanıldığında: otomatik snap yok; aynı boş el nozzle mesafesine girince snap tetiklenir.
        /// </summary>
        public void ScheduleSnapNozzleIfPinAlreadyPulled(ExtinguisherPickupItem item)
        {
            if (item == null || item.Controller == null || !item.Controller.IsPinPulled)
                return;

            if (_snapCoroutine != null)
            {
                StopCoroutine(_snapCoroutine);
                _snapCoroutine = null;
            }

            _pendingNozzleSnapItem = item;
        }

        private void Update()
        {
            TryProcessPendingNozzleProximitySnap();

            // Eğer elimize bir nozzle yapıştırdıysak, tüp yere bırakıldığı an nozzle'ı eski yerine geri koyalım.
            if (_snappedNozzle != null && _trackedExtinguisher != null)
            {
                if (!_trackedExtinguisher.IsEquipped)
                {
                    RestoreNozzle();
                }
                else if (_hoseDriverMono != null)
                {
                    // Nozzle elimizdeyken hortumun (spline) onu kesinlikle takip etmesini 
                    // garantilemek için her frame RefreshHose çağırıyoruz.
                    var refreshMethod = _hoseDriverMono.GetType().GetMethod("RefreshHose");
                    if (refreshMethod != null)
                    {
                        refreshMethod.Invoke(_hoseDriverMono, null);
                    }
                }
            }
        }

        void TryProcessPendingNozzleProximitySnap()
        {
            if (_pendingNozzleSnapItem == null)
                return;

            if (_snapCoroutine != null)
                return;

            ExtinguisherPickupItem item = _pendingNozzleSnapItem;

            if (!item.isActiveAndEnabled || !item.IsEquipped || item.Controller == null || !item.Controller.IsPinPulled)
            {
                _pendingNozzleSnapItem = null;
                return;
            }

            // Nozzle bu ele verilir: pim çekerken tüp tutmayan el. Bu el başka bir tüp tutmaya başladıysa bekleme iptal.
            if (myGrabber != null && myGrabber.IsHoldingExtinguisher)
            {
                _pendingNozzleSnapItem = null;
                return;
            }

            if (_snappedNozzle != null && _trackedExtinguisher == item)
            {
                _pendingNozzleSnapItem = null;
                return;
            }

            if (!TryGetHoseNozzleRoot(item, out _))
                return;

            Transform snapRef = ResolveNozzleSnapParent();
            if (snapRef == null)
                return;

            // Nozzle_low genelde spline görselinin altında kapalı — yakınlık / cast eşiği tüp köküne göre (hortum ucu snap’te yine kullanılır).
            Vector3 proximityWorld = item.transform.position;

            if (Vector3.Distance(snapRef.position, proximityWorld) > _nozzleSnapProximityRadius)
                return;

            if (!IsNozzleSnapGripActuated())
                return;

            if (_snapCoroutine != null)
            {
                StopCoroutine(_snapCoroutine);
                _snapCoroutine = null;
            }

            _snapCoroutine = StartCoroutine(SnapNozzleCoroutine(item));
        }

        static bool TryGetHoseNozzleRoot(ExtinguisherPickupItem item, out Transform nozzleRoot)
        {
            nozzleRoot = null;
            if (item == null)
                return false;

            foreach (var mono in item.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mono == null || mono.GetType().Name != "ViewmodelHoseSplineDriver")
                    continue;

                var prop = mono.GetType().GetProperty("NozzleRootTransform");
                if (prop == null)
                    continue;

                nozzleRoot = prop.GetValue(mono) as Transform;
                if (nozzleRoot != null)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Hortum snap: mesafe + (isteğe bağlı) grip basılı — <see cref="_nozzleSnapGripHold"/> yoksa <see cref="myGrabber"/>.grabInput.
        /// </summary>
        bool IsNozzleSnapGripActuated()
        {
            if (!_requireGripHoldForNozzleSnap)
                return true;

            InputActionReference gripRef = _nozzleSnapGripHold;
            if (gripRef == null && myGrabber != null)
                gripRef = myGrabber.grabInput;

            if (gripRef == null || gripRef.action == null)
            {
                if (!s_loggedMissingNozzleSnapGripAction)
                {
                    s_loggedMissingNozzleSnapGripAction = true;
                    Debug.LogWarning(
                        "[VRExtinguisherPinPuller] Hortum snap için grip gerekli ama InputAction atanmadı — " +
                        nameof(_nozzleSnapGripHold) + " veya " + nameof(myGrabber) + ".grabInput doldurun.",
                        this);
                }

                return false;
            }

            return gripRef.action.IsPressed();
        }

        private void OnPullStarted(InputAction.CallbackContext ctx)
        {
            // Eğer bu el halihazırda bir tüp tutuyorsa pim çekemez (sadece boşta olan el çekebilir)
            if (myGrabber != null && myGrabber.IsHoldingExtinguisher)
                return;

            // Önce etraftaki tüpleri bulabilmek için geniş bir tarama yapıyoruz (2 metre gibi)
            // Asıl hassas mesafe ölçümü aşağıda doğrudan pime olan uzaklıkla (pullRadius ile) yapılacak.
            Collider[] hits = Physics.OverlapSphere(transform.position, 2.0f, detectionLayerMask, QueryTriggerInteraction.Collide);

            float closestDist = float.MaxValue;
            ExtinguisherPickupItem targetItem = null;

            foreach (var hit in hits)
            {
                var item = hit.GetComponentInParent<ExtinguisherPickupItem>();
                if (item == null) continue;

                var ctrl = item.Controller;
                // Tüpün pimi zaten çekilmişse atla
                if (ctrl == null || ctrl.IsPinPulled)
                    continue;

                // Sadece DİĞER ELİMİZDE tutulan tüpün pimi çekilebilir (Yerdeki tüpün pimi çekilemez)
                if (!item.IsEquipped)
                    continue;

                // İçinde "Pin" tag'ine sahip objeyi bul
                Transform pinTransform = null;
                foreach (Transform child in item.GetComponentsInChildren<Transform>(true))
                {
                    if (child.CompareTag(pinTag))
                    {
                        pinTransform = child;
                        break;
                    }
                }

                // Eğer Pin tag'li obje yoksa güvenlik amaçlı kendi merkezini alırız
                Vector3 pinPos = pinTransform != null ? pinTransform.position : item.transform.position;

                // Mesafe ölçümü PİNİN KENDİ POZİSYONU üzerinden yapılıyor
                float dist = Vector3.Distance(transform.position, pinPos);
                if (dist <= pullRadius && dist < closestDist)
                {
                    closestDist = dist;
                    targetItem = item;
                }
            }

            if (targetItem != null)
            {
                PullPinOnItem(targetItem);
            }
        }

        private void PullPinOnItem(ExtinguisherPickupItem item)
        {
            var ctrl = item.Controller;
            if (ctrl == null) return;

            // PC'deki ile birebir aynı işlemi tetikler (ses ve animasyon otomatik oynayacak)
            if (ctrl.PullPin())
            {
                var usageState = item.UsageState;
                if (usageState != null)
                    usageState.MarkPinPulled();

                TrySpawnSlotReplacementLikePcDrop(item);

                Debug.Log($"[VRExtinguisherPinPuller] Pim başarıyla çekildi: {item.name}");

                if (_snapCoroutine != null)
                {
                    StopCoroutine(_snapCoroutine);
                    _snapCoroutine = null;
                }

                // Nozzle snap: tüpü tutmayan el nozzle'a yaklaşınca (TryProcessPendingNozzleProximitySnap).
                _pendingNozzleSnapItem = item;
            }
        }

        private System.Collections.IEnumerator SnapNozzleCoroutine(ExtinguisherPickupItem item)
        {
            yield return new WaitForEndOfFrame();

            if (!IsNozzleSnapGripActuated())
            {
                _snapCoroutine = null;
                yield break;
            }

            try
            {
                if (ApplyNozzleSnapToThisHand(item))
                    _pendingNozzleSnapItem = null;
            }
            finally
            {
                _snapCoroutine = null;
            }
        }

        bool ApplyNozzleSnapToThisHand(ExtinguisherPickupItem item)
        {
            if (item == null)
                return false;

            Transform snapParent = ResolveNozzleSnapParent();

            if (!TryGetHoseNozzleRoot(item, out Transform nozzleRoot))
                return false;

            ExtinguisherController ctrl = item.Controller;
            if (ctrl == null)
                return false;

            var allMonos = item.GetComponentsInChildren<MonoBehaviour>(true);

            _snappedNozzle = nozzleRoot;
            _originalNozzleParent = _snappedNozzle.parent;
            _trackedExtinguisher = item;

            Transform vrSprayOrigin = null;
            if (!string.IsNullOrEmpty(vrSprayDetectionTransformName))
            {
                foreach (var t in item.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == vrSprayDetectionTransformName)
                    {
                        vrSprayOrigin = t;
                        break;
                    }
                }
            }

            if (vrSprayOrigin == null)
                vrSprayOrigin = _snappedNozzle;

            foreach (var mono in allMonos)
            {
                if (mono != null && mono.GetType().Name == "ViewmodelHoseSplineDriver")
                {
                    _hoseDriverMono = mono;
                    break;
                }
            }

            _snappedNozzle.SetParent(snapParent, worldPositionStays: false);
            _snappedNozzle.localPosition = nozzleLocalPositionOffset;
            _snappedNozzle.localRotation = Quaternion.Euler(nozzleLocalEulerRotationOffset);

            Transform[] allTransforms = item.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name == "NewFireEx")
                {
                    t.SetParent(vrSprayOrigin, false);
                    t.localPosition = Vector3.zero;
                    t.localRotation = Quaternion.Euler(0f, 0f, -90f);
                    break;
                }
            }

            var allVfxMonos = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            foreach (var mono in allVfxMonos)
            {
                if (mono != null && mono.GetType().Name == "ExtinguisherSprayVFXPresenter" && mono.gameObject.scene.isLoaded)
                {
                    var setMethod = mono.GetType().GetMethod("SetVRNozzle");
                    if (setMethod != null)
                        setMethod.Invoke(mono, new object[] { vrSprayOrigin });

                    var field = mono.GetType().GetField("_nozzleTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                        field.SetValue(mono, vrSprayOrigin);
                }
            }

            ctrl.SetVRNozzle(vrSprayOrigin);

            if (ServiceLocator.TryGet<Woi.UI.VRNozzleHUDManager>(out var hudManager) && hudManager != null)
                hudManager.SetNozzle(vrSprayOrigin, ctrl);

            ctrl.SetVrHoseSplineVisualReady(true);
            return true;
        }

        static void TrySpawnSlotReplacementLikePcDrop(ExtinguisherPickupItem item)
        {
            if (item == null)
                return;

            ExtinguisherSlotController slots = null;

            if (ServiceLocator.TryGet<PlayerExtinguisherEquipment>(out var equip) && equip != null)
                slots = equip.SlotController;

            if (slots == null)
            {
                equip = UnityEngine.Object.FindFirstObjectByType<PlayerExtinguisherEquipment>();
                if (equip != null)
                    slots = equip.SlotController;
            }

            if (slots == null)
            {
                Debug.LogWarning(
                    "[VRExtinguisherPinPuller] VR pim sonrası slotta yeni tüp: PlayerExtinguisherEquipment veya SlotController bulunamadı.",
                    item);
                return;
            }

            if (!slots.TrySpawnReplacementAfterVrPinPull(item))
                Debug.LogWarning(
                    "[VRExtinguisherPinPuller] VR pim sonrası slot replacement başarısız (slot eşleşmesi veya Prefab).",
                    item);
        }

        private void RestoreNozzle()
        {
            if (_pendingNozzleSnapItem != null && _trackedExtinguisher != null && ReferenceEquals(_pendingNozzleSnapItem, _trackedExtinguisher))
                _pendingNozzleSnapItem = null;

            if (_snappedNozzle != null && _originalNozzleParent != null)
            {
                _snappedNozzle.SetParent(_originalNozzleParent, worldPositionStays: true);

                // HUD'un hedef nozzle'ını sıfırla ki gizlensin
                if (ServiceLocator.TryGet<Woi.UI.VRNozzleHUDManager>(out var hudManager) && hudManager != null)
                {
                    hudManager.SetNozzle(null);
                }

                // VFX'i orijinal PC state'ine geri döndür
                if (_trackedExtinguisher != null)
                {
                    var ctrl = _trackedExtinguisher.Controller;
                    if (ctrl != null)
                        ctrl.RestoreOriginalNozzle();

                    var allVfxMonos = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
                    foreach (var mono in allVfxMonos)
                    {
                        if (mono != null && mono.GetType().Name == "ExtinguisherSprayVFXPresenter" && mono.gameObject.scene.isLoaded)
                        {
                            var restoreMethod = mono.GetType().GetMethod("RestoreOriginalNozzle");
                            if (restoreMethod != null)
                            {
                                restoreMethod.Invoke(mono, null);
                            }
                        }
                    }
                }

                _snappedNozzle = null;
                _originalNozzleParent = null;
                _trackedExtinguisher = null;
                _hoseDriverMono = null;
            }
        }
    }
}
