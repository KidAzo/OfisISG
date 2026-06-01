using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Woi.OfficeFire
{
    /// <summary>
    /// PC: kameranın baktığı <see cref="ISelectable"/> öğesini (ör. <c>SelectableDoor</c>) etkileşim
    /// tuşuna (varsayılan E) DOĞRUDAN basıldığında seçer/açar. Hiçbir input-event'e bağlı değildir;
    /// klavyeyi doğrudan okur. Işın kamera merkezinden (crosshair) ileri atılır.
    /// VR modunda devre dışıdır (orada VrSelectableInteractor / trigger kullanılır).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PcSelectableInteractor : MonoBehaviour
    {
        [Header("Kamera (ışın kaynağı)")]
        [Tooltip("Boşsa Camera.main, o da yoksa 'Player' tag'li objedeki kamera kullanılır.")]
        [SerializeField] private Camera rayCamera;

        [SerializeField] private bool autoResolvePlayerCamera = true;
        [SerializeField] private string playerTag = "Player";

        [Header("Girdi")]
        [Tooltip("Etkileşim tuşu (varsayılan E). Doğrudan klavyeden okunur.")]
        [SerializeField] private Key interactKey = Key.E;

        [Header("Raycast")]
        [SerializeField] private float maxDistance = 5f;
        [SerializeField] private LayerMask selectionMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool drawDebugRay = true;

        [Header("Davranış")]
        [Tooltip("Açık olursa VR modunda devre dışı kalır. PC'de açılmıyorsa bunu kapatıp test et.")]
        [SerializeField] private bool disableInVrMode = true;

        private Camera _resolvedCamera;
        private bool _loggedAlive;
        private bool _loggedVrSkip;
        private bool _loggedNoKeyboard;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

        private void Update()
        {
            if (enableDebugLogs && !_loggedAlive)
            {
                _loggedAlive = true;
                Debug.Log(
                    $"[PcSelectableInteractor] Aktif. SourceInit={FirePlatformRuntime.IsSourceInitialized} IsVR={FirePlatformRuntime.IsVR} Keyboard={(Keyboard.current != null)} disableInVrMode={disableInVrMode}",
                    this);
            }

            // VR modunda bu sistem kapalı; orada VrSelectableInteractor (trigger) kullanılır.
            if (disableInVrMode && FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR)
            {
                if (enableDebugLogs && !_loggedVrSkip)
                {
                    _loggedVrSkip = true;
                    Debug.LogWarning("[PcSelectableInteractor] VR modu algılandı, E girişi devre dışı. PC modundaysan 'Disable In Vr Mode'u kapat.", this);
                }
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                if (enableDebugLogs && !_loggedNoKeyboard)
                {
                    _loggedNoKeyboard = true;
                    Debug.LogWarning("[PcSelectableInteractor] Keyboard.current NULL — Input System klavyeyi görmüyor.", this);
                }
                return;
            }

            KeyControl control = keyboard[interactKey];
            if (control != null && control.wasPressedThisFrame)
            {
                if (enableDebugLogs)
                    Debug.Log($"[PcSelectableInteractor] '{interactKey}' pressed.", this);

                TrySelectFromCamera();
            }
        }

        private void TrySelectFromCamera()
        {
            Camera cam = ResolveCamera();
            if (cam == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[PcSelectableInteractor] Kamera bulunamadı. Ray Camera ata ya da Player tag'ini kontrol et.", this);
                return;
            }

            // Crosshair (ekran merkezi) yönünde ışın.
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            int hitCount = Physics.RaycastNonAlloc(ray, HitBuffer, maxDistance, selectionMask, triggerInteraction);
            if (hitCount <= 0)
            {
                if (enableDebugLogs)
                    Debug.Log("[PcSelectableInteractor] Işın hiçbir şeye çarpmadı.", this);
                return;
            }

            SortHitsByDistance(hitCount);

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = HitBuffer[i].collider;
                if (collider == null)
                    continue;

                ISelectable selectable = FindSelectable(collider);
                if (selectable == null)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[PcSelectableInteractor] '{collider.name}' (layer={LayerMask.LayerToName(collider.gameObject.layer)}) çarptı ama ISelectable yok.", collider);
                    continue;
                }

                if (!selectable.IsSelectable)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[PcSelectableInteractor] '{collider.name}' ISelectable var ama IsSelectable=false.", collider);
                    continue;
                }

                if (enableDebugLogs)
                    Debug.Log($"[PcSelectableInteractor] Seçildi: '{collider.name}'.", collider);

                selectable.Select(new SelectionContext(SelectionSource.PC, cam.transform, ray, HitBuffer[i]));
                return;
            }

            if (enableDebugLogs)
                Debug.Log("[PcSelectableInteractor] Işın hattında seçilebilir ISelectable bulunamadı.", this);
        }

        private Camera ResolveCamera()
        {
            if (rayCamera != null)
            {
                _resolvedCamera = rayCamera;
                return rayCamera;
            }

            if (_resolvedCamera != null && _resolvedCamera.isActiveAndEnabled)
                return _resolvedCamera;

            if (Camera.main != null)
            {
                _resolvedCamera = Camera.main;
                return _resolvedCamera;
            }

            if (!autoResolvePlayerCamera)
                return null;

            if (!string.IsNullOrEmpty(playerTag))
            {
                GameObject player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null)
                {
                    Camera playerCamera = player.GetComponentInChildren<Camera>(true);
                    if (playerCamera != null)
                    {
                        _resolvedCamera = playerCamera;
                        return _resolvedCamera;
                    }
                }
            }

            return null;
        }

        private static void SortHitsByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit key = HitBuffer[i];
                int j = i - 1;
                while (j >= 0 && HitBuffer[j].distance > key.distance)
                {
                    HitBuffer[j + 1] = HitBuffer[j];
                    j--;
                }

                HitBuffer[j + 1] = key;
            }
        }

        private static ISelectable FindSelectable(Collider collider)
        {
            ISelectable selectable = collider.GetComponentInParent<ISelectable>();
            if (selectable != null)
                return selectable;

            return collider.GetComponentInChildren<ISelectable>();
        }

        private void OnDrawGizmos()
        {
            if (!drawDebugRay)
                return;

            Camera cam = rayCamera != null ? rayCamera : Camera.main;
            if (cam == null)
                return;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(ray.origin, ray.direction * maxDistance);
        }
    }
}
