using System;
using System.Collections;
using System.Globalization;
using FireExtinguisher.Core;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.UIElements;
using Woi.Events;
using WOI.Modules.SDK;
using Woi.UI.Popups.Localization;

namespace Woi.UI
{
    /// <summary>
    /// VR nozzle HUD rig: billboarding, V-Shape layout ve PC ile aynı ScriptableEvent köprüsünden
    /// Main + Telemetry UI Toolkit verilerini günceller (ayrı presenter gerekmez).
    /// </summary>
    [AddComponentMenu("Woi/UI/VR Nozzle HUD Manager")]
    public class VRNozzleHUDManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("HUD'nin takip edeceği Transform. (VRExtinguisherPinPuller tarafından otomatik atanır)")]
        public Transform nozzleTransform;

        [Tooltip("VR Headset Kamerası. Boş bırakılırsa otomatik olarak Camera.main atanır.")]
        public Transform cameraTransform;

        [Header("HUD Panels")]
        [Tooltip("Tüp kapasitesini gösteren ana HUD paneli (Transform; altında UIDocument)")]
        public Transform mainHUD;

        [Tooltip("Telemetry HUD paneli (Transform; altında UIDocument)")]
        public Transform telemetryHUD;

        [Header("UI Toolkit (opsiyonel)")]
        [Tooltip("Boşsa mainHUD / telemetryHUD üzerinden GetComponentInChildren<UIDocument> aranır.")]
        [SerializeField] private UIDocument mainHudDocumentOverride;
        [SerializeField] private UIDocument telemetryHudDocumentOverride;

        [Header("SO Event Bridge — PC ExtinguisherHUDController ile aynı asset'ler")]
        [SerializeField] private ScriptableEventInt _capacityEvent;
        [SerializeField] private ScriptableEventExtinguisherChangedEvent _extinguisherChangedEvent;
        [SerializeField] private ScriptableEventNoParam _sessionEndedEvent;

        [Header("Telemetry — mesafe eşikleri")]
        [SerializeField, Min(0.01f)] private float maxDisplayedDistance = 5f;
        [SerializeField, Min(0.01f)] private float idealDistanceMin = 2f;
        [SerializeField, Min(0.01f)] private float idealDistanceMax = 4f;
        [SerializeField, Min(0.01f)] private float tooCloseDistance = 1.2f;

        [Header("Positioning & Billboarding")]
        [Tooltip("HUD'nin Nozzle'ın ne kadar yukarısında belireceği (Metre cinsinden)")]
        public float heightOffset = 0.15f;

        [Tooltip("Billboarding yaparken rotasyon düzeltmesi (UI ters veya yamuk durursa ayarlayın)")]
        public Vector3 rotationOffset = Vector3.zero;

        [Header("V-Shape — açık panel offsetleri (rig local space)")]
        [SerializeField] private Vector3 mainHudOpenLocalPosition = new Vector3(-0.15f, 0f, 0f);
        [SerializeField] private Vector3 telemetryHudOpenLocalPosition = new Vector3(0.15f, 0f, 0f);
        [SerializeField] private Vector3 mainHudOpenEuler = new Vector3(0f, 15f, 0f);
        [SerializeField] private Vector3 telemetryHudOpenEuler = new Vector3(0f, -15f, 0f);

        [Header("Animation Settings")]
        public float animationSpeed = 8f;

        [Header("Dynamic State")]
        public bool isHUDActive;

        private ExtinguisherController _subscribedController;
        private ExtinguisherController _controllerFromSnap;
        private FireSource _activeFireSource;
        private bool _isExtinguishing;
        private float _lastExtinguishTime;
        public float hideDelayAfterStop = 1.5f;

        private ExtinguishResult _lastSprayResult;

        // Main HUD verisi (ExtinguisherHUDController ile uyumlu)
        private float _currentCapacity = 100f;
        private float _remainingTime;
        private bool _isEquipped;
        private string _equippedName = string.Empty;
        private string _equippedSubtitle = string.Empty;
        private float _totalDurationSeconds;
        private bool _pinPulled;
        private int _lastCapacityEventFrame = -1;

        // UI refs
        private UIDocument _mainDoc;
        private UIDocument _telemetryDoc;
        private VisualElement _mainRoot;
        private Label _titleText;
        private Label _pinStatusText;
        private Label _capacityText;
        private VisualElement _capacityFill;
        private Label _timeText;
        private Label _timeUnitLabel;
        private VisualElement _telemetryRoot;
        private Label _distanceText;
        private Label _distanceUnit;
        private VisualElement _distanceBarFill;
        private Label _distanceStatus;
        private Label _targetZoneText;
        private Label _targetStatusBadge;
        private bool _mainUiBound;
        private bool _telemetryUiBound;
        private string _lastHudLanguageForTimeUnit = string.Empty;
        private Coroutine _deferredBindRoutine;

        private readonly Vector3 _mainHUDPosOff = Vector3.zero;
        private readonly Quaternion _mainHUDRotOff = Quaternion.identity;
        private readonly Vector3 _telHUDScaleOff = Vector3.zero;

        private void Awake()
        {
            if (!ServiceLocator.IsRegistered<VRNozzleHUDManager>())
                ServiceLocator.Register<VRNozzleHUDManager>(this);
        }

        private void OnEnable()
        {
            if (_extinguisherChangedEvent != null)
                _extinguisherChangedEvent.OnRaised += OnExtinguisherChanged;
            if (_capacityEvent != null)
                _capacityEvent.OnRaised += OnCapacityChanged;
            if (_sessionEndedEvent != null)
                _sessionEndedEvent.OnRaised += OnSessionEnded;

            if (_deferredBindRoutine == null)
                _deferredBindRoutine = StartCoroutine(DeferredBindDocumentsRoutine());
        }

        private void OnDisable()
        {
            if (_deferredBindRoutine != null)
            {
                StopCoroutine(_deferredBindRoutine);
                _deferredBindRoutine = null;
            }

            if (_extinguisherChangedEvent != null)
                _extinguisherChangedEvent.OnRaised -= OnExtinguisherChanged;
            if (_capacityEvent != null)
                _capacityEvent.OnRaised -= OnCapacityChanged;
            if (_sessionEndedEvent != null)
                _sessionEndedEvent.OnRaised -= OnSessionEnded;

            UnhookController();
            _mainUiBound = false;
            _telemetryUiBound = false;
        }

        private void OnDestroy()
        {
            UnhookController();

            if (ServiceLocator.IsRegistered<VRNozzleHUDManager>())
                ServiceLocator.Unregister<VRNozzleHUDManager>();
        }

        private IEnumerator DeferredBindDocumentsRoutine()
        {
            const int maxFrames = 120;
            for (int i = 0; i < maxFrames && isActiveAndEnabled; i++)
            {
                TryResolveDocuments();
                if (TryBindMainUi() && TryBindTelemetryUi())
                    break;
                yield return null;
            }

            _deferredBindRoutine = null;
            RefreshMainHud();
        }

        private void TryResolveDocuments()
        {
            if (_mainDoc == null)
            {
                if (mainHudDocumentOverride != null)
                    _mainDoc = mainHudDocumentOverride;
                else if (mainHUD != null)
                    _mainDoc = mainHUD.GetComponentInChildren<UIDocument>(true);
            }

            if (_telemetryDoc == null)
            {
                if (telemetryHudDocumentOverride != null)
                    _telemetryDoc = telemetryHudDocumentOverride;
                else if (telemetryHUD != null)
                    _telemetryDoc = telemetryHUD.GetComponentInChildren<UIDocument>(true);
            }
        }

        private bool TryBindMainUi()
        {
            if (_mainDoc == null || _mainDoc.rootVisualElement == null)
                return false;

            VisualElement root = _mainDoc.rootVisualElement;
            _mainRoot = root.Q<VisualElement>("EstinguisherHUDVR") ?? root;
            _titleText = root.Q<Label>("extinguisher-title");
            _pinStatusText = root.Q<Label>("pin-status-text");
            _capacityText = root.Q<Label>("capacity-text");
            _capacityFill = root.Q<VisualElement>("progress-bar-fill");
            _timeText = root.Q<Label>("time-text");
            _timeUnitLabel = root.Q<Label>("time-unit");

            if (_capacityText == null)
                return false;

            _mainUiBound = true;
            return true;
        }

        private bool TryBindTelemetryUi()
        {
            if (_telemetryDoc == null || _telemetryDoc.rootVisualElement == null)
                return false;

            VisualElement root = _telemetryDoc.rootVisualElement;
            _telemetryRoot = root.Q<VisualElement>("hud-container") ?? root;
            _distanceText = root.Q<Label>("distance-text");
            _distanceUnit = root.Q<Label>("distance-unit");
            _distanceBarFill = root.Q<VisualElement>("distance-bar-fill");
            _distanceStatus = root.Q<Label>("distance-status");
            _targetZoneText = root.Q<Label>("target-zone-text");
            _targetStatusBadge = root.Q<Label>("target-status-badge");

            if (_distanceText == null)
                return false;

            _telemetryUiBound = true;
            return true;
        }

        private void Start()
        {
            if (telemetryHUD != null)
                telemetryHUD.localScale = Vector3.zero;

            if (mainHUD != null)
            {
                mainHUD.localScale = Vector3.zero;
                mainHUD.localPosition = _mainHUDPosOff;
                mainHUD.localRotation = _mainHUDRotOff;
            }
        }

        private void LateUpdate()
        {
            if (!_mainUiBound || !_telemetryUiBound)
            {
                TryResolveDocuments();
                if (!_mainUiBound)
                    TryBindMainUi();
                if (!_telemetryUiBound)
                    TryBindTelemetryUi();
            }

            if (cameraTransform == null)
            {
                AppMode currentMode = AppMode.PC;
                if (ServiceLocator.TryGet<IFirePortingPlatformSource>(out var portingSource) && portingSource != null)
                    currentMode = portingSource.CurrentMode;

                if (currentMode == AppMode.XR)
                {
                    if (ServiceLocator.TryGet<Woi.Player.IXRPlayerService>(out var xrService) && xrService.PlayerCamera != null)
                        cameraTransform = xrService.PlayerCamera.transform;
                }
                else
                {
                    if (ServiceLocator.TryGet<Woi.Player.IPlayerService>(out var pcService) && pcService.playerCamera != null)
                        cameraTransform = pcService.playerCamera.transform;
                }

                if (cameraTransform == null && Camera.main != null)
                    cameraTransform = Camera.main.transform;

                if (cameraTransform == null)
                    return;
            }

            UpdateHUDPositionAndRotation();

            if (_isExtinguishing)
                _lastExtinguishTime = Time.time;
            else if (isHUDActive)
            {
                if (Time.time - _lastExtinguishTime >= hideDelayAfterStop)
                {
                    isHUDActive = false;
                    _activeFireSource = null;
                }
            }

            UpdateHUDAnimations();
            RefreshTelemetryHud();

            if (_mainUiBound && _timeUnitLabel != null)
            {
                string code = ResolveHudLanguageCode();
                if (!string.Equals(code, _lastHudLanguageForTimeUnit, StringComparison.OrdinalIgnoreCase))
                {
                    _lastHudLanguageForTimeUnit = code;
                    _timeUnitLabel.text = IsHudTurkish() ? "sn" : "s";
                }
            }
        }

        private void UpdateHUDPositionAndRotation()
        {
            if (nozzleTransform == null) return;

            transform.position = nozzleTransform.position + Vector3.up * heightOffset;

            Vector3 lookDir = transform.position - cameraTransform.position;
            if (lookDir != Vector3.zero)
            {
                Quaternion targetLookRot = Quaternion.LookRotation(lookDir) * Quaternion.Euler(rotationOffset);
                transform.rotation = targetLookRot;
            }
        }

        private void UpdateHUDAnimations()
        {
            float dt = Time.deltaTime * animationSpeed;

            if (nozzleTransform == null)
            {
                if (mainHUD != null) mainHUD.localScale = Vector3.Lerp(mainHUD.localScale, Vector3.zero, dt);
                if (telemetryHUD != null) telemetryHUD.localScale = Vector3.Lerp(telemetryHUD.localScale, Vector3.zero, dt);
                return;
            }

            if (mainHUD != null)
                mainHUD.localScale = Vector3.Lerp(mainHUD.localScale, Vector3.one, dt);

            if (isHUDActive)
            {
                if (mainHUD != null)
                {
                    mainHUD.localPosition = Vector3.Lerp(mainHUD.localPosition, mainHudOpenLocalPosition, dt);
                    mainHUD.localRotation = Quaternion.Slerp(mainHUD.localRotation, Quaternion.Euler(mainHudOpenEuler), dt);
                }

                if (telemetryHUD != null)
                {
                    telemetryHUD.localScale = Vector3.Lerp(telemetryHUD.localScale, Vector3.one, dt);
                    telemetryHUD.localPosition = Vector3.Lerp(telemetryHUD.localPosition, telemetryHudOpenLocalPosition, dt);
                    telemetryHUD.localRotation = Quaternion.Slerp(telemetryHUD.localRotation, Quaternion.Euler(telemetryHudOpenEuler), dt);
                }
            }
            else
            {
                if (mainHUD != null)
                {
                    mainHUD.localPosition = Vector3.Lerp(mainHUD.localPosition, _mainHUDPosOff, dt);
                    mainHUD.localRotation = Quaternion.Slerp(mainHUD.localRotation, _mainHUDRotOff, dt);
                }

                if (telemetryHUD != null)
                {
                    telemetryHUD.localScale = Vector3.Lerp(telemetryHUD.localScale, _telHUDScaleOff, dt);
                    telemetryHUD.localPosition = Vector3.Lerp(telemetryHUD.localPosition, Vector3.zero, dt);
                    telemetryHUD.localRotation = Quaternion.Slerp(telemetryHUD.localRotation, Quaternion.identity, dt);
                }
            }
        }

        public void SetNozzle(Transform newNozzle, ExtinguisherController extinguisherController = null)
        {
            nozzleTransform = newNozzle;
            _controllerFromSnap = newNozzle != null ? extinguisherController : null;
            HookToController();
        }

        private void HookToController()
        {
            UnhookController();

            if (nozzleTransform == null)
                return;

            _subscribedController = _controllerFromSnap != null
                ? _controllerFromSnap
                : nozzleTransform.GetComponentInParent<ExtinguisherController>();

            if (_subscribedController == null)
            {
                Debug.LogWarning(
                    "[VRNozzleHUDManager] ExtinguisherController bulunamadı — VR'da SetNozzle(..., controller) kullanın.",
                    this);
                return;
            }

            _subscribedController.OnSprayEvaluated += OnSprayEvaluated;
            _subscribedController.OnSprayStopped += OnSprayStopped;

            SyncDurationAndCapacityFromController();
            RefreshMainHud();
        }

        private void UnhookController()
        {
            if (_subscribedController != null)
            {
                _subscribedController.OnSprayEvaluated -= OnSprayEvaluated;
                _subscribedController.OnSprayStopped -= OnSprayStopped;
                _subscribedController = null;
            }
        }

        /// <summary>
        /// PC ekipman olayındaki süre modeliyle uyumlu: kalan süre ≈ (NormalizedCapacity × MaxCapacity) / ConsumptionRate.
        /// Equip event'i kaçırıldığında veya <c>_totalDurationSeconds</c> 0 kaldığında kalan süreyi doldurur.
        /// </summary>
        private void SyncDurationAndCapacityFromController()
        {
            if (_subscribedController == null)
                return;

            float maxCap = _subscribedController.MaxCapacity;
            float norm = Mathf.Clamp01(_subscribedController.NormalizedCapacity);
            var data = _subscribedController.ExtinguisherData;
            float rate = (data != null && data.ConsumptionRate > 0f) ? data.ConsumptionRate : 1f;
            float safeRate = Mathf.Max(rate, 1e-6f);

            _totalDurationSeconds = maxCap > 0f ? maxCap / safeRate : 0f;
            _currentCapacity = Mathf.Clamp(norm * 100f, 0f, 100f);
            _remainingTime = (norm * maxCap) / safeRate;
            _pinPulled = _subscribedController.IsPinPulled;
        }

        private void OnSprayEvaluated(ExtinguishResult result)
        {
            _lastSprayResult = result;

            if (result.DidHitZone && result.Source != null)
            {
                _activeFireSource = result.Source;
                _isExtinguishing = true;
                isHUDActive = true;
                _lastExtinguishTime = Time.time;
            }

            RefreshTelemetryHud();
        }

        private void OnSprayStopped()
        {
            _isExtinguishing = false;
        }

        // ── ScriptableEvent handlers ─────────────────────────────────────────────

        private void OnExtinguisherChanged(ExtinguisherChangedEvent e)
        {
            TryResolveDocuments();
            if (!_mainUiBound)
                TryBindMainUi();
            if (!_telemetryUiBound)
                TryBindTelemetryUi();

            bool equipped = !string.IsNullOrEmpty(e.itemName);
            if (equipped)
            {
                if (_mainRoot != null)
                    _mainRoot.style.display = DisplayStyle.Flex;
                if (_telemetryRoot != null)
                    _telemetryRoot.style.display = DisplayStyle.Flex;
            }

            _isEquipped = equipped;
            _equippedName = equipped ? e.itemName : string.Empty;
            _equippedSubtitle = equipped ? ExtinguisherChangedEventCompat.TryGetSubtitle(e) : string.Empty;
            _currentCapacity = Mathf.Clamp(e.capacity, 0, 100);
            _totalDurationSeconds = _isEquipped && e.capacity > 0
                ? e.remainingTime / (e.capacity / 100f)
                : 0f;
            _remainingTime = e.remainingTime;
            _lastCapacityEventFrame = -1;
            _pinPulled = e.pinPulled;

            if (equipped && _totalDurationSeconds <= 0f && _subscribedController != null)
                SyncDurationAndCapacityFromController();

            RefreshMainHud();
        }

        private void OnCapacityChanged(int rawCapacity)
        {
            if (_totalDurationSeconds <= 0f && _subscribedController != null)
                SyncDurationAndCapacityFromController();

            _lastCapacityEventFrame = Time.frameCount;
            _currentCapacity = Mathf.Clamp(rawCapacity / 10f, 0f, 100f);
            _remainingTime = (_totalDurationSeconds > 0f)
                ? (_currentCapacity / 100f) * _totalDurationSeconds
                : 0f;
            RefreshMainHud();
        }

        private void OnSessionEnded()
        {
            if (_mainRoot != null)
                _mainRoot.style.display = DisplayStyle.None;
            if (_telemetryRoot != null)
                _telemetryRoot.style.display = DisplayStyle.None;
        }

        private void RefreshMainHud()
        {
            if (!_mainUiBound)
                return;

            CultureInfo culture = HudNumberCulture();

            if (_titleText != null)
            {
                if (_isEquipped && !string.IsNullOrWhiteSpace(_equippedSubtitle))
                    _titleText.text = $"{_equippedName}\n{_equippedSubtitle}";
                else
                    _titleText.text = _isEquipped ? _equippedName : string.Empty;
            }

            if (_pinStatusText != null)
            {
                if (!_isEquipped)
                    _pinStatusText.text = string.Empty;
                else if (_pinPulled)
                    _pinStatusText.text = IsHudTurkish() ? "ÇEKİLDİ" : "PULLED";
                else
                    _pinStatusText.text = IsHudTurkish() ? "ÇEKİLMEDİ" : "NOT PULLED";
            }

            if (_capacityText != null)
                _capacityText.text = $"{_currentCapacity.ToString("F1", culture)}%";

            if (_timeText != null)
                _timeText.text = _remainingTime.ToString("F1", culture);

            if (_timeUnitLabel != null)
                _timeUnitLabel.text = IsHudTurkish() ? "sn" : "s";

            if (_capacityFill != null)
                _capacityFill.style.width = Length.Percent(_currentCapacity);

            ApplyCapacityTextColor(_capacityText, _currentCapacity);
            ApplyCapacityTextColor(_timeText, _currentCapacity);
        }

        private static void ApplyCapacityTextColor(Label label, float capacity)
        {
            if (label == null)
                return;

            label.RemoveFromClassList("text-cyan");
            label.RemoveFromClassList("text-red");
            if (capacity > 20f)
                label.AddToClassList("text-cyan");
            else
                label.AddToClassList("text-red");
        }

        private void RefreshTelemetryHud()
        {
            if (!_telemetryUiBound || !isHUDActive)
                return;

            CultureInfo culture = HudNumberCulture();

            float distanceM;
            if (nozzleTransform != null && _activeFireSource != null)
                distanceM = Vector3.Distance(nozzleTransform.position, _activeFireSource.transform.position);
            else if (_lastSprayResult.DidHitZone)
                distanceM = _lastSprayResult.Distance;
            else
                distanceM = _lastSprayResult.Distance > 0f ? _lastSprayResult.Distance : 0f;

            if (_distanceText != null)
                _distanceText.text = distanceM.ToString("F1", culture);

            if (_distanceUnit != null)
                _distanceUnit.text = "m";

            float normalized = Mathf.Clamp01(distanceM / Mathf.Max(0.01f, maxDisplayedDistance));
            if (_distanceBarFill != null)
                _distanceBarFill.style.width = Length.Percent(normalized * 100f);

            if (_distanceStatus != null)
                _distanceStatus.text = ResolveDistanceStatus(distanceM);

            if (_subscribedController != null && _subscribedController.IsDischarging)
                ApplyAimFromLastResult();
            else
                ClearAimRow();
        }

        private void ClearAimRow()
        {
            if (_targetZoneText != null)
                _targetZoneText.text = "—";
            if (_targetStatusBadge != null)
            {
                _targetStatusBadge.text = string.Empty;
                _targetStatusBadge.style.display = DisplayStyle.None;
            }
        }

        private void ApplyAimFromLastResult()
        {
            ExtinguishResult result = _lastSprayResult;

            if (result.DidHitZone && result.Source != null)
            {
                string label;
                switch (result.Compatibility)
                {
                    case CompatibilityResult.Forbidden:
                        label = Loc("WRONG EXTINGUISHER", "YANLIŞ SÖNDÜRÜCÜ");
                        break;
                    case CompatibilityResult.Neutral:
                        label = result.HitZone != null ? result.HitZone.name : Loc("ZONE", "BÖLGE");
                        break;
                    default:
                        label = result.HitZone != null ? result.HitZone.name : Loc("ON TARGET", "Hedefte");
                        break;
                }

                if (_targetZoneText != null)
                    _targetZoneText.text = label;

                if (_targetStatusBadge != null)
                {
                    _targetStatusBadge.text = BadgeForCompatibility(result.Compatibility);
                    _targetStatusBadge.style.display = string.IsNullOrEmpty(_targetStatusBadge.text)
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
                }

                return;
            }

            if (_targetZoneText != null)
                _targetZoneText.text = LabelForMiss(result.MissReason);

            if (_targetStatusBadge != null)
            {
                _targetStatusBadge.text = BadgeForMiss(result.MissReason);
                _targetStatusBadge.style.display = string.IsNullOrEmpty(_targetStatusBadge.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
        }

        private static string BadgeForCompatibility(CompatibilityResult c)
        {
            switch (c)
            {
                case CompatibilityResult.Effective:
                    return Loc("OK", "UYGUN");
                case CompatibilityResult.Neutral:
                    return Loc("WARN", "UYARI");
                case CompatibilityResult.Forbidden:
                    return Loc("NO", "HAYIR");
                default:
                    return string.Empty;
            }
        }

        private static string BadgeForMiss(SprayMissReason reason)
        {
            switch (reason)
            {
                case SprayMissReason.ZoneAlreadyExtinguished:
                case SprayMissReason.FireAlreadyExtinguished:
                    return Loc("WARN", "UYARI");
                default:
                    return Loc("NO", "HAYIR");
            }
        }

        private static string LabelForMiss(SprayMissReason reason)
        {
            switch (reason)
            {
                case SprayMissReason.OutOfRange:
                    return Loc("OUT OF RANGE", "MENZİL DIŞI");
                case SprayMissReason.NoFireZoneHit:
                    return Loc("NO FIRE ZONE", "YANGIN BÖLGESİ YOK");
                case SprayMissReason.OutsideConeAngle:
                    return Loc("AIM OFF CENTER", "NİŞAN MERKEZ DIŞI");
                case SprayMissReason.ZoneAlreadyExtinguished:
                    return Loc("ZONE OUT", "BÖLGE SÖNDÜ");
                case SprayMissReason.FireAlreadyExtinguished:
                    return Loc("FIRE OUT", "YANGIN SÖNDÜ");
                default:
                    return Loc("ADJUST AIM", "NİŞANI AYARLA");
            }
        }

        private string ResolveDistanceStatus(float distance)
        {
            if (distance < tooCloseDistance)
                return Loc("TOO CLOSE", "ÇOK YAKIN");
            if (distance >= idealDistanceMin && distance <= idealDistanceMax)
                return Loc("IDEAL POSITION", "İDEAL MESAFE");
            if (distance > idealDistanceMax)
                return Loc("TOO FAR", "ÇOK UZAK");
            return Loc("ADJUST RANGE", "MESAFEYİ AYARLA");
        }

        private static bool IsHudTurkish()
        {
            string code = ResolveHudLanguageCode();
            return code == LocalizationService.Turkish || code.StartsWith("tr", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveHudLanguageCode()
        {
            if (ServiceLocator.TryGet<ILocalizationService>(out ILocalizationService iloc) && iloc != null &&
                !string.IsNullOrEmpty(iloc.CurrentLanguage))
                return iloc.CurrentLanguage.Trim().ToLowerInvariant();

            if (LocalizationService.Instance != null && !string.IsNullOrEmpty(LocalizationService.Instance.CurrentLanguage))
                return LocalizationService.Instance.CurrentLanguage.Trim().ToLowerInvariant();

            return LocalizationService.Turkish;
        }

        private static CultureInfo HudNumberCulture() =>
            IsHudTurkish() ? CultureInfo.GetCultureInfo("tr-TR") : CultureInfo.InvariantCulture;

        private static string Loc(string english, string turkish) => IsHudTurkish() ? turkish : english;
    }
}
