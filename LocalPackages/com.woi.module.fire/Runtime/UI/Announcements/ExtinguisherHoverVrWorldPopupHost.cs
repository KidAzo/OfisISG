using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using WOI.Modules.SDK;
using Woi.Player;

namespace Woi.UI.Announcements
{
    /// <summary>Eğitim SOAP world kartları: USS veya kırmızı (yanlış tüp) / yeşil (yangın söndü) metin renkleri.</summary>
    public enum VrWorldTrainingCardTone
    {
        UseStyleSheet = 0,
        TrainingSoapError = 1,
        TrainingSoapSuccess = 2,
    }

    /// <summary>
    /// World-space UI Toolkit hover card for VR (same UXML as <see cref="Woi.UI.Popups.PopupService"/>).
    /// Place once under the VR rig; <see cref="ExtinguisherHoverController"/> uses hit placement; yangın proximity <see cref="ShowAt"/> ile alevi üstünde dünya-y ekseni normali + yaw billboard.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExtinguisherHoverVrWorldPopupHost : MonoBehaviour
    {
        [SerializeField]
        private VisualTreeAsset popupUxml;

        [Tooltip("Cloned at runtime; render mode is switched to World Space. Use your project Panel Settings (e.g. UI Toolkit/PanelSettings). Unity 6+ does not use worldSpaceCamera on PanelSettings — sorting uses distance to the camera.")]
        [SerializeField]
        private PanelSettings cloneSourcePanelSettings;

        [Tooltip("Small push along the mesh normal so the card does not Z-fight with the tube.")]
        [SerializeField]
        [FormerlySerializedAs("offsetAlongHitNormalMeters")]
        private float surfaceSeparationAlongNormalMeters = 0.03f;

        [Tooltip("Extra offset toward the HMD. Side-hit normals alone often hide the card inside or beside the tube.")]
        [SerializeField]
        private float offsetTowardCameraMeters = 0.28f;

        [Tooltip("Optional: assign the XR / center-eye camera. In VR, Camera.main is often null — offset + billboard then fail unless this is set or another camera is found.")]
        [SerializeField]
        private Camera billboardCameraOverride;

        [Tooltip("When true (default), billboard rotation adds 180° on Y so UI Toolkit text faces the camera correctly (fixes mirrored / reversed text in world space). Turn off if your panel already reads correctly.")]
        [SerializeField]
        private bool billboardFlipY180 = true;

        [Tooltip(
            "World ShowAt (yangın / tüp): yalnızca Y ekseni etrafında kameraya dön — panel dünya dikeyinde kalır (yatay ‘şerit’ hissi azalır). " +
            "Kapalıyken tam LookRotation (tüp yüzeyine göre hafif eğim mümkün).")]
        [SerializeField]
        private bool billboardYawOnlyAroundWorldUp = true;

        [Header("World card — global offset")]
        [Tooltip("Tüm world kartları (ShowAt, head-locked) kök transforma dünya uzayında eklenir. Y ile kartı genel olarak yukarı/aşağı kaydırın.")]
        [SerializeField]
        private Vector3 worldCardExtraWorldOffset;

        [Header("World card — training SOAP metin renkleri")]
        [Tooltip("Yanlış tüp (TrainingSoapError) başlık rengi.")]
        [SerializeField]
        private Color trainingSoapErrorTitle = new Color(1f, 0.45f, 0.4f, 1f);

        [Tooltip("Yanlış tüp mesaj rengi.")]
        [SerializeField]
        private Color trainingSoapErrorMessage = new Color(1f, 0.72f, 0.68f, 1f);

        [Tooltip("Yangın söndü (TrainingSoapSuccess) başlık rengi.")]
        [SerializeField]
        private Color trainingSoapSuccessTitle = new Color(0.45f, 0.95f, 0.55f, 1f);

        [Tooltip("Yangın söndü mesaj rengi.")]
        [SerializeField]
        private Color trainingSoapSuccessMessage = new Color(0.75f, 0.98f, 0.8f, 1f);

        [Tooltip("Uniform world scale for the UI Toolkit panel (meters per layout unit). Increase if the card is too small in headset.")]
        [SerializeField]
        private float worldDocumentScale = 0.0016f;

        [Header("Head-locked VR (fire proximity etc.)")]
        [Tooltip("When <see cref=\"ShowHeadLocked\"/>, panel position each frame in <b>camera local space</b> (meters). Forward Z is toward scene in front of the HMD.")]
        [SerializeField]
        private Vector3 headLockedLocalOffset = new Vector3(0f, -0.08f, 0.55f);

        [Header("World-space UIDocument (Unity 6)")]
        [Tooltip(
            "When on, document size follows the root layout (content-sized). Requires compact VR layout (see ApplyVrWorldVisualLayout). "
            + "When off, uses Fixed + Fixed World Panel Size below.")]
        [SerializeField]
        private bool worldSpaceUseDynamicSize = true;

        [Tooltip("Only applied when World Space Use Dynamic Size is off (Fixed mode).")]
        [SerializeField]
        private Vector2 fixedWorldPanelSize = new Vector2(400f, 200f);

        UIDocument _document;
        PanelSettings _runtimePanelSettings;
        VisualElement _root;
        VisualElement _backdrop;
        Label _titleLabel;
        Label _messageLabel;
        Button _closeButton;
        bool _bound;
        bool _visible;
        Coroutine _pendingShowRoutine;
        PendingShowKind _pendingKind;
        Vector3 _pendingHitPoint;
        Vector3 _pendingHitNormal;
        string _pendingTitle;
        string _pendingMessage;
        float _pendingSeparationOverride = float.NaN;
        float _pendingTowardCameraOverride = float.NaN;
        float _pendingWorldDocumentScaleMultiplier = float.NaN;
        VrWorldTrainingCardTone _pendingCardTone = VrWorldTrainingCardTone.UseStyleSheet;
        bool _followHeadAnchor;

        enum PendingShowKind
        {
            None,
            AtHit,
            HeadLocked
        }

        static bool s_warnedNoBillboardCamera;

        public static ExtinguisherHoverVrWorldPopupHost FindInstance() =>
            FindFirstObjectByType<ExtinguisherHoverVrWorldPopupHost>();

        public static bool TryGetInstance(out ExtinguisherHoverVrWorldPopupHost host)
        {
            host = FindInstance();
            return host != null;
        }

        void Awake()
        {
            EnsureUi();
        }

        void OnEnable()
        {
            EnsureUi();
        }

        void OnDisable()
        {
            _bound = false;
            _root = null;
            _backdrop = null;
            _titleLabel = null;
            _messageLabel = null;
            _closeButton = null;
        }

        void LateUpdate()
        {
            if (!_visible || _document == null)
                return;

            Camera cam = ResolveBillboardCamera();
            if (cam == null)
                return;

            if (_followHeadAnchor)
                transform.position = cam.transform.TransformPoint(headLockedLocalOffset) + worldCardExtraWorldOffset;

            Vector3 toCam = cam.transform.position - transform.position;
            if (toCam.sqrMagnitude < 1e-6f)
                return;

            Quaternion face;
            if (!_followHeadAnchor && billboardYawOnlyAroundWorldUp)
            {
                Vector3 flat = toCam;
                flat.y = 0f;
                if (flat.sqrMagnitude < 1e-8f)
                {
                    flat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
                    if (flat.sqrMagnitude < 1e-8f)
                        flat = Vector3.forward;
                }

                face = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }
            else
                face = Quaternion.LookRotation(toCam.normalized, Vector3.up);

            if (billboardFlipY180)
                face *= Quaternion.Euler(0f, 180f, 0f);
            transform.rotation = face;
        }

        void OnDestroy()
        {
            if (_pendingShowRoutine != null)
            {
                StopCoroutine(_pendingShowRoutine);
                _pendingShowRoutine = null;
            }

            if (_runtimePanelSettings != null)
                Destroy(_runtimePanelSettings);
        }

        /// <summary>
        /// World-space card in front of the HMD (camera local offset). İsteğe bağlı; yangın proximity <see cref="ShowAt"/> kullanır.
        /// </summary>
        public void ShowHeadLocked(string title, string message)
        {
            if (!EnsureUi())
                return;

            ConfigureWorldSpaceUidocument();

            if (!TryBind())
            {
                if (_pendingShowRoutine != null)
                {
                    StopCoroutine(_pendingShowRoutine);
                    _pendingShowRoutine = null;
                }

                _pendingKind = PendingShowKind.HeadLocked;
                _pendingTitle = title;
                _pendingMessage = message;
                _pendingSeparationOverride = float.NaN;
                _pendingTowardCameraOverride = float.NaN;
                _pendingWorldDocumentScaleMultiplier = float.NaN;
                _pendingCardTone = VrWorldTrainingCardTone.UseStyleSheet;
                _pendingShowRoutine = StartCoroutine(ShowPendingWhenRootReady());
                return;
            }

            ApplyShowHeadLocked(title, message);
        }

        public void ShowAt(Vector3 worldHitPoint, Vector3 worldHitNormal, string title, string message)
        {
            EnqueueOrApplyShowAt(
                worldHitPoint,
                worldHitNormal,
                title,
                message,
                float.NaN,
                float.NaN,
                VrWorldTrainingCardTone.UseStyleSheet,
                float.NaN);
        }

        /// <summary>
        /// <see cref="ShowAt"/> with per-call placement (ör. yangın proximity: yüzeyden uzaklık + kameraya ek itiş).
        /// <paramref name="surfaceSeparationMeters"/> veya <paramref name="towardCameraMeters"/> için <see cref="float.NaN"/> = Inspector varsayılanı.
        /// <paramref name="worldDocumentScaleMultiplier"/> için <see cref="float.NaN"/> = yalnızca Inspector <c>worldDocumentScale</c>;
        /// aksi halde <c>worldDocumentScale * multiplier</c> (ör. yangın world popup anchor transformunun lossyScale’i).
        /// </summary>
        public void ShowAt(
            Vector3 worldHitPoint,
            Vector3 worldHitNormal,
            string title,
            string message,
            float surfaceSeparationMeters,
            float towardCameraMeters,
            VrWorldTrainingCardTone cardTone = VrWorldTrainingCardTone.UseStyleSheet,
            float worldDocumentScaleMultiplier = float.NaN)
        {
            EnqueueOrApplyShowAt(
                worldHitPoint,
                worldHitNormal,
                title,
                message,
                surfaceSeparationMeters,
                towardCameraMeters,
                cardTone,
                worldDocumentScaleMultiplier);
        }

        void EnqueueOrApplyShowAt(
            Vector3 worldHitPoint,
            Vector3 worldHitNormal,
            string title,
            string message,
            float surfaceSeparationMeters,
            float towardCameraMeters,
            VrWorldTrainingCardTone cardTone,
            float worldDocumentScaleMultiplier)
        {
            if (!EnsureUi())
                return;

            ConfigureWorldSpaceUidocument();

            if (!TryBind())
            {
                if (_pendingShowRoutine != null)
                {
                    StopCoroutine(_pendingShowRoutine);
                    _pendingShowRoutine = null;
                }

                _pendingKind = PendingShowKind.AtHit;
                _pendingHitPoint = worldHitPoint;
                _pendingHitNormal = worldHitNormal;
                _pendingTitle = title;
                _pendingMessage = message;
                _pendingSeparationOverride = surfaceSeparationMeters;
                _pendingTowardCameraOverride = towardCameraMeters;
                _pendingWorldDocumentScaleMultiplier = worldDocumentScaleMultiplier;
                _pendingCardTone = cardTone;
                _pendingShowRoutine = StartCoroutine(ShowPendingWhenRootReady());
                return;
            }

            ApplyShow(
                worldHitPoint,
                worldHitNormal,
                title,
                message,
                surfaceSeparationMeters,
                towardCameraMeters,
                cardTone,
                worldDocumentScaleMultiplier);
        }

        IEnumerator ShowPendingWhenRootReady()
        {
            const int maxFrames = 8;
            for (int i = 0; i < maxFrames && !TryBind(); i++)
                yield return null;

            _pendingShowRoutine = null;

            if (!_bound)
            {
                Debug.LogWarning(
                    "[ExtinguisherHoverVrWorldPopupHost] UIDocument root was not ready or UXML missing 'popup-service-root' — hover card not shown.",
                    this);
                yield break;
            }

            switch (_pendingKind)
            {
                case PendingShowKind.AtHit:
                    ApplyShow(
                        _pendingHitPoint,
                        _pendingHitNormal,
                        _pendingTitle,
                        _pendingMessage,
                        _pendingSeparationOverride,
                        _pendingTowardCameraOverride,
                        _pendingCardTone,
                        _pendingWorldDocumentScaleMultiplier);
                    break;
                case PendingShowKind.HeadLocked:
                    ApplyShowHeadLocked(_pendingTitle, _pendingMessage);
                    break;
            }

            _pendingKind = PendingShowKind.None;
        }

        void ApplyShow(
            Vector3 worldHitPoint,
            Vector3 worldHitNormal,
            string title,
            string message,
            float separationOverrideMeters = float.NaN,
            float towardCameraOverrideMeters = float.NaN,
            VrWorldTrainingCardTone cardTone = VrWorldTrainingCardTone.UseStyleSheet,
            float worldDocumentScaleMultiplier = float.NaN)
        {
            _followHeadAnchor = false;

            ApplyVrWorldVisualLayout();

            if (_titleLabel != null)
                _titleLabel.text = title ?? string.Empty;
            if (_messageLabel != null)
                _messageLabel.text = message ?? string.Empty;

            Vector3 n = worldHitNormal.sqrMagnitude > 1e-6f ? worldHitNormal.normalized : Vector3.up;
            float sep = float.IsNaN(separationOverrideMeters)
                ? Mathf.Clamp(surfaceSeparationAlongNormalMeters, 0f, 0.35f)
                : Mathf.Clamp(separationOverrideMeters, 0f, 2.5f);
            float towardCam = float.IsNaN(towardCameraOverrideMeters)
                ? Mathf.Clamp(offsetTowardCameraMeters, 0f, 1.5f)
                : Mathf.Clamp(towardCameraOverrideMeters, 0f, 2.5f);
            Vector3 pos = worldHitPoint + n * sep;

            Camera cam = ResolveBillboardCamera();
            if (cam != null && towardCam > 0f)
            {
                Vector3 toCam = cam.transform.position - pos;
                if (toCam.sqrMagnitude > 1e-8f)
                    pos += toCam.normalized * towardCam;
            }
            else if (towardCam > 0f && !s_warnedNoBillboardCamera)
            {
                s_warnedNoBillboardCamera = true;
                Debug.LogWarning(
                    "[ExtinguisherHoverVrWorldPopupHost] No camera for VR hover offset (Camera.main null, override unset). "
                    + "Assign Billboard Camera Override to your XR center-eye camera, or tag that camera MainCamera.",
                    this);
            }

            transform.position = pos + worldCardExtraWorldOffset;

            ApplyWorldDocumentRootScale(worldDocumentScaleMultiplier);

            ApplyTrainingCardTone(cardTone);

            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
                _root.MarkDirtyRepaint();
            }

            _document?.rootVisualElement?.MarkDirtyRepaint();

            _visible = true;
        }

        void ApplyShowHeadLocked(string title, string message)
        {
            ApplyVrWorldVisualLayout();

            if (_titleLabel != null)
                _titleLabel.text = title ?? string.Empty;
            if (_messageLabel != null)
                _messageLabel.text = message ?? string.Empty;

            ApplyWorldDocumentRootScale(float.NaN);

            _followHeadAnchor = true;

            Camera cam = ResolveBillboardCamera();
            if (cam != null)
                transform.position = cam.transform.TransformPoint(headLockedLocalOffset) + worldCardExtraWorldOffset;
            else if (!s_warnedNoBillboardCamera)
            {
                s_warnedNoBillboardCamera = true;
                Debug.LogWarning(
                    "[ExtinguisherHoverVrWorldPopupHost] ShowHeadLocked: no camera — assign Billboard Camera Override (XR center eye) or register IXRPlayerService.",
                    this);
            }

            ApplyTrainingCardTone(VrWorldTrainingCardTone.UseStyleSheet);

            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
                _root.MarkDirtyRepaint();
            }

            _document?.rootVisualElement?.MarkDirtyRepaint();

            _visible = true;
        }

        void ApplyTrainingCardTone(VrWorldTrainingCardTone tone)
        {
            switch (tone)
            {
                case VrWorldTrainingCardTone.UseStyleSheet:
                    RevertLabelColorsInternal();
                    return;
                case VrWorldTrainingCardTone.TrainingSoapError:
                    if (_titleLabel != null)
                        _titleLabel.style.color = new StyleColor(trainingSoapErrorTitle);
                    if (_messageLabel != null)
                        _messageLabel.style.color = new StyleColor(trainingSoapErrorMessage);
                    return;
                case VrWorldTrainingCardTone.TrainingSoapSuccess:
                    if (_titleLabel != null)
                        _titleLabel.style.color = new StyleColor(trainingSoapSuccessTitle);
                    if (_messageLabel != null)
                        _messageLabel.style.color = new StyleColor(trainingSoapSuccessMessage);
                    return;
                default:
                    RevertLabelColorsInternal();
                    return;
            }
        }

        static void RevertLabelColorsInternal(Label title, Label message)
        {
            if (title != null)
                title.style.color = StyleKeyword.Null;
            if (message != null)
                message.style.color = StyleKeyword.Null;
        }

        void RevertLabelColorsInternal() =>
            RevertLabelColorsInternal(_titleLabel, _messageLabel);

        public void Hide()
        {
            _visible = false;
            _followHeadAnchor = false;

            ApplyWorldDocumentRootScale(float.NaN);

            RevertLabelColorsInternal();

            if (_pendingShowRoutine != null)
            {
                StopCoroutine(_pendingShowRoutine);
                _pendingShowRoutine = null;
            }

            if (_root != null)
                _root.style.display = DisplayStyle.None;
        }

        bool EnsureUi()
        {
            if (popupUxml == null || cloneSourcePanelSettings == null)
            {
                Debug.LogWarning(
                    "[ExtinguisherHoverVrWorldPopupHost] Assign AnnouncementPopup.uxml and a Panel Settings asset to clone (see tooltip).",
                    this);
                return false;
            }

            if (_document != null)
            {
                EnsureRuntimePanelAndDocumentWired();
                ApplyWorldDocumentRootScale(float.NaN);
                ConfigureWorldSpaceUidocument();
                return true;
            }

            _document = GetComponent<UIDocument>();
            if (_document == null)
                _document = gameObject.AddComponent<UIDocument>();

            EnsureRuntimePanelAndDocumentWired();

            ApplyWorldDocumentRootScale(float.NaN);

            ConfigureWorldSpaceUidocument();

            return true;
        }

        /// <summary>
        /// <see cref="float.NaN"/>: <c>transform.localScale = Vector3.one * worldDocumentScale</c>;
        /// aksi halde <c>worldDocumentScale * multiplier</c> (clamp 0.01–50).
        /// </summary>
        void ApplyWorldDocumentRootScale(float multiplierOrNaN)
        {
            float baseS = Mathf.Max(1e-6f, worldDocumentScale);
            float mult = float.IsNaN(multiplierOrNaN) ? 1f : Mathf.Clamp(multiplierOrNaN, 0.01f, 50f);
            transform.localScale = Vector3.one * (baseS * mult);
        }

        void EnsureRuntimePanelAndDocumentWired()
        {
            if (_document == null || cloneSourcePanelSettings == null)
                return;

            if (_runtimePanelSettings == null)
            {
                _runtimePanelSettings = Instantiate(cloneSourcePanelSettings);
                _runtimePanelSettings.name = name + "_WorldPanelSettings";
                _runtimePanelSettings.renderMode = PanelRenderMode.WorldSpace;
            }

            if (_document.panelSettings != _runtimePanelSettings)
                _document.panelSettings = _runtimePanelSettings;

            _runtimePanelSettings.clearColor = true;
            _runtimePanelSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);

            if (popupUxml != null && _document.visualTreeAsset != popupUxml)
                _document.visualTreeAsset = popupUxml;

            _document.sortingOrder = 32000;
            _runtimePanelSettings.sortingOrder = 32000;
        }

        void ConfigureWorldSpaceUidocument()
        {
            if (_document == null)
                return;

            if (worldSpaceUseDynamicSize)
                _document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Dynamic;
            else
            {
                _document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
                _document.worldSpaceSize = new Vector2(
                    Mathf.Max(32f, fixedWorldPanelSize.x),
                    Mathf.Max(32f, fixedWorldPanelSize.y));
            }

            _document.pivot = Pivot.Center;
            _document.pivotReferenceSize = PivotReferenceSize.BoundingBox;
        }

        /// <returns>True when binding completed (or was already done).</returns>
        bool TryBind()
        {
            if (_bound)
                return true;

            if (_document == null)
                return false;

            VisualElement ve = _document.rootVisualElement;
            if (ve == null)
                return false;

            _root = ve.Q<VisualElement>("popup-service-root");
            if (_root == null)
                return false;

            _backdrop = _root.Q<VisualElement>("popup-backdrop");
            _titleLabel = _root.Q<Label>("popup-title");
            _messageLabel = _root.Q<Label>("popup-message");
            _closeButton = _root.Q<Button>("popup-close");

            ApplyVrWorldVisualLayout();

            _root.style.display = DisplayStyle.None;
            _bound = true;
            return true;
        }

        /// <summary>
        /// PC popup uses full-screen root + dim backdrop; in world space that becomes a huge dark quad behind the card.
        /// Strip full-bleed layout, hide backdrop, center the card (same UXML as <see cref="Woi.UI.Popups.PopupService"/>).
        /// </summary>
        void ApplyVrWorldVisualLayout()
        {
            if (_root == null)
                return;

            _root.RemoveFromClassList("screen-container");
            _root.AddToClassList("vr-world-compact");
            _root.style.flexGrow = 0;
            _root.style.backgroundColor = Color.clear;

            VisualElement anchorWrap = _root.Q<VisualElement>("popup-anchor-wrap");
            if (anchorWrap != null)
            {
                anchorWrap.RemoveFromClassList("anchor-top-right");
                anchorWrap.RemoveFromClassList("anchor-top-center");
                anchorWrap.RemoveFromClassList("anchor-bottom-center");
                anchorWrap.RemoveFromClassList("anchor-center");
                anchorWrap.AddToClassList("vr-world-anchor");
            }

            if (_backdrop == null)
                _backdrop = _root.Q<VisualElement>("popup-backdrop");

            if (_backdrop != null)
            {
                _backdrop.style.display = DisplayStyle.None;
                _backdrop.style.opacity = 0f;
            }

            if (_closeButton == null)
                _closeButton = _root.Q<Button>("popup-close");

            if (_closeButton != null)
            {
                _closeButton.style.display = DisplayStyle.None;
                _closeButton.pickingMode = PickingMode.Ignore;
            }
        }

        Camera ResolveBillboardCamera()
        {
            if (billboardCameraOverride != null && billboardCameraOverride.isActiveAndEnabled)
                return billboardCameraOverride;

            if (FirePlatformRuntime.IsVR
                && ServiceLocator.TryGet<IXRPlayerService>(out var xr)
                && xr.PlayerCamera != null
                && xr.PlayerCamera.isActiveAndEnabled)
                return xr.PlayerCamera;

            if (Camera.main != null)
                return Camera.main;

            Camera best = null;
            float bestDepth = float.MinValue;
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera c = cameras[i];
                if (c == null || !c.isActiveAndEnabled)
                    continue;
                if (c.CompareTag("MainCamera"))
                    return c;
                if (c.depth > bestDepth)
                {
                    bestDepth = c.depth;
                    best = c;
                }
            }

            return best;
        }
    }
}
