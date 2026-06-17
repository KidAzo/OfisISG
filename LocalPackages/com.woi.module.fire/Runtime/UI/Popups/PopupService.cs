/*
 * POPUP SERVICE — SETUP (Unity Editor)
 * ------------------------------------
 * 1. Create UI Document: GameObject → UI Toolkit → UI Document.
 * 2. Assign Panel Settings (Screen Space Overlay recommended).
 * 3. Set Source Asset to AnnouncementPopup.uxml (Assets/_Game/UI/Popups/AnnouncementPopup.uxml).
 * 4. Add PopupService to the same GameObject (or bootstrap object).
 * 5. Wire PopupService → UIDocument and optionally LocalizationService.
 * 6. Add LocalizationService once in the scene (or DDOL) for Turkish/English switching.
 * 7. Create PopupDefinition assets via Create → Woi → UI → Popup Definition (content = variants × language rows: title + message per row).
 *
 * By default this component persists across scenes (DontDestroyOnLoad) and destroys duplicate instances.
 *
 * Standalone use: call Show / ShowText / Hide from code or use PopupTrigger + UnityEvents.
 * Service locator: register in Start; resolve ServiceLocator from other scripts in Start or later, not Awake.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using WOI.Modules.SDK;
using Woi.UI.Popups.Localization;

namespace Woi.UI.Popups
{
    [DefaultExecutionOrder(-5000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/UI/Popup Service")]
    public sealed class PopupService : MonoBehaviour, IPopupService
    {
        private const float FadeSeconds = 0.22f;
        private const float SlidePixels = 18f;

        [Header("Persistence")]
        [Tooltip("Survives scene loads (DontDestroyOnLoad). Duplicate PopupService components are destroyed.")]
        [SerializeField]
        private bool persistAcrossScenes = true;

        private static PopupService _instance;

        [SerializeField] private UIDocument document;
        [SerializeField] private LocalizationService localizationService;

        [Tooltip("When a new popup requests show without replacing (Popup Definition → Replace Current Popup off), Queue Next waits until the current popup closes; Ignore New drops that request.")]
        [SerializeField] private PopupOverflowBehavior overflowBehavior = PopupOverflowBehavior.QueueNext;

        [Tooltip("Seconds for fade + slide.")]
        [SerializeField, Min(0.02f)]
        private float animationDuration = 0.22f;

        [Tooltip("When the active Unity scene changes (e.g. SceneGroup load / SetActiveScene), dismiss visible popups and drop queued Show requests.")]
        [SerializeField]
        private bool dismissPopupsOnActiveSceneChange = true;

        [Header("Service locator")]
        [Tooltip("Registers IPopupService / PopupService on ServiceLocator in Start when not already registered (runs after all Awakes).")]
        [SerializeField]
        private bool registerWithServiceLocator = true;

        private bool _registeredWithServiceLocator;

        private VisualElement _root;
        private VisualElement _backdrop;
        private VisualElement _anchorWrap;
        private VisualElement _panel;
        private Label _titleLabel;
        private Label _messageLabel;
        private VisualElement _iconWrap;
        private UnityEngine.UIElements.Image _iconImage;
        private Button _closeButton;

        private PopupDefinition _currentDefinition;
        private PopupDefinition _transientDefinition;
        private bool _bound;

        private Coroutine _routine;
        private Coroutine _subscribeRootRoutine;
        private bool _attachToPanelRegistered;
        private int _animToken;

        private readonly Queue<PendingPopup> _queue = new Queue<PendingPopup>();

        private bool _isOpen;
        private string _lastCustomUssClass;

        public bool IsVisible => _isOpen;

        public event Action<PopupDefinition> OnPopupShown;
        public event Action OnPopupHidden;
        public event Action<PopupDefinition> OnPopupClicked;
        public event Action<PopupDefinition> OnPopupCloseButtonClicked;

        private struct PendingPopup
        {
            public PopupDefinition Definition;
            public float DurationOverride;
            public bool HasDurationOverride;
            public int ContentEntryIndex;
            public bool? BlockInputOverride;
        }

        private void Awake()
        {
            if (persistAcrossScenes)
            {
                if (_instance != null && _instance != this)
                {
                    Destroy(gameObject);
                    return;
                }

                _instance = this;
                DontDestroyOnLoad(gameObject);
            }

            if (document == null)
                document = GetComponent<UIDocument>();

            if (localizationService == null)
                localizationService = LocalizationService.Instance;
        }

        private void Start()
        {
            TryRegisterWithServiceLocator();
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            _instance = null;
            TryUnregisterWithServiceLocator();
        }

        private void EnsureGameObjectActive()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        private bool TryStartRoutine(IEnumerator routine, out Coroutine started)
        {
            started = null;
            if (routine == null)
            {
                return false;
            }

            EnsureGameObjectActive();
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning(
                    "[PopupService] Cannot start coroutine because the GameObject is inactive.",
                    this);
                return false;
            }

            started = StartCoroutine(routine);
            return true;
        }

        private void TryRegisterWithServiceLocator()
        {
            if (!registerWithServiceLocator)
                return;

            if (ServiceLocator.IsRegistered<IPopupService>())
                return;

            ServiceLocator.Register<IPopupService>(this);
            ServiceLocator.Register<PopupService>(this);
            _registeredWithServiceLocator = true;
        }

        private void TryUnregisterWithServiceLocator()
        {
            if (!_registeredWithServiceLocator)
                return;

            ServiceLocator.Unregister<IPopupService>();
            ServiceLocator.Unregister<PopupService>();
            _registeredWithServiceLocator = false;
        }

        private void OnEnable()
        {
            EnsureAttachToPanelSubscription();

            if (dismissPopupsOnActiveSceneChange)
                SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            if (document != null && _attachToPanelRegistered)
            {
                var root = document.rootVisualElement;
                if (root != null)
                    root.UnregisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            }

            _attachToPanelRegistered = false;
            _subscribeRootRoutine = null;

            StopAllCoroutines();
            _routine = null;
        }

        /// <summary>
        /// <see cref="UIDocument.rootVisualElement"/> is often still null in <see cref="OnEnable"/> until the document sources UXML / joins a panel.
        /// </summary>
        private void EnsureAttachToPanelSubscription()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (document == null)
            {
                Debug.LogWarning("[PopupService] No UIDocument — assign the reference or add UIDocument on the same GameObject.");
                return;
            }

            var root = document.rootVisualElement;
            if (root != null)
            {
                RegisterAttachToPanelOnRoot(root);
                return;
            }

            if (_subscribeRootRoutine == null && isActiveAndEnabled)
            {
                if (TryStartRoutine(WaitForRootAndSubscribe(), out Coroutine subscribeRoutine))
                {
                    _subscribeRootRoutine = subscribeRoutine;
                }
            }
        }

        private void RegisterAttachToPanelOnRoot(VisualElement root)
        {
            if (_attachToPanelRegistered)
                return;

            root.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            _attachToPanelRegistered = true;

            if (root.panel != null)
                TryBind();
        }

        private IEnumerator WaitForRootAndSubscribe()
        {
            while (isActiveAndEnabled && document != null)
            {
                var root = document.rootVisualElement;
                if (root != null)
                {
                    RegisterAttachToPanelOnRoot(root);
                    _subscribeRootRoutine = null;
                    yield break;
                }

                yield return null;
            }

            _subscribeRootRoutine = null;
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            TryBind();
        }

        private void TryBind()
        {
            if (_bound || document == null)
                return;

            VisualElement ve = document.rootVisualElement;
            if (ve == null)
                return;

            _root = ve.Q<VisualElement>("popup-service-root");
            if (_root == null)
            {
                Debug.LogWarning("[PopupService] UXML must define 'popup-service-root'.");
                return;
            }

            _backdrop = _root.Q<VisualElement>("popup-backdrop");
            _anchorWrap = _root.Q<VisualElement>("popup-anchor-wrap");
            _panel = _root.Q<VisualElement>("popup-panel");
            _titleLabel = _root.Q<Label>("popup-title");
            _messageLabel = _root.Q<Label>("popup-message");
            _iconWrap = _root.Q<VisualElement>("popup-icon-wrap");
            _iconImage = _root.Q<UnityEngine.UIElements.Image>("popup-icon");
            _closeButton = _root.Q<Button>("popup-close");

            if (_backdrop != null)
                _backdrop.RegisterCallback<ClickEvent>(OnBackdropClick);

            if (_closeButton != null)
                _closeButton.clicked += OnCloseClicked;

            _root.style.display = DisplayStyle.None;
            _bound = true;
        }

        private void OnCloseClicked()
        {
            PopupDefinition def = _currentDefinition;
            Debug.Log("[PopupService] Hide (close button)");
            if (def != null)
                OnPopupCloseButtonClicked?.Invoke(def);

            Hide();
        }

        private void OnBackdropClick(ClickEvent evt)
        {
            if (_currentDefinition != null)
                OnPopupClicked?.Invoke(_currentDefinition);
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            if (!dismissPopupsOnActiveSceneChange)
                return;

            DismissAllPopups();
        }

        public void Show(PopupDefinition definition)
        {
            Show(definition, -1f);
        }

        public void Show(PopupDefinition definition, float durationOverride, bool? blockInputOverride = null)
        {
            Show(definition, durationOverride, -1, blockInputOverride);
        }

        /// <summary>Uses <see cref="PopupDefinition.contentVariants"/> slot <paramref name="contentEntryIndex"/> (Queue All + per-clip text).</summary>
        public void Show(PopupDefinition definition, float durationOverride, int contentEntryIndex, bool? blockInputOverride = null)
        {
            if (definition == null)
                return;

            bool hasOverride = durationOverride >= 0f;
            ShowInternal(definition, hasOverride, durationOverride, forceReplaceCurrent: false, contentEntryIndex, blockInputOverride);
        }

        public void ShowText(string title, string message, PopupType type)
        {
            PopupDefinition transient = PopupDefinition.CreateTransient(title ?? string.Empty, message ?? string.Empty, type);
            _transientDefinition = transient;
            ShowInternal(transient, false, -1f, false, -1, null);
        }

        /// <inheritdoc />
        public void ShowText(string title, string message, PopupType type, float visibleSeconds)
        {
            float seconds = Mathf.Max(0.02f, visibleSeconds);
            PopupDefinition transient = PopupDefinition.CreateTransient(title ?? string.Empty, message ?? string.Empty, type);
            transient.defaultDuration = seconds;
            _transientDefinition = transient;
            ShowInternal(transient, true, seconds, false, -1, blockInputOverride: false);
        }

        /// <inheritdoc />
        public void ShowLocalizedText(
            string titleTr,
            string messageTr,
            string titleEn,
            string messageEn,
            PopupType type,
            float visibleSeconds)
        {
            EnsureGameObjectActive();

            float seconds = Mathf.Max(0.02f, visibleSeconds);
            PopupDefinition transient = PopupDefinition.CreateTransientBilingual(
                titleTr,
                messageTr,
                titleEn,
                messageEn,
                type,
                seconds);
            _transientDefinition = transient;
            ShowInternal(transient, true, seconds, false, -1, blockInputOverride: false);
        }

        /// <inheritdoc />
        public void ShowTextUntilHidden(string title, string message, PopupType type) =>
            ShowTextUntilHidden(title, message, type, PopupAnchor.TopRight);

        /// <inheritdoc />
        public void ShowTextUntilHidden(string title, string message, PopupType type, PopupAnchor anchor)
        {
            PopupDefinition transient = PopupDefinition.CreateHoverTransient(title ?? string.Empty, message ?? string.Empty, type, anchor);
            _transientDefinition = transient;
            ShowInternal(transient, false, -1f, forceReplaceCurrent: true, -1, blockInputOverride: false);
        }

        /// <summary>Show immediately, replacing any visible popup (does not modify the <see cref="PopupDefinition"/> asset).</summary>
        public void Replace(PopupDefinition definition)
        {
            Replace(definition, -1f);
        }

        /// <inheritdoc cref="Show(PopupDefinition, float, int)"/>
        public void Replace(PopupDefinition definition, float durationOverride, bool? blockInputOverride = null)
        {
            Replace(definition, durationOverride, -1, blockInputOverride);
        }

        public void Replace(PopupDefinition definition, float durationOverride, int contentEntryIndex, bool? blockInputOverride = null)
        {
            if (definition == null)
                return;

            bool hasOverride = durationOverride >= 0f;
            ShowInternal(definition, hasOverride, durationOverride, forceReplaceCurrent: true, contentEntryIndex, blockInputOverride);
        }

        public void Hide()
        {
            HideInternal(dequeuePendingAfterHide: true);
        }

        /// <inheritdoc cref="IPopupService.DismissAllPopups"/>
        public void DismissAllPopups()
        {
            _queue.Clear();
            HideInternal(dequeuePendingAfterHide: false);
        }

        private void HideInternal(bool dequeuePendingAfterHide)
        {
            Debug.Log("[PopupService] Hide");
            if (!_bound)
                TryBind();

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            _animToken++;
            int token = _animToken;

            if (gameObject.activeInHierarchy && _root != null && _root.style.display == DisplayStyle.Flex)
            {
                if (TryStartRoutine(HideRoutine(token, dequeueAfter: dequeuePendingAfterHide), out Coroutine hideRoutine))
                {
                    _routine = hideRoutine;
                }
                else
                {
                    SnapHidden(dequeueAfter: dequeuePendingAfterHide);
                }
            }
            else
                SnapHidden(dequeueAfter: dequeuePendingAfterHide);
        }

        private void ShowInternal(
            PopupDefinition definition,
            bool hasDurationOverride,
            float durationOverride,
            bool forceReplaceCurrent = false,
            int contentEntryIndex = -1,
            bool? blockInputOverride = null)
        {
            EnsureGameObjectActive();
            TryBind();
            if (!_bound)
                return;

            bool replaceCurrent = forceReplaceCurrent || definition.replaceCurrentPopup;
            // Use _currentDefinition, not _isOpen: _isOpen flips only after open animation; otherwise a second Show
            // in the same frame / during fade-in bypasses the queue and steals the slot.
            bool hasOccupant = _currentDefinition != null;

            if (hasOccupant && !replaceCurrent)
            {
                if (overflowBehavior == PopupOverflowBehavior.IgnoreNew)
                {
                    Debug.Log($"[PopupService] Show: ignored (active popup, queued mode). id={definition.id}");
                    return;
                }

                _queue.Enqueue(new PendingPopup
                {
                    Definition = definition,
                    DurationOverride = durationOverride,
                    HasDurationOverride = hasDurationOverride,
                    ContentEntryIndex = contentEntryIndex,
                    BlockInputOverride = blockInputOverride
                });
                Debug.Log($"[PopupService] Show: queued id={definition.id}");
                return;
            }

            // Same PopupDefinition + new content variant (queued clips): update text in place and restart the dwell timer.
            // Avoids full SwitchRoutine hide/show — that re-triggers the open animation and briefly reads like "popup 0 again"
            // before clip 2, and races with the previous clip's auto-close hide.
            if (forceReplaceCurrent
                && hasOccupant
                && _isOpen
                && contentEntryIndex >= 0
                && ReferenceEquals(definition, _currentDefinition))
            {
                if (_routine != null)
                {
                    StopCoroutine(_routine);
                    _routine = null;
                }

                _animToken++;
                int swapToken = _animToken;

                if (!definition.autoClose)
                {
                    ApplyDefinitionToUi(definition, contentEntryIndex);
                    SetInteractable(GetBlockInput(definition, blockInputOverride));
                    return;
                }

                _routine = TryStartRoutine(
                    ContentRefreshRoutine(
                        definition,
                        hasDurationOverride,
                        durationOverride,
                        contentEntryIndex,
                        swapToken,
                        blockInputOverride),
                    out Coroutine refreshRoutine)
                    ? refreshRoutine
                    : null;

                if (_routine == null)
                {
                    ShowImmediate(
                        definition,
                        hasDurationOverride,
                        durationOverride,
                        contentEntryIndex,
                        blockInputOverride);
                }

                return;
            }

            Debug.Log($"[PopupService] Show: id={definition.id}, type={definition.type}");

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            _animToken++;
            int token = _animToken;

            IEnumerator routine = hasOccupant && replaceCurrent
                ? SwitchRoutine(definition, hasDurationOverride, durationOverride, token, contentEntryIndex, blockInputOverride)
                : ShowRoutine(definition, hasDurationOverride, durationOverride, token, contentEntryIndex, blockInputOverride);

            if (TryStartRoutine(routine, out Coroutine started))
            {
                _routine = started;
            }
            else
            {
                ShowImmediate(
                    definition,
                    hasDurationOverride,
                    durationOverride,
                    contentEntryIndex,
                    blockInputOverride);
            }
        }

        private void ShowImmediate(
            PopupDefinition definition,
            bool hasDurationOverride,
            float durationOverride,
            int contentEntryIndex,
            bool? blockInputOverride)
        {
            ApplyDefinitionToUi(definition, contentEntryIndex);
            bool block = GetBlockInput(definition, blockInputOverride);

            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
            }

            _currentDefinition = definition;
            _isOpen = true;
            SetInteractable(block);

            if (_panel != null)
            {
                _panel.style.opacity = 1f;
                _panel.style.translate = new Translate(0f, 0f);
            }

            if (_backdrop != null)
            {
                _backdrop.style.opacity = block ? 0.55f : 0f;
            }

            OnPopupShown?.Invoke(definition);

            if (!definition.autoClose)
            {
                return;
            }

            float wait = hasDurationOverride && durationOverride >= 0f
                ? durationOverride
                : definition.defaultDuration;

            if (wait <= 0f)
            {
                HideInternal(dequeuePendingAfterHide: true);
                return;
            }

            if (TryStartRoutine(AutoHideAfterSeconds(wait), out Coroutine autoHide))
            {
                _routine = autoHide;
            }
            else
            {
                HideInternal(dequeuePendingAfterHide: true);
            }
        }

        private IEnumerator AutoHideAfterSeconds(float waitSeconds)
        {
            float elapsed = 0f;
            while (elapsed < waitSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            HideInternal(dequeuePendingAfterHide: true);
        }

        private IEnumerator SwitchRoutine(
            PopupDefinition definition,
            bool hasDurationOverride,
            float durationOverride,
            int token,
            int contentEntryIndex,
            bool? blockInputOverride)
        {
            yield return HideRoutine(token, dequeueAfter: false);
            if (token != _animToken)
                yield break;

            yield return ShowRoutine(definition, hasDurationOverride, durationOverride, token, contentEntryIndex, blockInputOverride);
        }

        private IEnumerator ShowRoutine(
            PopupDefinition definition,
            bool hasDurationOverride,
            float durationOverride,
            int token,
            int contentEntryIndex,
            bool? blockInputOverride)
        {
            ApplyDefinitionToUi(definition, contentEntryIndex);

            bool block = GetBlockInput(definition, blockInputOverride);

            _root.style.display = DisplayStyle.Flex;
            _currentDefinition = definition;

            float dur = animationDuration > 0f ? animationDuration : FadeSeconds;
            float slide = SlidePixels;

            SetInteractable(block);

            float fromOpacity = 0f;
            float toOpacity = 1f;
            Vector2 off = GetSlideOffset(definition.anchor, slide);
            _panel.style.opacity = fromOpacity;
            _panel.style.translate = new Translate(off.x, off.y);

            float backdropTarget = block ? 0.55f : 0f;
            if (_backdrop != null)
                _backdrop.style.opacity = 0f;

            float t = 0f;
            while (t < dur)
            {
                if (token != _animToken)
                    yield break;

                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / dur);
                float ease = 1f - Mathf.Pow(1f - a, 3f);

                _panel.style.opacity = Mathf.Lerp(fromOpacity, toOpacity, ease);
                _panel.style.translate = new Translate(
                    Mathf.Lerp(off.x, 0f, ease),
                    Mathf.Lerp(off.y, 0f, ease));

                if (_backdrop != null && block)
                    _backdrop.style.opacity = Mathf.Lerp(0f, backdropTarget, ease);

                yield return null;
            }

            if (token != _animToken)
                yield break;

            _panel.style.opacity = 1f;
            _panel.style.translate = new Translate(0f, 0f);
            if (_backdrop != null && block)
                _backdrop.style.opacity = backdropTarget;

            _isOpen = true;
            OnPopupShown?.Invoke(definition);

            if (!definition.autoClose)
            {
                _routine = null;
                yield break;
            }

            float wait = hasDurationOverride && durationOverride >= 0f
                ? durationOverride
                : definition.defaultDuration;

            if (wait > 0f)
            {
                float elapsed = 0f;
                while (elapsed < wait)
                {
                    if (token != _animToken)
                        yield break;

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (token != _animToken)
                    yield break;
            }

            yield return HideRoutine(token, dequeueAfter: true);
        }

        /// <summary>
        /// Same visible popup asset, new localized row — swap copy and restart auto-close without hide/show animation.
        /// </summary>
        private IEnumerator ContentRefreshRoutine(
            PopupDefinition definition,
            bool hasDurationOverride,
            float durationOverride,
            int contentEntryIndex,
            int token,
            bool? blockInputOverride)
        {
            ApplyDefinitionToUi(definition, contentEntryIndex);

            bool block = GetBlockInput(definition, blockInputOverride);
            SetInteractable(block);

            if (!definition.autoClose)
            {
                _routine = null;
                yield break;
            }

            float wait = hasDurationOverride && durationOverride >= 0f
                ? durationOverride
                : definition.defaultDuration;

            if (wait > 0f)
            {
                float elapsed = 0f;
                while (elapsed < wait)
                {
                    if (token != _animToken)
                        yield break;

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (token != _animToken)
                    yield break;
            }

            yield return HideRoutine(token, dequeueAfter: true);
        }

        private IEnumerator HideRoutine(int token, bool dequeueAfter)
        {
            if (_panel == null)
                yield break;

            float dur = animationDuration > 0f ? animationDuration : FadeSeconds;
            float slide = SlidePixels;

            PopupDefinition def = _currentDefinition;
            Vector2 off = def != null ? GetSlideOffset(def.anchor, slide) : Vector2.zero;

            float start = _panel.resolvedStyle.opacity;
            float backdropStart = _backdrop != null ? _backdrop.resolvedStyle.opacity : 0f;
            float t = 0f;
            while (t < dur)
            {
                if (token != _animToken)
                    yield break;

                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / dur);
                // Match ShowRoutine: progress 0→1 must drive opacity start→0 (Hide used Pow(1-a,3) as lerp weight,
                // which inverted the curve and brought the panel back to full opacity on the last frames).
                float u = 1f - Mathf.Pow(1f - a, 3f);

                _panel.style.opacity = Mathf.Lerp(start, 0f, u);
                _panel.style.translate = new Translate(
                    Mathf.Lerp(0f, off.x, u),
                    Mathf.Lerp(0f, off.y, u));

                if (_backdrop != null)
                    _backdrop.style.opacity = Mathf.Lerp(backdropStart, 0f, u);

                yield return null;
            }

            if (token != _animToken)
                yield break;

            SnapHidden(dequeueAfter);
        }

        private void SnapHidden(bool dequeueAfter = true)
        {
            CleanupTransient();

            if (_root != null)
                _root.style.display = DisplayStyle.None;

            if (_panel != null)
            {
                _panel.style.opacity = 0f;
                _panel.style.translate = new Translate(0f, 0f);
            }

            if (_backdrop != null)
                _backdrop.style.opacity = 0f;

            SetInteractable(false);
            _isOpen = false;
            _currentDefinition = null;
            _routine = null;

            OnPopupHidden?.Invoke();

            if (dequeueAfter)
                TryDequeueNext();
        }

        private void TryDequeueNext()
        {
            if (_queue.Count == 0)
                return;

            PendingPopup next = _queue.Dequeue();
            ShowInternal(next.Definition, next.HasDurationOverride, next.DurationOverride, false, next.ContentEntryIndex, next.BlockInputOverride);
        }

        private static bool GetBlockInput(PopupDefinition def, bool? overrideBlock)
        {
            return overrideBlock ?? def.blockInput;
        }

        private void CleanupTransient()
        {
            if (_transientDefinition != null)
            {
                Destroy(_transientDefinition);
                _transientDefinition = null;
            }
        }

        private void SetInteractable(bool block)
        {
            if (_backdrop != null)
            {
                _backdrop.pickingMode = block ? PickingMode.Position : PickingMode.Ignore;
                _backdrop.style.display = block ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private static Vector2 GetSlideOffset(PopupAnchor anchor, float slide)
        {
            return anchor switch
            {
                PopupAnchor.TopRight => new Vector2(slide, -slide),
                PopupAnchor.TopCenter => new Vector2(0f, -slide),
                PopupAnchor.BottomCenter => new Vector2(0f, slide),
                PopupAnchor.Center => new Vector2(0f, slide * 0.5f),
                _ => Vector2.zero
            };
        }

        private void ApplyDefinitionToUi(PopupDefinition def, int contentEntryIndex = -1)
        {
            def.EnsureContentMigrated();

            LocalizationService loc = localizationService != null ? localizationService : LocalizationService.Instance;

            int variantIndex = contentEntryIndex >= 0 ? contentEntryIndex : 0;
            PopupContentVariant variant = GetContentVariant(def, variantIndex);

            string title;
            string body;

            if (loc != null)
            {
                loc.GetPopupVariantText(variant, out title, out body);
            }
            else
            {
                FallbackVariantText(variant, out title, out body);
            }

            if (_titleLabel != null)
                _titleLabel.text = title ?? string.Empty;

            if (_messageLabel != null)
                _messageLabel.text = body ?? string.Empty;

            ApplyTypeClasses(def.type);
            ApplyAnchorClasses(def.anchor);

            if (_panel != null)
            {
                if (!string.IsNullOrEmpty(_lastCustomUssClass))
                    _panel.RemoveFromClassList(_lastCustomUssClass);

                _lastCustomUssClass = def.customUssClass;
                if (!string.IsNullOrEmpty(_lastCustomUssClass))
                    _panel.AddToClassList(_lastCustomUssClass);
            }

            bool showIcon = def.icon != null;
            if (_iconWrap != null)
                _iconWrap.style.display = showIcon ? DisplayStyle.Flex : DisplayStyle.None;

            if (_iconImage != null && def.icon != null)
                _iconImage.sprite = def.icon;

            if (_closeButton != null)
                _closeButton.style.display = DisplayStyle.None;
        }

        private static PopupContentVariant GetContentVariant(PopupDefinition def, int variantIndex)
        {
            if (def?.contentVariants == null || def.contentVariants.Count == 0)
                return new PopupContentVariant();

            if (variantIndex < 0)
                variantIndex = 0;
            if (variantIndex >= def.contentVariants.Count)
                variantIndex = def.contentVariants.Count - 1;

            return def.contentVariants[variantIndex] ?? new PopupContentVariant();
        }

        private static void FallbackVariantText(PopupContentVariant variant, out string title, out string message)
        {
            title = string.Empty;
            message = string.Empty;
            if (variant?.lines == null || variant.lines.Count == 0)
                return;

            for (int i = 0; i < variant.lines.Count; i++)
            {
                PopupLocalizedLine line = variant.lines[i];
                if (line == null)
                    continue;
                if (!string.IsNullOrEmpty(line.title) || !string.IsNullOrEmpty(line.message))
                {
                    title = line.title ?? string.Empty;
                    message = line.message ?? string.Empty;
                    return;
                }
            }

            PopupLocalizedLine first = variant.lines[0];
            if (first != null)
            {
                title = first.title ?? string.Empty;
                message = first.message ?? string.Empty;
            }
        }

        private void ApplyTypeClasses(PopupType type)
        {
            if (_panel == null)
                return;

            string[] all =
            {
                "popup--info", "popup--warning", "popup--error", "popup--success", "popup--training"
            };

            for (int i = 0; i < all.Length; i++)
                _panel.RemoveFromClassList(all[i]);

            string add = type switch
            {
                PopupType.Info => "popup--info",
                PopupType.Warning => "popup--warning",
                PopupType.Error => "popup--error",
                PopupType.Success => "popup--success",
                PopupType.Training => "popup--training",
                _ => "popup--info"
            };

            _panel.AddToClassList(add);
        }

        private void ApplyAnchorClasses(PopupAnchor anchor)
        {
            VisualElement target = _anchorWrap != null ? _anchorWrap : _panel;
            if (target == null)
                return;

            target.RemoveFromClassList("anchor-top-right");
            target.RemoveFromClassList("anchor-top-center");
            target.RemoveFromClassList("anchor-bottom-center");
            target.RemoveFromClassList("anchor-center");

            string cls = anchor switch
            {
                PopupAnchor.TopRight => "anchor-top-right",
                PopupAnchor.TopCenter => "anchor-top-center",
                PopupAnchor.BottomCenter => "anchor-bottom-center",
                PopupAnchor.Center => "anchor-center",
                _ => "anchor-top-right"
            };

            target.AddToClassList(cls);
        }
    }
}
