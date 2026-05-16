using System;
using System.Globalization;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.UIElements;
using Woi.Equipment;
using Woi.Game.Training.UI;

namespace Woi.Game.Training.UI.FireTelemetry
{
    /// <summary>
    /// Screen-space UI Toolkit telemetry strip for active extinguisher intervention.
    /// With <c>autoDriveFromEquipment</c>, follows the equipped <see cref="PlayerExtinguisherEquipment"/> and
    /// <see cref="ExtinguishResult.Source"/> from spray evaluation; otherwise bind transforms and lifecycle manually.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("WOI/Training/UI/Fire Telemetry Panel Controller")]
    public sealed class FireTelemetryPanelController : MonoBehaviour
    {
        private static readonly string[] StatusClassNames =
        {
            "status-neutral",
            "status-ok",
            "status-warning",
            "status-danger",
            "status-wrong"
        };

        [Header("UI")]
        [SerializeField] private UIDocument document;
        [Tooltip("PC: hide equipment pitch row. Enable for VR builds / prefab variants where nozzle angle matters.")]
        [SerializeField] private bool showEquipmentAngle;

        [Header("Transforms")]
        [SerializeField] private Transform playerTarget;
        [Tooltip("Optional: used only for editor tests or if you drive distance without FireSource (normally comes from active fire).")]
        [SerializeField] private Transform fireTarget;
        [SerializeField] private Transform extinguisherRoot;

        [Header("Auto drive")]
        [Tooltip("When enabled, distance/angle follow the equipped extinguisher and active fire from spray evaluation (no stale serialized extinguisher root).")]
        [SerializeField] private bool autoDriveFromEquipment = true;
        [SerializeField] private PlayerExtinguisherEquipment playerExtinguisherEquipment;
        [Tooltip("Optional origin for player→fire distance (e.g. camera). If unset, uses Player Target, then equipment transform.")]
        [SerializeField] private Transform playerPositionOverride;

        [Header("Distance")]
        [SerializeField, Min(0.01f)] private float maxDisplayedDistance = 5f;
        [SerializeField, Min(0.01f)] private float idealDistanceMin = 2f;
        [SerializeField, Min(0.01f)] private float idealDistanceMax = 4f;
        [SerializeField, Min(0.01f)] private float tooCloseDistance = 1.2f;

        [Header("Extinguish lifecycle")]
        [SerializeField, Min(0f)] private float hideDelayAfterStop = 1.5f;

        [Header("Debug")]
        [SerializeField] private bool useDebugMockData;

        private VisualElement _root;
        private VisualElement _liveDot;
        private Label _headerTitle;
        private Label _liveBadge;
        private Label _distanceValue;
        private Label _distanceUnit;
        private VisualElement _distanceBarFill;
        private Label _distanceStatus;
        private Label _sectionLabelFireDistance;
        private Label _sectionLabelAimPosition;
        private Label _aimValue;
        private Label _aimBadge;
        private Label _angleValue;
        private Label _angleSectionLabel;
        private VisualElement _equipmentAngleSection;

        private CultureInfo _numberCulture = CultureInfo.InvariantCulture;
        private string _lastLocaleCode = "\u0001";

        private FireSource _activeFireSource;
        private bool _isExtinguishing;
        private float _lastExtinguishTime;

        private string _aimLabel = "—";
        private FireTelemetryStatus _aimStatus = FireTelemetryStatus.Neutral;

        private ExtinguisherController _subscribedController;

        private bool _warnedDocument;
        private bool _warnedRoot;
        private bool _warnedBinding;
        private bool _warnedAutoEquipment;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (idealDistanceMax < idealDistanceMin)
                idealDistanceMax = idealDistanceMin + 0.01f;
            if (idealDistanceMin <= tooCloseDistance)
                Debug.LogWarning($"[{nameof(FireTelemetryPanelController)}] idealDistanceMin should be greater than tooCloseDistance.", this);

            if (Application.isPlaying && _equipmentAngleSection != null)
                ApplyEquipmentAngleVisibility();
        }
#endif

        public FireSource ActiveFireSource => _activeFireSource;

        private void OnEnable()
        {
            TryBindUi();
            ApplyTelemetryStaticChrome();

            if (!useDebugMockData && autoDriveFromEquipment)
                EnableAutoDrive();

            Hide();
        }

        private void LateUpdate()
        {
            if (_root == null || _root.style.display == DisplayStyle.None)
                return;

            MaybeRefreshLocale();
        }

        private void OnDisable()
        {
            if (!useDebugMockData && autoDriveFromEquipment)
                DisableAutoDrive();
        }

        private void Update()
        {
            if (useDebugMockData)
            {
                RunMockTelemetry();
                return;
            }

            if (_activeFireSource == null)
            {
                Hide();
                return;
            }

            if (_isExtinguishing)
                _lastExtinguishTime = Time.time;

            if (!_isExtinguishing)
            {
                if (Time.time - _lastExtinguishTime >= hideDelayAfterStop)
                {
                    ClearBindingInternal();
                    Hide();
                    return;
                }
            }

            RefreshTelemetry();
        }

        /// <summary>Begins tracking spray on <paramref name="fireSource"/> and shows the panel.</summary>
        public void BeginExtinguishing(FireSource fireSource)
        {
            if (fireSource == null)
                return;

            TryBindUi();
            if (_root == null)
                return;

            ApplyTelemetryStaticChrome();

            _activeFireSource = fireSource;
            _isExtinguishing = true;
            _lastExtinguishTime = Time.time;
            Show();
        }

        /// <summary>Keep-alive while spraying; switches fire if the player changes target mid-spray.</summary>
        public void UpdateExtinguishing(FireSource fireSource)
        {
            if (fireSource == null)
                return;

            _lastExtinguishTime = Time.time;
            _isExtinguishing = true;

            if (_activeFireSource != fireSource)
                _activeFireSource = fireSource;

            Show();
        }

        /// <summary>Marks spray stopped for <paramref name="fireSource"/>; panel hides after <see cref="hideDelayAfterStop"/>.</summary>
        public void EndExtinguishing(FireSource fireSource)
        {
            if (fireSource != _activeFireSource)
                return;

            _isExtinguishing = false;
            _lastExtinguishTime = Time.time;
        }

        /// <summary>Clears binding and hides immediately.</summary>
        public void ClearActiveFire()
        {
            _activeFireSource = null;
            _isExtinguishing = false;
            Hide();
        }

        /// <summary>Updates aim row from gameplay systems (hit zone / evaluator).</summary>
        public void SetAimPosition(string label, FireTelemetryStatus status)
        {
            MaybeRefreshLocale();
            _aimLabel = label ?? string.Empty;
            _aimStatus = status;
            ApplyAimUi();
        }

        public void Show()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.Flex;
            if (document != null)
                document.enabled = true;

            MaybeRefreshLocale();
        }

        public void Hide()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.None;
        }

        public void SetVisible(bool visible)
        {
            if (visible)
                Show();
            else
                Hide();
        }

        private void ClearBindingInternal()
        {
            _activeFireSource = null;
        }

        private void TryBindUi()
        {
            _root = null;
            if (document == null)
            {
                LogOnce(ref _warnedDocument, $"{nameof(FireTelemetryPanelController)}: UIDocument is not assigned.");
                return;
            }

            VisualElement docRoot = document.rootVisualElement;
            _root = docRoot.Q<VisualElement>("fire-telemetry-root");
            if (_root == null)
            {
                LogOnce(ref _warnedRoot, $"{nameof(FireTelemetryPanelController)}: UXML must define 'fire-telemetry-root'.");
                return;
            }

            _liveDot = docRoot.Q<VisualElement>("live-dot");
            _headerTitle = docRoot.Q<Label>("header-title");
            _liveBadge = docRoot.Q<Label>("live-badge");
            _distanceValue = docRoot.Q<Label>("distance-value");
            _distanceUnit = docRoot.Q<Label>("distance-unit");
            _distanceBarFill = docRoot.Q<VisualElement>("distance-bar-fill");
            _distanceStatus = docRoot.Q<Label>("distance-status");
            _sectionLabelFireDistance = docRoot.Q<Label>("section-label-fire-distance");
            _sectionLabelAimPosition = docRoot.Q<Label>("section-label-aim-position");
            _aimValue = docRoot.Q<Label>("aim-value");
            _aimBadge = docRoot.Q<Label>("aim-badge");
            _angleValue = docRoot.Q<Label>("angle-value");
            _angleSectionLabel = docRoot.Q<Label>("angle-label");
            _equipmentAngleSection = docRoot.Q<VisualElement>("equipment-angle-section");
            ApplyEquipmentAngleVisibility();
        }

        private void ApplyEquipmentAngleVisibility()
        {
            if (_equipmentAngleSection != null)
            {
                _equipmentAngleSection.style.display = showEquipmentAngle
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
        }

        void MaybeRefreshLocale()
        {
            string code = TrainingResultUiLanguage.ResolveCode();
            if (string.Equals(code, _lastLocaleCode, StringComparison.OrdinalIgnoreCase))
                return;

            _lastLocaleCode = code;
            _numberCulture = TrainingResultUiLanguage.IsTurkish()
                ? CultureInfo.GetCultureInfo("tr-TR")
                : CultureInfo.InvariantCulture;

            ApplyTelemetryStaticChrome();
            ApplyAimUi();

            // Distance status line is refreshed next frame in RefreshTelemetry / RunMockTelemetry.
        }

        void ApplyTelemetryStaticChrome()
        {
            if (_headerTitle != null)
                _headerTitle.text = Loc("LIVE TELEMETRY", "CANLI TELEMETRİ");
            if (_liveBadge != null)
                _liveBadge.text = Loc("LIVE", "CANLI");
            if (_distanceUnit != null)
                _distanceUnit.text = Loc("m", "m");
            if (_sectionLabelFireDistance != null)
                _sectionLabelFireDistance.text = Loc("FIRE DISTANCE", "YANGIN MESAFESİ");
            if (_sectionLabelAimPosition != null)
                _sectionLabelAimPosition.text = Loc("AIM POSITION", "NİŞAN KONUMU");
            if (_angleSectionLabel != null)
                _angleSectionLabel.text = Loc("EQUIPMENT ANGLE", "EKİPMAN AÇISI");
        }

        static bool IsTr() => TrainingResultUiLanguage.IsTurkish();

        static string Loc(string english, string turkish) => IsTr() ? turkish : english;

        private void RefreshTelemetry()
        {
            if (_root == null)
                return;

            MaybeRefreshLocale();

            Transform fireTransform = _activeFireSource != null ? _activeFireSource.transform : fireTarget;
            Transform playerRef = ResolvePlayerTransform();
            if (playerRef == null || fireTransform == null)
            {
                LogOnce(ref _warnedBinding, $"{nameof(FireTelemetryPanelController)}: assign player position (Player Target, override, or equipment) and ensure an active fire.");
                return;
            }

            float distance = Vector3.Distance(playerRef.position, fireTransform.position);

            if (_distanceValue != null)
                _distanceValue.text = distance.ToString("F1", _numberCulture);

            float normalized = Mathf.Clamp01(distance / Mathf.Max(0.01f, maxDisplayedDistance));
            if (_distanceBarFill != null)
                _distanceBarFill.style.width = Length.Percent(normalized * 100f);

            FireTelemetryStatus distStatus;
            string distText = ResolveDistanceStatus(distance, out distStatus);

            if (_distanceStatus != null)
                _distanceStatus.text = distText;

            ApplyStatusClass(_distanceStatus, distStatus);
            ApplyStatusClass(_distanceBarFill, distStatus);
            ApplyStatusClass(_liveDot, distStatus);

            ApplyAimUi();

            if (showEquipmentAngle && _angleValue != null)
                _angleValue.text = FormatEquipmentAngle();
        }

        private string ResolveDistanceStatus(float distance, out FireTelemetryStatus status)
        {
            if (distance < tooCloseDistance)
            {
                status = FireTelemetryStatus.Danger;
                return Loc("TOO CLOSE", "ÇOK YAKIN");
            }

            if (distance >= idealDistanceMin && distance <= idealDistanceMax)
            {
                status = FireTelemetryStatus.Ok;
                return Loc("IDEAL POSITION", "İDEAL MESAFE");
            }

            if (distance > idealDistanceMax)
            {
                status = FireTelemetryStatus.Neutral;
                return Loc("TOO FAR", "ÇOK UZAK");
            }

            status = FireTelemetryStatus.Warning;
            return Loc("ADJUST RANGE", "MESAFEYİ AYARLA");
        }

        /// <summary>Pitch of equipped extinguisher forward vs horizontal (for VR telemetry when <see cref="showEquipmentAngle"/> is on).</summary>
        private string FormatEquipmentAngle()
        {
            Transform root = ResolveExtinguisherRoot();
            if (root == null)
                return "--°";

            Vector3 f = root.forward;
            float xz = Mathf.Sqrt(f.x * f.x + f.z * f.z);
            float pitchDeg = xz > 1e-5f
                ? Mathf.Atan2(f.y, xz) * Mathf.Rad2Deg
                : (f.y > 0f ? 90f : -90f);

            return $"{pitchDeg.ToString("F1", _numberCulture)}°";
        }

        private void ApplyAimUi()
        {
            if (_aimValue != null)
                _aimValue.text = string.IsNullOrEmpty(_aimLabel) ? "—" : _aimLabel;

            ApplyStatusClass(_aimValue, _aimStatus);

            if (_aimBadge != null)
            {
                _aimBadge.text = LocalizedBadgeForStatus(_aimStatus);
                ApplyStatusClass(_aimBadge, _aimStatus);
                _aimBadge.style.display = string.IsNullOrEmpty(_aimBadge.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
        }

        private static string LocalizedBadgeForStatus(FireTelemetryStatus s)
        {
            switch (s)
            {
                case FireTelemetryStatus.Ok:
                    return Loc("OK", "UYGUN");
                case FireTelemetryStatus.Warning:
                    return Loc("WARN", "UYARI");
                case FireTelemetryStatus.Danger:
                    return Loc("RISK", "RİSK");
                case FireTelemetryStatus.Wrong:
                    return Loc("NO", "HAYIR");
                default:
                    return "";
            }
        }

        /// <summary>Removes all <c>status-*</c> USS classes, then applies the one for <paramref name="status"/>.</summary>
        public static void ApplyStatusClass(VisualElement element, FireTelemetryStatus status)
        {
            if (element == null)
                return;

            for (int i = 0; i < StatusClassNames.Length; i++)
                element.RemoveFromClassList(StatusClassNames[i]);

            switch (status)
            {
                case FireTelemetryStatus.Neutral:
                    element.AddToClassList("status-neutral");
                    break;
                case FireTelemetryStatus.Ok:
                    element.AddToClassList("status-ok");
                    break;
                case FireTelemetryStatus.Warning:
                    element.AddToClassList("status-warning");
                    break;
                case FireTelemetryStatus.Danger:
                    element.AddToClassList("status-danger");
                    break;
                case FireTelemetryStatus.Wrong:
                    element.AddToClassList("status-wrong");
                    break;
            }
        }

        private void RunMockTelemetry()
        {
            TryBindUi();
            Show();

            float t = Time.time;
            float mockDist = Mathf.Lerp(0.8f, 6f, Mathf.PingPong(t * 0.35f, 1f));

            if (_distanceValue != null)
                _distanceValue.text = mockDist.ToString("F1", _numberCulture);

            float normalized = Mathf.Clamp01(mockDist / Mathf.Max(0.01f, maxDisplayedDistance));
            if (_distanceBarFill != null)
                _distanceBarFill.style.width = Length.Percent(normalized * 100f);

            FireTelemetryStatus distStatus;
            string distText = ResolveDistanceStatus(mockDist, out distStatus);
            if (_distanceStatus != null)
                _distanceStatus.text = distText;

            ApplyStatusClass(_distanceStatus, distStatus);
            ApplyStatusClass(_distanceBarFill, distStatus);
            ApplyStatusClass(_liveDot, distStatus);

            SetAimPosition(mockDist < 2.5f ? Loc("BASE ZONE", "TABAN BÖLGESİ") : Loc("ABOVE FLAME", "ALEV ÜSTÜ"),
                mockDist < 2f ? FireTelemetryStatus.Ok : FireTelemetryStatus.Warning);

            if (showEquipmentAngle && _angleValue != null)
                _angleValue.text = $"{Mathf.PingPong(t * 40f, 55f).ToString("F1", _numberCulture)}°";
        }

        private void EnableAutoDrive()
        {
            ResolveEquipmentReference();
            if (playerExtinguisherEquipment != null)
            {
                playerExtinguisherEquipment.OnExtinguisherChanged += OnEquippedExtinguisherChanged;
                HookCurrentEquippedController();
            }
            else
            {
                LogOnce(ref _warnedAutoEquipment,
                    $"{nameof(FireTelemetryPanelController)}: Auto drive is on but no {nameof(PlayerExtinguisherEquipment)} found. Assign the reference or parent this UI under the player.");
            }
        }

        private void DisableAutoDrive()
        {
            if (playerExtinguisherEquipment != null)
                playerExtinguisherEquipment.OnExtinguisherChanged -= OnEquippedExtinguisherChanged;

            UnhookController();
        }

        private void ResolveEquipmentReference()
        {
            if (playerExtinguisherEquipment != null)
                return;

            playerExtinguisherEquipment = GetComponentInParent<PlayerExtinguisherEquipment>();
            if (playerExtinguisherEquipment == null)
                playerExtinguisherEquipment = FindFirstObjectByType<PlayerExtinguisherEquipment>();
        }

        private void OnEquippedExtinguisherChanged(ExtinguisherPickupItem _)
        {
            HookCurrentEquippedController();
        }

        private void HookCurrentEquippedController()
        {
            UnhookController();

            if (playerExtinguisherEquipment == null)
                return;

            ExtinguisherPickupItem item = playerExtinguisherEquipment.CurrentItem;
            if (item == null)
                return;

            ExtinguisherController c = item.Controller;
            if (c == null)
                return;

            _subscribedController = c;
            _subscribedController.OnSprayEvaluated += OnSprayEvaluatedAuto;
            _subscribedController.OnSprayStopped += OnSprayStoppedAuto;
        }

        private void UnhookController()
        {
            if (_subscribedController == null)
                return;

            _subscribedController.OnSprayEvaluated -= OnSprayEvaluatedAuto;
            _subscribedController.OnSprayStopped -= OnSprayStoppedAuto;
            _subscribedController = null;
        }

        private void OnSprayEvaluatedAuto(ExtinguishResult result)
        {
            if (!autoDriveFromEquipment || useDebugMockData)
                return;

            if (result.DidHitZone && result.Source != null)
            {
                UpdateExtinguishing(result.Source);
                ApplyAimFromHit(result);
                return;
            }

            if (_subscribedController != null && _subscribedController.IsDischarging)
                ApplyMissAim(result);
        }

        private void OnSprayStoppedAuto()
        {
            if (!autoDriveFromEquipment || useDebugMockData)
                return;

            if (_activeFireSource != null)
                EndExtinguishing(_activeFireSource);
        }

        private void ApplyAimFromHit(ExtinguishResult result)
        {
            string label;
            FireTelemetryStatus status;

            switch (result.Compatibility)
            {
                case CompatibilityResult.Forbidden:
                    label = Loc("WRONG EXTINGUISHER", "YANLIŞ SÖNDÜRÜCÜ");
                    status = FireTelemetryStatus.Wrong;
                    break;
                case CompatibilityResult.Neutral:
                    label = result.HitZone != null ? result.HitZone.name : Loc("ZONE", "BÖLGE");
                    status = FireTelemetryStatus.Warning;
                    break;
                default:
                    label = result.HitZone != null ? result.HitZone.name : Loc("ON TARGET", "Hedefte");
                    status = FireTelemetryStatus.Ok;
                    break;
            }

            SetAimPosition(label, status);
        }

        private void ApplyMissAim(ExtinguishResult result)
        {
            SetAimPosition(LabelForMiss(result.MissReason), StatusForMiss(result.MissReason));
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

        private static FireTelemetryStatus StatusForMiss(SprayMissReason reason)
        {
            switch (reason)
            {
                case SprayMissReason.ZoneAlreadyExtinguished:
                case SprayMissReason.FireAlreadyExtinguished:
                    return FireTelemetryStatus.Warning;
                default:
                    return FireTelemetryStatus.Wrong;
            }
        }

        private Transform ResolvePlayerTransform()
        {
            if (playerPositionOverride != null)
                return playerPositionOverride;
            if (playerTarget != null)
                return playerTarget;
            if (autoDriveFromEquipment && playerExtinguisherEquipment != null)
                return playerExtinguisherEquipment.transform;
            return null;
        }

        private Transform ResolveExtinguisherRoot()
        {
            if (autoDriveFromEquipment && playerExtinguisherEquipment != null && playerExtinguisherEquipment.CurrentItem != null)
                return playerExtinguisherEquipment.CurrentItem.transform;

            return extinguisherRoot;
        }

        private static void LogOnce(ref bool flag, string message)
        {
            if (flag)
                return;
            flag = true;
            Debug.LogWarning(message);
        }
    }
}
