using System;
using System.Collections;
using System.Globalization;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.UIElements;
using WOI.Modules.SDK;
using Woi.Events;
using Woi.UI.Popups.Localization;

public class ExtinguisherHUDController : MonoBehaviour
{
    [Header("UI")]
    public UIDocument uiDocument;

    [Header("SO Event Bridge")]
    [Tooltip("ScriptableEventInt raised by ExtinguisherController. Payload: capacity 0–100.")]
    [SerializeField] private ScriptableEventInt _capacityEvent;

    [Tooltip("Raised by PlayerExtinguisherEquipment on equip/drop. Carries name, capacity, duration.")]
    [SerializeField] private ScriptableEventExtinguisherChangedEvent _extinguisherChangedEvent;

    [Tooltip("Raised when the training session ends. Hides the HUD completely.")]
    [SerializeField] private ScriptableEventNoParam _sessionEndedEvent;

    // ── UI element refs ───────────────────────────────────────────────────────

    private Label         _nameText;
    private Label         _subtitleText;
    private Label         _capacityText;
    private VisualElement _capacityFill;
    private Label         _timeText;
    private VisualElement _warningOverlay;
    private Label         _warningText;
    private VisualElement _sprayIconBg;
    private VisualElement _sprayIcon;
    private VisualElement _pinSection;
    private Label         _pinStatusValue;

    private Label         _pinLabel;
    private Label         _capacityLabel;
    private Label         _timeLabelTop;
    private Label         _timeLabelBottom;
    private Label         _timeUnitLabel;

    private string _lastHudLanguageCode = string.Empty;

    private IVisualElementScheduledItem _pulseAnimation;
    private bool _isPulseFaded;

    // ── Runtime state ─────────────────────────────────────────────────────────

    [Header("Live Data (read-only)")]
    public float currentCapacity = 100f; // 0–100 with one decimal (e.g. 78.7)
    public float remainingTime;
    public bool  isSpraying;
    public bool  pinPulled;

    // Derived from equipped ExtinguisherData.ConsumptionRate on equip.
    private float _totalDurationSeconds;

    // Whether an extinguisher is currently in the slot.
    private bool   _isEquipped;
    // Display name of the currently equipped extinguisher.
    private string _equippedName = string.Empty;
    private string _equippedSubtitle = string.Empty;
    // Absolute max capacity in the same units as ConsumptionRate.
    private float  _maxCapacity;

    // Frame on which the last capacity event arrived.
    // If it matches the current frame → discharging this frame.
    private int _lastCapacityEventFrame = -1;

    private bool _uiBound;
    private Coroutine _deferredBindRoutine;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();

        if (_extinguisherChangedEvent != null)
            _extinguisherChangedEvent.OnRaised += HandleExtinguisherChangedEvent;
        else
            Debug.LogWarning("[ExtinguisherHUDController] No ScriptableEventExtinguisherChangedEvent assigned.", this);

        if (_capacityEvent != null)
            _capacityEvent.OnRaised += HandleCapacityChanged;
        else
            Debug.LogWarning("[ExtinguisherHUDController] No ScriptableEventInt assigned.", this);

        if (_sessionEndedEvent != null)
            _sessionEndedEvent.OnRaised += HandleSessionEnded;

        TryBindUi();
        RefreshHUD();
    }

    private void LateUpdate()
    {
        if (!_uiBound)
            return;

        string code = ResolveHudLanguageCode();
        if (string.Equals(code, _lastHudLanguageCode, StringComparison.OrdinalIgnoreCase))
            return;

        _lastHudLanguageCode = code;
        ApplyHudStaticLabels();
        ApplyHudNumberTexts();
        UpdatePinSection();
        UpdateWarningOverlay();
    }

    private void OnDisable()
    {
        if (_deferredBindRoutine != null)
        {
            StopCoroutine(_deferredBindRoutine);
            _deferredBindRoutine = null;
        }

        _pulseAnimation?.Pause();
        _pulseAnimation = null;

        _uiBound = false;

        if (_extinguisherChangedEvent != null)
            _extinguisherChangedEvent.OnRaised -= HandleExtinguisherChangedEvent;

        if (_capacityEvent != null)
            _capacityEvent.OnRaised -= HandleCapacityChanged;

        if (_sessionEndedEvent != null)
            _sessionEndedEvent.OnRaised -= HandleSessionEnded;
    }

    private void Update()
    {
        bool nowSpraying = (_lastCapacityEventFrame == Time.frameCount) && currentCapacity > 0f;

        if (nowSpraying != isSpraying)
        {
            isSpraying = nowSpraying;
            UpdateIconState();
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandleExtinguisherChangedEvent(ExtinguisherChangedEvent e)
    {
        TryBindUi();

        _isEquipped   = !string.IsNullOrEmpty(e.itemName);
        _equippedName = _isEquipped ? e.itemName : string.Empty;
        _equippedSubtitle = _isEquipped ? ExtinguisherChangedEventCompat.TryGetSubtitle(e) : string.Empty;
        _maxCapacity  = e.maxCapacity;

        // e.capacity is still 0–100 int (sent on equip, before spray starts).
        // Convert to float percentage directly.
        currentCapacity = Mathf.Clamp(e.capacity, 0, 100);

        // Total duration = remainingTime / normalizedCapacity
        _totalDurationSeconds = _isEquipped && e.capacity > 0
            ? e.remainingTime / (e.capacity / 100f)
            : 0f;

        remainingTime = e.remainingTime;

        _lastCapacityEventFrame = -1;
        isSpraying              = false;
        pinPulled               = e.pinPulled;

        RefreshHUD();
    }

    private void HandleCapacityChanged(int rawCapacity)
    {
        _lastCapacityEventFrame = Time.frameCount;

        // rawCapacity is 0–1000 (controller sends normalizedCapacity * 1000).
        // Divide by 10 to get 0–100.0 with one decimal (e.g. 787 → 78.7).
        currentCapacity = Mathf.Clamp(rawCapacity / 10f, 0f, 100f);
        remainingTime   = (currentCapacity / 100f) * _totalDurationSeconds;

        RefreshHUD();
    }

    private void HandleSessionEnded()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.style.display = DisplayStyle.None;
            Debug.Log("[ExtinguisherHUDController] Session Ended event received. HUD is now invisible.");
        }
    }

    // ── HUD update ────────────────────────────────────────────────────────────

    private void RefreshHUD()
    {
        TryBindUi();

        ApplyHudNumberTexts();

        if (_capacityFill != null) _capacityFill.style.width = Length.Percent(currentCapacity); // 0–100f

        UpdateNameText();
        UpdateColors(currentCapacity);
        UpdateIconState();
        UpdatePinSection();

        UpdateWarningOverlay();
    }

    void ApplyHudNumberTexts()
    {
        CultureInfo culture = HudNumberCulture();
        if (_capacityText != null) _capacityText.text = $"{currentCapacity.ToString("F1", culture)}%";
        if (_timeText != null) _timeText.text = remainingTime.ToString("F1", culture);
    }

    private void UpdateNameText()
    {
        if (_nameText != null)
            _nameText.text = _isEquipped ? _equippedName : string.Empty;

        if (_subtitleText != null)
            _subtitleText.text = _isEquipped && !string.IsNullOrWhiteSpace(_equippedSubtitle) ? _equippedSubtitle : string.Empty;
    }

    private void TryBindUi()
    {
        if (_uiBound)
            return;

        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
            return;

        VisualElement root = uiDocument.rootVisualElement;
        if (root == null)
        {
            if (_deferredBindRoutine == null && isActiveAndEnabled)
                _deferredBindRoutine = StartCoroutine(DeferredBindRoutine());
            return;
        }

        BindFromRoot(root);
        if (_nameText != null)
        {
            _uiBound = true;
            ApplyHudStaticLabels();
            _lastHudLanguageCode = ResolveHudLanguageCode();
        }
        else if (_deferredBindRoutine == null && isActiveAndEnabled)
            _deferredBindRoutine = StartCoroutine(DeferredBindRoutine());
    }

    private IEnumerator DeferredBindRoutine()
    {
        const int maxFrames = 120;
        for (int i = 0; i < maxFrames && isActiveAndEnabled; i++)
        {
            if (uiDocument == null)
                break;

            VisualElement root = uiDocument.rootVisualElement;
            if (root != null)
            {
                BindFromRoot(root);
                if (_nameText != null)
                {
                    _uiBound = true;
                    ApplyHudStaticLabels();
                    _lastHudLanguageCode = ResolveHudLanguageCode();
                    break;
                }
            }

            yield return null;
        }

        _deferredBindRoutine = null;

        if (_uiBound)
            RefreshHUD();
    }

    private void BindFromRoot(VisualElement root)
    {
        _nameText       = root.Q<Label>("extinguisher-title");
        _subtitleText   = root.Q<Label>("extinguisher-subtitle");
        _capacityText   = root.Q<Label>("capacity-value");
        _capacityFill   = root.Q<VisualElement>("progress-bar-fill");
        _timeText       = root.Q<Label>("time-value");
        _warningOverlay = root.Q<VisualElement>("empty-warning");
        _warningText    = root.Q<Label>("warning-text");
        _sprayIconBg    = root.Q<VisualElement>("spray-icon-bg");
        _sprayIcon      = root.Q<VisualElement>("spray-icon");
        _pinSection     = root.Q<VisualElement>("pin-section");
        _pinStatusValue = root.Q<Label>("pin-status-value");
        _pinLabel       = root.Q<Label>("pin-label");
        _capacityLabel  = root.Q<Label>("capacity-label");
        _timeLabelTop    = root.Q<Label>("time-label-top");
        _timeLabelBottom = root.Q<Label>("time-label-bottom");
        _timeUnitLabel   = root.Q<Label>("time-unit");

        EnsurePulseAnimation();

        if (_nameText == null)
            Debug.LogWarning(
                "[ExtinguisherHUDController] UXML'de 'extinguisher-title' yok veya kök henüz yüklenmedi — başlık güncellenmez.",
                this);
    }

    private void EnsurePulseAnimation()
    {
        if (_sprayIconBg == null)
            return;

        _pulseAnimation?.Pause();
        _pulseAnimation = _sprayIconBg.schedule.Execute(() =>
        {
            if (isSpraying)
            {
                _isPulseFaded = !_isPulseFaded;
                _sprayIconBg.style.opacity = _isPulseFaded ? 0.4f : 1f;
            }
            else
            {
                _sprayIconBg.style.opacity = 1f;
            }
        }).Every(400);
    }

    private void UpdatePinSection()
    {
        if (_pinSection == null) return;

        if (!_isEquipped)
        {
            _pinSection.style.display = DisplayStyle.None;
            _pinSection.RemoveFromClassList("pin-section--ready");
            return;
        }

        _pinSection.style.display = DisplayStyle.Flex;

        if (_pinStatusValue != null)
        {
            if (pinPulled)
            {
                _pinStatusValue.text = IsHudTurkish() ? "ÇEKİLDİ" : "PULLED";
                _pinStatusValue.RemoveFromClassList("text-amber");
                _pinStatusValue.RemoveFromClassList("text-red");
                _pinStatusValue.AddToClassList("text-emerald");
            }
            else
            {
                _pinStatusValue.text = IsHudTurkish() ? "ÇEKİLMEDİ" : "NOT PULLED";
                _pinStatusValue.RemoveFromClassList("text-emerald");
                _pinStatusValue.RemoveFromClassList("text-red");
                _pinStatusValue.AddToClassList("text-amber");
            }
        }

        if (pinPulled)
            _pinSection.AddToClassList("pin-section--ready");
        else
            _pinSection.RemoveFromClassList("pin-section--ready");
    }

    private void UpdateWarningOverlay()
    {
        if (_warningOverlay == null) return;

        bool showNoExtinguisher = !_isEquipped;
        bool showDepleted       = _isEquipped && currentCapacity <= 0f;
        bool show               = showNoExtinguisher || showDepleted;

        _warningOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

        if (_warningText == null) return;

        if (showNoExtinguisher)
            _warningText.text = IsHudTurkish() ? "TÜP YOK!" : "NO CYLINDER!";
        else if (showDepleted)
            _warningText.text = IsHudTurkish() ? "TÜP BOŞALDI!" : "CYLINDER EMPTY!";
    }

    private void UpdateIconState()
    {
        if (_sprayIconBg == null || _sprayIcon == null)
            return;

        _sprayIconBg.RemoveFromClassList("icon-bg-idle");
        _sprayIconBg.RemoveFromClassList("icon-bg-active");
        _sprayIcon.RemoveFromClassList("icon-tint-idle");
        _sprayIcon.RemoveFromClassList("icon-tint-active");

        if (isSpraying)
        {
            _sprayIconBg.AddToClassList("icon-bg-active");
            _sprayIcon.AddToClassList("icon-tint-active");
        }
        else
        {
            _sprayIconBg.AddToClassList("icon-bg-idle");
            _sprayIcon.AddToClassList("icon-tint-idle");
        }
    }

    private void UpdateColors(float capacity)
    {
        if (_capacityFill == null || _capacityText == null || _timeText == null)
            return;

        _capacityFill.RemoveFromClassList("bg-emerald");
        _capacityFill.RemoveFromClassList("bg-amber");
        _capacityFill.RemoveFromClassList("bg-red");
        _capacityText.RemoveFromClassList("text-emerald");
        _capacityText.RemoveFromClassList("text-amber");
        _capacityText.RemoveFromClassList("text-red");
        _timeText.RemoveFromClassList("text-emerald");
        _timeText.RemoveFromClassList("text-amber");
        _timeText.RemoveFromClassList("text-red");

        string bg   = capacity > 50f ? "bg-emerald"  : capacity > 20f ? "bg-amber"  : "bg-red";
        string text = capacity > 50f ? "text-emerald" : capacity > 20f ? "text-amber" : "text-red";

        _capacityFill.AddToClassList(bg);
        _capacityText.AddToClassList(text);
        _timeText.AddToClassList(text);
    }

    static string ResolveHudLanguageCode()
    {
        if (ServiceLocator.TryGet<ILocalizationService>(out ILocalizationService iloc) && iloc != null && !string.IsNullOrEmpty(iloc.CurrentLanguage))
            return iloc.CurrentLanguage.Trim().ToLowerInvariant();

        if (LocalizationService.Instance != null && !string.IsNullOrEmpty(LocalizationService.Instance.CurrentLanguage))
            return LocalizationService.Instance.CurrentLanguage.Trim().ToLowerInvariant();

        return LocalizationService.Turkish;
    }

    static bool IsHudTurkish()
    {
        string code = ResolveHudLanguageCode();
        return code == LocalizationService.Turkish || code.StartsWith("tr", StringComparison.Ordinal);
    }

    static CultureInfo HudNumberCulture() =>
        IsHudTurkish() ? CultureInfo.GetCultureInfo("tr-TR") : CultureInfo.InvariantCulture;

    void ApplyHudStaticLabels()
    {
        bool tr = IsHudTurkish();
        if (_pinLabel != null)
            _pinLabel.text = tr ? "GÜVENLİK İĞNESİ" : "SAFETY PIN";

        if (_capacityLabel != null)
            _capacityLabel.text = tr ? "KAPASİTE" : "CAPACITY";

        if (_timeLabelTop != null)
            _timeLabelTop.text = tr ? "KALAN" : "REMAINING";

        if (_timeLabelBottom != null)
            _timeLabelBottom.text = tr ? "SÜRE" : "TIME";

        if (_timeUnitLabel != null)
            _timeUnitLabel.text = tr ? "sn" : "s";
    }
}
