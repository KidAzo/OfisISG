using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Woi.OfficeFire
{
    public enum InteractPopupFacingMode
    {
        AutoFaceCamera = 0,
        AnchorForward = 1,
    }

    /// <summary>
    /// Dedicated world-space hover popup for selectables. Uses the same UXML as PopupService
    /// but owns a separate runtime <see cref="UIDocument"/> — the 2D PopupHUD document is never touched.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Interact Hover Popup Host")]
    public sealed class OfficeFireInteractPopupHost : MonoBehaviour
    {
        private static readonly Regex QuotedKeyPattern = new(
            @"^(""([^""]+)""|'([^']+)')\s*(.*)$",
            RegexOptions.Compiled);

        [SerializeField]
        private VisualTreeAsset popupUxml;

        [Tooltip("World Space Panel Settings used for 3D hover popups. Do NOT use the Screen Space PopupHUD settings.")]
        [SerializeField]
        private PanelSettings worldPanelSettingsSource;

        [Tooltip("AutoFaceCamera keeps the anchor yaw and only applies a small turn toward the camera plus a readable-side flip when needed.")]
        [SerializeField]
        private InteractPopupFacingMode facingMode = InteractPopupFacingMode.AutoFaceCamera;

        [Tooltip("Max left/right yaw adjustment toward the camera in AutoFaceCamera mode.")]
        [SerializeField]
        [Range(0f, 15f)]
        private float autoFaceCameraMaxYawDegrees = 2f;

        [Tooltip("Only used when Facing Mode is AnchorForward.")]
        [SerializeField]
        private bool uiFacingFlipY180 = true;

        [SerializeField]
        private bool autoResolvePlayerCamera = true;

        [SerializeField]
        private string playerTag = "Player";

        [SerializeField]
        private float worldDocumentScale = 0.0016f;

        [SerializeField]
        private bool worldSpaceUseDynamicSize = true;

        [SerializeField]
        private Vector2 fixedWorldPanelSize = new Vector2(400f, 200f);

        private UIDocument _document;
        private PanelSettings _runtimePanelSettings;
        private VisualElement _root;
        private VisualElement _backdrop;
        private Label _titleLabel;
        private Label _messageLabel;
        private Button _closeButton;
        private bool _bound;
        private bool _attachToPanelRegistered;
        private bool _visible;
        private bool _hasPendingShow;
        private object _currentOwner;
        private Transform _currentAnchor;
        private Vector3 _currentLocalOffset;
        private float _currentWorldScale = 1f;
        private Coroutine _waitForRootRoutine;
        private Coroutine _pendingShowRoutine;

        private object _pendingOwner;
        private Transform _pendingAnchor;
        private Vector3 _pendingLocalOffset;
        private float _pendingWorldScale = 1f;
        private string _pendingInstructionText;
        private string _currentInstructionText = string.Empty;
        private Quaternion _baseWorldRotation = Quaternion.identity;
        private Camera _resolvedViewCamera;

        public static OfficeFireInteractPopupHost FindInstance() =>
            FindFirstObjectByType<OfficeFireInteractPopupHost>(FindObjectsInactive.Include);

        public static bool TryGetInstance(out OfficeFireInteractPopupHost host)
        {
            host = FindInstance();
            return host != null;
        }

        private void Awake()
        {
            EnsureUi();
            EnsureAttachToPanelSubscription();
        }

        private void OnEnable()
        {
            EnsureUi();
            EnsureAttachToPanelSubscription();
        }

        private void OnDisable()
        {
            HideInternal();
            _bound = false;
            _attachToPanelRegistered = false;
            _root = null;
            _backdrop = null;
            _titleLabel = null;
            _messageLabel = null;
            _closeButton = null;

            if (_waitForRootRoutine != null)
            {
                StopCoroutine(_waitForRootRoutine);
                _waitForRootRoutine = null;
            }
        }

        private void LateUpdate()
        {
            if (!_visible)
            {
                return;
            }

            UpdateWorldPosition();
            ApplyAnchorRotation();
        }

        private void OnDestroy()
        {
            if (_pendingShowRoutine != null)
            {
                StopCoroutine(_pendingShowRoutine);
                _pendingShowRoutine = null;
            }

            if (_waitForRootRoutine != null)
            {
                StopCoroutine(_waitForRootRoutine);
                _waitForRootRoutine = null;
            }

            if (_runtimePanelSettings != null)
            {
                Destroy(_runtimePanelSettings);
            }
        }

        public void Show(object owner, Transform anchor, Vector3 localOffset, string instructionText, float worldScale = 1f)
        {
            _pendingOwner = owner;
            _pendingAnchor = anchor;
            _pendingLocalOffset = localOffset;
            _pendingWorldScale = worldScale > 0f ? worldScale : 1f;
            _pendingInstructionText = instructionText;
            _hasPendingShow = true;

            if (!EnsureUi())
            {
                return;
            }

            EnsureAttachToPanelSubscription();

            if (TryBind())
            {
                ApplyShow(
                    _pendingOwner,
                    _pendingAnchor,
                    _pendingLocalOffset,
                    _pendingInstructionText,
                    _pendingWorldScale);
                _hasPendingShow = false;
                return;
            }

            if (_pendingShowRoutine == null && isActiveAndEnabled)
            {
                _pendingShowRoutine = StartCoroutine(ShowPendingWhenRootReady());
            }
        }

        public void UpdatePosition(object owner, Transform anchor, Vector3 localOffset, float worldScale = 1f)
        {
            if (!_visible || !ReferenceEquals(_currentOwner, owner))
            {
                return;
            }

            _currentAnchor = anchor != null ? anchor : transform;
            _currentLocalOffset = localOffset;
            _currentWorldScale = worldScale > 0f ? worldScale : 1f;
            ApplyWorldScale();
            UpdateWorldPosition();
        }

        public void Hide(object owner)
        {
            if (_visible && _currentOwner != null && !ReferenceEquals(_currentOwner, owner))
            {
                return;
            }

            HideInternal();
        }

        private IEnumerator ShowPendingWhenRootReady()
        {
            const int maxFrames = 120;
            for (int i = 0; i < maxFrames && !TryBind(); i++)
            {
                yield return null;
            }

            _pendingShowRoutine = null;

            if (!_hasPendingShow)
            {
                yield break;
            }

            if (!_bound)
            {
                Debug.LogWarning(
                    "[OfficeFireInteractPopupHost] UIDocument root was not ready — hover popup not shown.",
                    this);
                yield break;
            }

            ApplyShow(
                _pendingOwner,
                _pendingAnchor,
                _pendingLocalOffset,
                _pendingInstructionText,
                _pendingWorldScale);
            _hasPendingShow = false;
        }

        private void ApplyShow(
            object owner,
            Transform anchor,
            Vector3 localOffset,
            string instructionText,
            float worldScale)
        {
            _currentOwner = owner;
            _currentAnchor = anchor != null ? anchor : transform;
            _currentLocalOffset = localOffset;
            _currentWorldScale = worldScale > 0f ? worldScale : 1f;
            _currentInstructionText = instructionText ?? string.Empty;
            _baseWorldRotation = ResolveBaseRotation(_currentAnchor);

            if (!TryBind())
            {
                return;
            }

            EnsureLabelBindings();
            ApplyVrWorldVisualLayout();
            ApplyInstructionText(_currentInstructionText);
            ApplyWorldScale();

            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
                _root.MarkDirtyRepaint();
            }

            _document?.rootVisualElement?.MarkDirtyRepaint();

            _visible = true;
            UpdateWorldPosition();
            ApplyAnchorRotation();
        }

        private void HideInternal()
        {
            _visible = false;
            _hasPendingShow = false;
            _currentOwner = null;
            _currentAnchor = null;
            _currentLocalOffset = Vector3.zero;
            _currentWorldScale = 1f;

            if (_pendingShowRoutine != null)
            {
                StopCoroutine(_pendingShowRoutine);
                _pendingShowRoutine = null;
            }

            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
            }
        }

        private void EnsureAttachToPanelSubscription()
        {
            if (_document == null || _attachToPanelRegistered)
            {
                return;
            }

            VisualElement visualRoot = _document.rootVisualElement;
            if (visualRoot != null)
            {
                RegisterAttachToPanelOnRoot(visualRoot);
                return;
            }

            if (_waitForRootRoutine == null && isActiveAndEnabled)
            {
                _waitForRootRoutine = StartCoroutine(WaitForRootAndSubscribe());
            }
        }

        private void RegisterAttachToPanelOnRoot(VisualElement visualRoot)
        {
            if (_attachToPanelRegistered || visualRoot == null)
            {
                return;
            }

            visualRoot.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            _attachToPanelRegistered = true;

            if (visualRoot.panel != null)
            {
                TryBind();
                TryApplyPendingShow();
            }
        }

        private IEnumerator WaitForRootAndSubscribe()
        {
            while (isActiveAndEnabled && _document != null)
            {
                VisualElement visualRoot = _document.rootVisualElement;
                if (visualRoot != null)
                {
                    RegisterAttachToPanelOnRoot(visualRoot);
                    _waitForRootRoutine = null;
                    yield break;
                }

                yield return null;
            }

            _waitForRootRoutine = null;
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            TryBind();
            TryApplyPendingShow();
        }

        private void TryApplyPendingShow()
        {
            if (!_hasPendingShow || !TryBind())
            {
                return;
            }

            ApplyShow(
                _pendingOwner,
                _pendingAnchor,
                _pendingLocalOffset,
                _pendingInstructionText,
                _pendingWorldScale);
            _hasPendingShow = false;

            if (_pendingShowRoutine != null)
            {
                StopCoroutine(_pendingShowRoutine);
                _pendingShowRoutine = null;
            }
        }

        private void ApplyWorldScale()
        {
            float scale = Mathf.Max(1e-6f, worldDocumentScale * _currentWorldScale);
            transform.localScale = Vector3.one * scale;
        }

        private void UpdateWorldPosition()
        {
            if (_currentAnchor == null)
            {
                return;
            }

            transform.position = _currentAnchor.TransformPoint(_currentLocalOffset);
            _baseWorldRotation = ResolveBaseRotation(_currentAnchor);
            ApplyAnchorRotation();
        }

        private void ApplyAnchorRotation()
        {
            if (facingMode == InteractPopupFacingMode.AutoFaceCamera)
            {
                Camera viewCamera = ResolveViewCamera();
                if (viewCamera != null)
                {
                    transform.rotation = ResolveReadableCameraRotation(viewCamera.transform);
                    return;
                }
            }

            transform.rotation = ApplyUiFacingFlip(_baseWorldRotation);
        }

        private Quaternion ResolveReadableCameraRotation(Transform cameraTransform)
        {
            Quaternion anchorRotation = ResolveReadableAnchorRotation(cameraTransform);
            return ApplyClampedYawTowardCamera(anchorRotation, cameraTransform, autoFaceCameraMaxYawDegrees);
        }

        private Quaternion ResolveReadableAnchorRotation(Transform cameraTransform)
        {
            Quaternion noFlip = _baseWorldRotation;
            Quaternion withFlip = _baseWorldRotation * Quaternion.Euler(0f, 180f, 0f);

            float noFlipReadability = Vector3.Dot(noFlip * Vector3.right, cameraTransform.right);
            float flipReadability = Vector3.Dot(withFlip * Vector3.right, cameraTransform.right);

            return flipReadability > noFlipReadability ? withFlip : noFlip;
        }

        private Quaternion ApplyClampedYawTowardCamera(
            Quaternion baseRotation,
            Transform cameraTransform,
            float maxYawDegrees)
        {
            if (maxYawDegrees <= 0f)
            {
                return baseRotation;
            }

            Vector3 toCamera = cameraTransform.position - transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude <= 1e-8f)
            {
                return baseRotation;
            }

            Vector3 baseForward = baseRotation * Vector3.forward;
            baseForward.y = 0f;
            if (baseForward.sqrMagnitude <= 1e-8f)
            {
                return baseRotation;
            }

            float yawDelta = Vector3.SignedAngle(
                baseForward.normalized,
                toCamera.normalized,
                Vector3.up);
            yawDelta = Mathf.Clamp(yawDelta, -maxYawDegrees, maxYawDegrees);

            return baseRotation * Quaternion.Euler(0f, yawDelta, 0f);
        }

        private Camera ResolveViewCamera()
        {
            if (_resolvedViewCamera != null && _resolvedViewCamera.isActiveAndEnabled)
            {
                return _resolvedViewCamera;
            }

            if (Camera.main != null)
            {
                _resolvedViewCamera = Camera.main;
                return _resolvedViewCamera;
            }

            if (!autoResolvePlayerCamera)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(playerTag))
            {
                GameObject player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null)
                {
                    Camera playerCamera = player.GetComponentInChildren<Camera>(true);
                    if (playerCamera != null)
                    {
                        _resolvedViewCamera = playerCamera;
                        return _resolvedViewCamera;
                    }
                }
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                {
                    continue;
                }

                _resolvedViewCamera = candidate;
                return _resolvedViewCamera;
            }

            return null;
        }

        private Quaternion ApplyUiFacingFlip(Quaternion rotation)
        {
            if (uiFacingFlipY180)
            {
                return rotation * Quaternion.Euler(0f, 180f, 0f);
            }

            return rotation;
        }

        private static Quaternion ResolveBaseRotation(Transform anchor)
        {
            if (anchor == null)
            {
                return Quaternion.identity;
            }

            Vector3 forward = anchor.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 1e-8f)
            {
                forward = Vector3.ProjectOnPlane(anchor.up, Vector3.up);
                if (forward.sqrMagnitude <= 1e-8f)
                {
                    return anchor.rotation;
                }
            }

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private void EnsureLabelBindings()
        {
            if (_root == null && _document?.rootVisualElement != null)
            {
                _root = _document.rootVisualElement.Q<VisualElement>("popup-service-root");
            }

            if (_root == null)
            {
                return;
            }

            _backdrop = _root.Q<VisualElement>("popup-backdrop");
            _titleLabel = _root.Q<Label>("popup-title");
            _messageLabel = _root.Q<Label>("popup-message");
            _closeButton = _root.Q<Button>("popup-close");
        }

        private void ApplyInstructionText(string instructionText)
        {
            EnsureLabelBindings();
            ParseInstruction(instructionText ?? string.Empty, out string title, out string message);

            if (_titleLabel != null)
            {
                _titleLabel.text = title;
                _titleLabel.style.display = string.IsNullOrWhiteSpace(title)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            if (_messageLabel != null)
            {
                _messageLabel.text = message ?? string.Empty;
                _messageLabel.style.display = string.IsNullOrWhiteSpace(message)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
        }

        private static void ParseInstruction(string text, out string title, out string message)
        {
            text = text.Trim();
            Match match = QuotedKeyPattern.Match(text);
            if (match.Success)
            {
                title = match.Groups[1].Value;
                message = match.Groups[4].Value.Trim();
                if (string.IsNullOrEmpty(message))
                {
                    message = null;
                }

                return;
            }

            title = text;
            message = null;
        }

        private bool EnsureUi()
        {
            if (popupUxml == null || worldPanelSettingsSource == null)
            {
                Debug.LogWarning(
                    "[OfficeFireInteractPopupHost] Assign AnnouncementPopup.uxml and InteractHoverWorldPanelSettings.",
                    this);
                return false;
            }

            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
                if (_document == null)
                {
                    _document = gameObject.AddComponent<UIDocument>();
                }
            }

            EnsureRuntimePanelAndDocumentWired();
            ConfigureWorldSpaceUidocument();
            return true;
        }

        private void EnsureRuntimePanelAndDocumentWired()
        {
            if (_document == null || worldPanelSettingsSource == null)
            {
                return;
            }

            if (_runtimePanelSettings == null)
            {
                _runtimePanelSettings = Instantiate(worldPanelSettingsSource);
                _runtimePanelSettings.name = name + "_InteractHoverPanelSettings";
            }

            if (_runtimePanelSettings.renderMode != PanelRenderMode.WorldSpace)
            {
                _runtimePanelSettings.renderMode = PanelRenderMode.WorldSpace;
            }
            _runtimePanelSettings.clearColor = true;
            _runtimePanelSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);
            _runtimePanelSettings.sortingOrder = 31000;

            PanelSettings previousPanel = _document.panelSettings;
            VisualTreeAsset previousUxml = _document.visualTreeAsset;

            _document.panelSettings = _runtimePanelSettings;
            _document.visualTreeAsset = popupUxml;
            _document.sortingOrder = 31000;

            if (previousPanel != _runtimePanelSettings || previousUxml != popupUxml)
            {
                _bound = false;
                _root = null;
                _backdrop = null;
                _titleLabel = null;
                _messageLabel = null;
                _closeButton = null;
                _attachToPanelRegistered = false;
            }
        }

        private void ConfigureWorldSpaceUidocument()
        {
            if (_document == null)
            {
                return;
            }

            if (worldSpaceUseDynamicSize)
            {
                _document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Dynamic;
            }
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

        private bool TryBind()
        {
            if (_document == null)
            {
                return false;
            }

            VisualElement visualRoot = _document.rootVisualElement;
            if (visualRoot == null)
            {
                return false;
            }

            VisualElement root = visualRoot.Q<VisualElement>("popup-service-root");
            if (root == null)
            {
                return false;
            }

            _root = root;
            EnsureLabelBindings();

            if (!_bound)
            {
                ApplyVrWorldVisualLayout();
                if (!string.IsNullOrEmpty(_currentInstructionText))
                {
                    ApplyInstructionText(_currentInstructionText);
                }

                _root.style.display = DisplayStyle.None;
                _bound = true;
            }

            return true;
        }

        private void ApplyVrWorldVisualLayout()
        {
            if (_root == null)
            {
                return;
            }

            EnsureLabelBindings();

            _root.RemoveFromClassList("screen-container");
            _root.AddToClassList("vr-world-compact");
            _root.AddToClassList("interact-hover-compact");
            _root.style.flexGrow = 0;
            _root.style.backgroundColor = Color.clear;
            _root.style.width = StyleKeyword.Auto;
            _root.style.height = StyleKeyword.Auto;

            VisualElement anchorWrap = _root.Q<VisualElement>("popup-anchor-wrap");
            if (anchorWrap != null)
            {
                anchorWrap.RemoveFromClassList("anchor-top-right");
                anchorWrap.RemoveFromClassList("anchor-top-center");
                anchorWrap.RemoveFromClassList("anchor-bottom-center");
                anchorWrap.RemoveFromClassList("anchor-center");
                anchorWrap.RemoveFromClassList("notification-container");
                anchorWrap.AddToClassList("vr-world-anchor");
                anchorWrap.style.width = StyleKeyword.Auto;
                anchorWrap.style.maxWidth = StyleKeyword.None;
            }

            VisualElement panel = _root.Q<VisualElement>("popup-panel");
            if (panel != null)
            {
                panel.AddToClassList("interact-hover-compact");
                panel.style.marginBottom = 0;
                panel.style.alignSelf = Align.FlexStart;
                panel.style.flexGrow = 0;
                panel.style.flexShrink = 0;
            }

            VisualElement mainRow = _root.Q<VisualElement>(className: "popup-main-row");
            if (mainRow != null)
            {
                mainRow.style.flexGrow = 0;
                mainRow.style.flexShrink = 0;
                mainRow.style.alignItems = Align.Center;
            }

            VisualElement textStack = _root.Q<VisualElement>(className: "popup-text-stack");
            if (textStack != null)
            {
                textStack.style.flexDirection = FlexDirection.Row;
                textStack.style.alignItems = Align.Center;
                textStack.style.flexGrow = 0;
                textStack.style.flexShrink = 0;
                textStack.style.marginRight = 0;
            }

            if (_titleLabel != null)
            {
                _titleLabel.style.marginBottom = 0;
                _titleLabel.style.marginRight = 6;
                _titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            }

            if (_messageLabel != null)
            {
                _messageLabel.style.marginTop = 0;
                _messageLabel.style.marginBottom = 0;
                _messageLabel.style.whiteSpace = WhiteSpace.NoWrap;
            }

            VisualElement iconWrap = _root.Q<VisualElement>("popup-icon-wrap");
            if (iconWrap != null)
            {
                iconWrap.style.display = DisplayStyle.None;
            }

            if (_backdrop != null)
            {
                _backdrop.style.display = DisplayStyle.None;
                _backdrop.style.opacity = 0f;
            }

            HideCloseButton();
        }

        private void HideCloseButton()
        {
            if (_closeButton == null && _root != null)
            {
                _closeButton = _root.Q<Button>("popup-close");
            }

            if (_closeButton == null)
            {
                return;
            }

            _closeButton.style.display = DisplayStyle.None;
            _closeButton.visible = false;
            _closeButton.pickingMode = PickingMode.Ignore;
        }
    }
}
