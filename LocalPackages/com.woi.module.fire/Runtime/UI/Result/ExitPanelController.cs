using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Unity.XR.CoreUtils;
using WOI.Modules.SDK;
using Woi.Player;
using Woi.Game.Training.UI;
using Woi.UI.Popups.Localization;
using Woi.Training;

namespace Woi.UI.Result
{
    /// <summary>
    /// VR: açma girdisi (ör. sağ grip) panel kapalıyken açar; <b>açıkken tekrar basılınca kapatır</b> (Hayır düğmesi ile aynı).
    /// Evet: önce eğitim oturumu biter (sonuç ekranı), ardından XR Origin hedefe taşınır, Locomotion + Teleportation kapatılır, <see cref="_onExitConfirmed"/> çağrılır.
    /// Panel açıkken baş kameranın <see cref="_distanceInFrontOfCamera"/> m önünde tutulur.
    /// </summary>
    [AddComponentMenu("Woi/UI/Exit Panel Controller")]
    [RequireComponent(typeof(UIDocument))]
    public sealed class ExitPanelController : MonoBehaviour
    {
        const string MainContainerName = "MainContainer";
        const string BtnYesName = "BtnYes";
        const string BtnNoName = "BtnNo";

        [Header("UI")]
        [SerializeField]
        UIDocument _document;

        [Tooltip("Boşsa aynı GameObject’teki UIDocument kullanılır.")]
        [SerializeField]
        bool _startHidden = true;

        [Header("VR — paneli aç / kapat")]
        [Tooltip("Kapalıyken Started = panel açılır; panel zaten açıkken aynı girdi = panel kapanır (Hayır ile aynı).")]
        [SerializeField]
        InputActionReference _openPanelAction;

        [Header("VR — konum (XR Origin göz kamerası)")]
        [Tooltip("Panel açıkken baş kamerasının ileri ekseninde ne kadar ileride dursun (metre).")]
        [SerializeField, Min(0.05f)]
        float _distanceInFrontOfCamera = 1.15f;

        [Tooltip("Boşsa IXRPlayerService → PlayerCamera transform, yoksa MainCamera. İsterseniz doğrudan XR Origin Center Eye transform.")]
        [SerializeField]
        Transform _cameraOverride;

        [Tooltip("Kapalıyken transform güncellenmez (son konumda kalır).")]
        [SerializeField]
        bool _followCameraOnlyWhenOpen = true;

        [Tooltip("LookRotation sonrası yerel Y ekseninde ek açı (derece). Panel kameraya göre 180° ters görünüyorsa 180 bırakın; zaten doğruysa 0 yapın.")]
        [SerializeField]
        float _billboardYawOffsetDegrees = 180f;

        [Header("Exit — EVET (XR rig)")]
        [Tooltip("EVET sonrası XR Origin bu hedefe Unity XROrigin API ile taşınır; ardından Locomotion / Teleportation kapatılır. Pivot genelde zemindedir.")]
        [SerializeField]
        Transform _xrRigTeleportTarget;

        [Tooltip("Hedef transformun yerel uzayında kamera (HMD) hedef konumu. Pivot zemindeyse varsayılan (0, ~1.6, 0); hedef zaten göz hizasıysa (0,0,0).")]
        [SerializeField]
        Vector3 _teleportCameraLocalOffsetFromTarget = new Vector3(0f, 1.6f, 0f);

        [Tooltip("Açıksa Y offset yerine, ışınlanmadan önce ölçülen kamera–origin yüksekliği (dünya up) kullanılır; X/Y/Z yerel offset yine uygulanır.")]
        [SerializeField]
        bool _teleportUseMeasuredCameraHeight = true;

        [Tooltip("XR Interaction Toolkit: Locomotion / Teleportation dışında kapatılacak ek kökler (isteğe bağlı).")]
        [SerializeField]
        Transform[] _extraLocomotionRootsToDisable;

        [Header("Events")]
        [Tooltip("EVET — ek sahne mantığı (teleport + locomotion kapatma sonrası).")]
        [SerializeField]
        UnityEvent _onExitConfirmed = new UnityEvent();

        [Header("Training — VR EVET")]
        [Tooltip("Boşsa sahnede aranır. EVET: oturum biter ve TrainingResultScreenSessionBinder sonuç ekranını doldurur.")]
        [SerializeField]
        LevelController _levelController;

        [Tooltip("Çıkış paneli açılınca yanlışlıkla görünen sonuç UI köklerini kapat (grip ile XR FinishedGame çakışması vb.).")]
        [SerializeField]
        TrainingResultScreenSessionBinder _resultScreenSessionBinder;

        VisualElement _mainContainer;
        Button _btnYes;
        Button _btnNo;
        bool _isOpen;
        Coroutine _bindRoutine;

        void Reset()
        {
            _document = GetComponent<UIDocument>();
        }

        void Awake()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();

            if (GetComponent<ExitPanelNearFarUiBootstrap>() == null)
                gameObject.AddComponent<ExitPanelNearFarUiBootstrap>();
        }

        void OnEnable()
        {
            if (_openPanelAction != null && _openPanelAction.action != null)
            {
                _openPanelAction.action.Enable();
                _openPanelAction.action.started += OnOpenActionStarted;
            }

            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }

            _bindRoutine = StartCoroutine(BindUiWhenReady());
        }

        void OnDisable()
        {
            if (_openPanelAction != null && _openPanelAction.action != null)
            {
                _openPanelAction.action.started -= OnOpenActionStarted;
                _openPanelAction.action.Disable();
            }

            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }

            UnregisterButtons();
        }

        IEnumerator BindUiWhenReady()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();

            int safety = 120;
            while (safety-- > 0 && enabled && _document != null && _document.rootVisualElement == null)
                yield return null;

            if (!enabled || _document == null)
                yield break;

            VisualElement root = _document.rootVisualElement;
            if (root == null)
                yield break;

            _mainContainer = root.Q<VisualElement>(MainContainerName) ?? root;
            _btnYes = root.Q<Button>(BtnYesName);
            _btnNo = root.Q<Button>(BtnNoName);

            if (_btnYes != null)
                _btnYes.clicked += OnYesClicked;
            if (_btnNo != null)
                _btnNo.clicked += OnNoClicked;

            if (_startHidden)
                SetPanelVisible(false);
            else
                SetPanelVisible(true);

            ApplyWorldDocumentPivotIfNeeded();

            ApplyExitPanelLocalizedText(root);

            _bindRoutine = null;
        }

        static void ApplyExitPanelLocalizedText(VisualElement root)
        {
            if (root == null)
                return;

            Label attention = root.Q<Label>("exit-lbl-attention");
            if (attention != null)
                attention.text = LocalizedUiPair.Resolve("ATTENTION", "DİKKAT");

            Label subtitle = root.Q<Label>("exit-lbl-subtitle");
            if (subtitle != null)
                subtitle.text = LocalizedUiPair.Resolve(
                    "SIMULATION EXIT REQUESTED",
                    "SİMÜLASYON İPTALİ İSTENİYOR");

            Label bodyTitle = root.Q<Label>("exit-lbl-body-title");
            if (bodyTitle != null)
                bodyTitle.text = LocalizedUiPair.Resolve("EXIT GAME", "OYUNDAN ÇIK");

            Label bodyDesc = root.Q<Label>("exit-lbl-body-desc");
            if (bodyDesc != null)
                bodyDesc.text = LocalizedUiPair.Resolve(
                    "Your current progress will be saved. Are you sure you want to leave the simulation?",
                    "Mevcut ilerlemeniz kaydedilecek. Simülasyondan ayrılmak istediğinize emin misiniz?");

            Button yes = root.Q<Button>(BtnYesName);
            if (yes != null)
                yes.text = LocalizedUiPair.Resolve("YES", "EVET");

            Button no = root.Q<Button>(BtnNoName);
            if (no != null)
                no.text = LocalizedUiPair.Resolve("NO", "HAYIR");
        }

        /// <summary>
        /// UIDocument zaten World Space Panel Settings ile kurulduysa merkez pivot + dinamik boyut (önünde hizalama için).
        /// </summary>
        void ApplyWorldDocumentPivotIfNeeded()
        {
            if (_document == null || _document.panelSettings == null)
                return;

            if (_document.panelSettings.renderMode != PanelRenderMode.WorldSpace)
                return;

            _document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Dynamic;
            _document.pivot = Pivot.Center;
            _document.pivotReferenceSize = PivotReferenceSize.BoundingBox;
        }

        void UnregisterButtons()
        {
            if (_btnYes != null)
                _btnYes.clicked -= OnYesClicked;
            if (_btnNo != null)
                _btnNo.clicked -= OnNoClicked;

            _btnYes = null;
            _btnNo = null;
            _mainContainer = null;
        }

        void OnOpenActionStarted(InputAction.CallbackContext _)
        {
            if (_isOpen)
                SetPanelVisible(false);
            else
                SetPanelVisible(true);
        }

        void OnNoClicked()
        {
            SetPanelVisible(false);
        }

        void OnYesClicked()
        {
            RequestTrainingSessionEndForVrYes();

            if (!TryResolveXrOrigin(out XROrigin xrOrigin))
            {
                Debug.LogWarning(
                    $"[{nameof(ExitPanelController)}] EVET: XROrigin bulunamadı (IXRPlayerService.PlayerTransform veya sahne XROrigin).",
                    this);
            }
            else
            {
                if (_xrRigTeleportTarget != null)
                {
                    TeleportXrOriginToTarget(xrOrigin, _xrRigTeleportTarget);
                    DisableXrLocomotionAndTeleportation(xrOrigin.transform);
                }
                else
                {
                    Debug.LogWarning(
                        $"[{nameof(ExitPanelController)}] EVET: '{nameof(_xrRigTeleportTarget)}' atanmamış — ışınlama ve locomotion kapatma atlandı.",
                        this);
                }
            }

            _onExitConfirmed?.Invoke();
            SetPanelVisible(false);
        }

        void RequestTrainingSessionEndForVrYes()
        {
            LevelController lc = _levelController;
            if (lc == null)
                lc = FindFirstObjectByType<LevelController>();

            if (lc == null)
            {
                Debug.LogWarning(
                    $"[{nameof(ExitPanelController)}] EVET: {nameof(LevelController)} bulunamadı — oturum sonlandırılamadı. Inspector’dan atayın.",
                    this);
                return;
            }

            lc.RequestEndSessionFromExitPanel();
        }

        static bool TryResolveXrOrigin(out XROrigin xrOrigin)
        {
            xrOrigin = null;

            if (ServiceLocator.TryGet<IXRPlayerService>(out var xr) && xr != null && xr.PlayerTransform != null)
            {
                xrOrigin = xr.PlayerTransform.GetComponent<XROrigin>()
                           ?? xr.PlayerTransform.GetComponentInParent<XROrigin>()
                           ?? xr.PlayerTransform.GetComponentInChildren<XROrigin>(true);
            }

            if (xrOrigin == null)
            {
                Transform t = TryFindXrOriginTransformInLoadedScenes();
                if (t != null)
                    xrOrigin = t.GetComponent<XROrigin>();
            }

            return xrOrigin != null;
        }

        static Transform TryFindXrOriginTransformInLoadedScenes()
        {
            XROrigin[] origins = UnityEngine.Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (origins == null || origins.Length == 0)
                return null;

            foreach (XROrigin o in origins)
            {
                if (o == null)
                    continue;
                GameObject go = o.gameObject;
                if (!go.scene.IsValid() || !go.scene.isLoaded)
                    continue;
                return o.transform;
            }

            return null;
        }

        /// <summary>
        /// Unity <see cref="XROrigin.MatchOriginUpCameraForward"/> + <see cref="XROrigin.MoveCameraToWorldLocation"/> ile taşır
        /// (kamera offset’i yüzünden kökü doğrudan SetPosition ile taşımak havada/yanlış konuma yol açar).
        /// </summary>
        void TeleportXrOriginToTarget(XROrigin origin, Transform target)
        {
            if (origin == null || target == null)
                return;

            Transform rig = origin.transform;
            CharacterController[] controllers = rig.GetComponentsInChildren<CharacterController>(true);
            var ccWasEnabled = new bool[controllers.Length];
            for (int i = 0; i < controllers.Length; i++)
            {
                CharacterController cc = controllers[i];
                if (cc == null)
                    continue;
                ccWasEnabled[i] = cc.enabled;
                cc.enabled = false;
            }

            Physics.SyncTransforms();

            Vector3 up = target.up.sqrMagnitude > 1e-6f ? target.up.normalized : Vector3.up;
            Vector3 fwd = target.forward.sqrMagnitude > 1e-6f ? target.forward.normalized : rig.forward;
            fwd = Vector3.ProjectOnPlane(fwd, up);
            if (fwd.sqrMagnitude < 1e-6f)
                fwd = Vector3.ProjectOnPlane(origin.Camera != null ? origin.Camera.transform.forward : rig.forward, up);
            if (fwd.sqrMagnitude < 1e-6f)
                fwd = Vector3.forward;
            fwd.Normalize();

            origin.MatchOriginUpCameraForward(up, fwd);

            Vector3 desiredCameraWorld;
            if (_teleportUseMeasuredCameraHeight && origin.Camera != null)
            {
                float measured = Vector3.Dot(origin.Camera.transform.position - rig.position, up);
                measured = Mathf.Max(measured, 0.05f);
                Vector3 planar = target.TransformDirection(
                    new Vector3(_teleportCameraLocalOffsetFromTarget.x, 0f, _teleportCameraLocalOffsetFromTarget.z));
                desiredCameraWorld = target.position + up * measured + planar;
            }
            else
                desiredCameraWorld = target.TransformPoint(_teleportCameraLocalOffsetFromTarget);
            if (origin.Camera != null)
                origin.MoveCameraToWorldLocation(desiredCameraWorld);
            else
                rig.SetPositionAndRotation(target.position, Quaternion.LookRotation(fwd, up));

            Physics.SyncTransforms();

            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null && ccWasEnabled[i])
                    controllers[i].enabled = true;
            }
        }

        void DisableXrLocomotionAndTeleportation(Transform rig)
        {
            if (rig == null)
                return;

            DisableLocomotionOrTeleportationObjectsUnderRig(rig);

            if (_extraLocomotionRootsToDisable != null)
            {
                for (int i = 0; i < _extraLocomotionRootsToDisable.Length; i++)
                {
                    Transform t = _extraLocomotionRootsToDisable[i];
                    if (t != null)
                        t.gameObject.SetActive(false);
                }
            }

            DisableXriLocomotionProviders(rig);
            DisableLocomotionSystemUnderRig(rig);
        }

        /// <summary>
        /// XR Origin altında adı Locomotion / Teleportation olan tüm kök objeleri kapatır (Starter Assets isimleri, büyük/küçük harf duyarsız).
        /// </summary>
        static void DisableLocomotionOrTeleportationObjectsUnderRig(Transform rig)
        {
            foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (t == null)
                    continue;

                if (string.Equals(t.name, "Locomotion", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.name, "Teleportation", StringComparison.OrdinalIgnoreCase))
                    t.gameObject.SetActive(false);
            }
        }

        static void DisableLocomotionSystemUnderRig(Transform rig)
        {
            foreach (MonoBehaviour mb in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null)
                    continue;

                Type ty = mb.GetType();
                if (ty.Name == "LocomotionSystem" &&
                    ty.Namespace != null &&
                    ty.Namespace.StartsWith("UnityEngine.XR.Interaction.Toolkit.Locomotion", StringComparison.Ordinal))
                    mb.enabled = false;
            }
        }

        /// <summary>
        /// XRI locomotion provider’ları (assembly referansı olmadan tam ad ile).
        /// </summary>
        static void DisableXriLocomotionProviders(Transform rig)
        {
            foreach (MonoBehaviour mb in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.enabled)
                    continue;

                string n = mb.GetType().FullName;
                if (string.IsNullOrEmpty(n))
                    continue;

                if (n == "UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets.ControllerInputActionManager")
                {
                    mb.enabled = false;
                    continue;
                }

                if (!n.StartsWith("UnityEngine.XR.Interaction.Toolkit.Locomotion.", StringComparison.Ordinal))
                    continue;

                if (n.Contains("Teleportation", StringComparison.Ordinal) ||
                    n.Contains("MoveProvider", StringComparison.Ordinal) ||
                    (n.Contains("Turn", StringComparison.Ordinal) && n.Contains("Provider", StringComparison.Ordinal)))
                {
                    mb.enabled = false;
                }
            }
        }

        void LateUpdate()
        {
            if (_followCameraOnlyWhenOpen && !_isOpen)
                return;

            Transform eye = ResolveFollowEye();
            if (eye == null)
                return;

            Transform t = transform;
            Vector3 pos = eye.position + eye.forward * _distanceInFrontOfCamera;
            t.position = pos;

            Vector3 toEye = eye.position - pos;
            if (toEye.sqrMagnitude > 1e-6f)
            {
                Quaternion look = Quaternion.LookRotation(toEye, eye.up);
                if (Mathf.Abs(_billboardYawOffsetDegrees) > 1e-3f)
                    look *= Quaternion.Euler(0f, _billboardYawOffsetDegrees, 0f);
                t.rotation = look;
            }
        }

        Transform ResolveFollowEye()
        {
            if (_cameraOverride != null)
                return _cameraOverride;

            if (ServiceLocator.TryGet<IXRPlayerService>(out var xr) && xr != null && xr.PlayerCamera != null &&
                xr.PlayerCamera.isActiveAndEnabled)
                return xr.PlayerCamera.transform;

            return Camera.main != null ? Camera.main.transform : null;
        }

        void SetPanelVisible(bool visible)
        {
            if (_mainContainer == null && _document != null && _document.rootVisualElement != null)
                _mainContainer = _document.rootVisualElement.Q<VisualElement>(MainContainerName)
                    ?? _document.rootVisualElement;

            if (_mainContainer == null)
                return;

            _mainContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _isOpen = visible;

            if (visible)
            {
                HideTrainingResultsWhileExitDialogOpen();
                if (_document != null && _document.rootVisualElement != null)
                    ApplyExitPanelLocalizedText(_document.rootVisualElement);
            }
        }

        void HideTrainingResultsWhileExitDialogOpen()
        {
            TrainingResultScreenSessionBinder binder = _resultScreenSessionBinder;
            if (binder == null)
                binder = FindFirstObjectByType<TrainingResultScreenSessionBinder>();

            binder?.HideResults();
        }
    }
}
