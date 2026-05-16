using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using UnityEngine.XR;
using Woi.Game.Training;
using Woi.Training;
using Woi.UI.Popups.Localization;

namespace Woi.Game.Training.UI
{
    /// <summary>
    /// Binds <see cref="TrainingResultScreenModel"/> to UI Toolkit assets (main UXML + card/row templates).
    /// Supports the slate <c>TrainingResultScreen.uxml</c> layout (fire-analysis-grid / evaluation-list) and the
    /// older header-scenario / fire-cards-host layout if you still use that asset in a scene.
    /// <c>ResultScreenVR.uxml</c> is hidden when <see cref="FirePlatformRuntime.IsPC"/>; the slate layout is hidden when <see cref="FirePlatformRuntime.IsVR"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainingResultScreenController : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;

        [Tooltip("TrainingResultFireCard.uxml")]
        [SerializeField] private VisualTreeAsset _fireCardTemplate;

        [Tooltip("TrainingResultEvalRow.uxml")]
        [SerializeField] private VisualTreeAsset _evalRowTemplate;

        [Header("Navigation")]
        [Tooltip("Fired when the player clicks 'Back to Login'. " +
                 "Wire LevelController.ReturnToLogin() here.")]
        [SerializeField] private UnityEvent _onBackToLogin;

        private VisualElement _root;
        private bool _useModernLayout;
        private bool _vrLayout;
        private VisualElement _fireCardsHost;
        private VisualElement _evalRowsHost;
        private VisualElement _criticalList;
        private VisualElement _advancedLines;
        private Foldout _advancedFoldout;

        private SessionReport _lastSessionReportForLocale;
        string _lastPresentLanguageCode = "\u0001";
        string _cachedTraineeName = string.Empty;

        private void OnEnable()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();

            if (_document != null && _document.rootVisualElement != null)
            {
                CacheRoots(_document.rootVisualElement);
                ApplyPortingVisibility();
            }
        }

        private void LateUpdate()
        {
            if (_lastSessionReportForLocale == null || _root == null)
                return;

            string code = TrainingResultUiLanguage.ResolveCode();
            if (string.Equals(code, _lastPresentLanguageCode, StringComparison.OrdinalIgnoreCase))
                return;

            Present(TrainingResultScreenMapper.FromSessionReport(_lastSessionReportForLocale));
            if (!string.IsNullOrEmpty(_cachedTraineeName))
                SetTraineeName(_cachedTraineeName);
        }

        private void OnDisable()
        {
            if (_root != null)
            {
                Button btn = _root.Q<Button>("btn-back-to-login-inner");
                if (btn != null)
                    btn.UnregisterCallback<ClickEvent>(OnBackToLoginClicked);

                Button menuBtn = _root.Q<Button>("btn-return-menu");
                if (menuBtn != null)
                    menuBtn.UnregisterCallback<ClickEvent>(OnBackToLoginClicked);
            }
        }

        private void OnBackToLoginClicked(ClickEvent evt)
        {
            Debug.Log("[TrainingResultScreenController] Back to Login ClickEvent fired.", this);

            // Try the Inspector-wired UnityEvent first
            if (_onBackToLogin != null && _onBackToLogin.GetPersistentEventCount() > 0)
            {
                _onBackToLogin.Invoke();
                return;
            }

            // Fallback: find LevelController in the scene and call ReturnToLogin directly
            LevelController lc = UnityEngine.Object.FindAnyObjectByType<LevelController>();
            if (lc != null)
            {
                Debug.Log("[TrainingResultScreenController] Found LevelController — calling ReturnToLogin().", this);
                lc.ReturnToLogin();
            }
            else
            {
                Debug.LogError("[TrainingResultScreenController] LevelController not found in scene. Cannot return to login.", this);
            }
        }

        public void Present(SessionReport report)
        {
            if (report == null)
            {
                Debug.LogWarning($"[{nameof(TrainingResultScreenController)}] Session report was null.", this);
                return;
            }

            _lastSessionReportForLocale = report;
            Present(TrainingResultScreenMapper.FromSessionReport(report));
        }

        public void Present(TrainingResultScreenModel model)
        {
            if (model == null)
            {
                Debug.LogWarning($"[{nameof(TrainingResultScreenController)}] Model was null.", this);
                return;
            }

            if (_document == null)
                _document = GetComponent<UIDocument>();

            if (_document == null || _document.rootVisualElement == null)
            {
                Debug.LogError($"[{nameof(TrainingResultScreenController)}] UIDocument or root missing.", this);
                return;
            }

            _root = _document.rootVisualElement;
            CacheRoots(_root);

            if (_useModernLayout)
                BindModernHeader(model.Header);
            else
                BindLegacyHeader(model.Header);

            RebuildFireCards(model.FireCards);
            RebuildEvalRows(model.OverallEvaluation);
            RebuildCriticalMistakes(model.CriticalMistakes);
            BindAdvanced(model.Advanced);

            ApplyResultScreenStaticChrome();

            _lastPresentLanguageCode = TrainingResultUiLanguage.ResolveCode();

            ApplyPortingVisibility();
        }

        /// <summary>
        /// Overrides the header title with the trainee's full name from the login screen.
        /// Call this after <see cref="Present"/> with <c>SessionDataSO.UserName</c>.
        /// Targets the <c>scenario-title</c> label — the large name at the top of the card.
        /// </summary>
        public void SetTraineeName(string name)
        {
            _cachedTraineeName = name ?? string.Empty;

            if (_document == null)
                _document = GetComponent<UIDocument>();

            VisualElement root = _document != null ? _document.rootVisualElement : _root;
            if (root == null) return;

            Label label = root.Q<Label>("scenario-title");
            if (label != null)
                label.text = _cachedTraineeName;

            Label legacyHeader = root.Q<Label>("header-scenario");
            if (legacyHeader != null)
                legacyHeader.text = _cachedTraineeName;
        }

        public void HideScreen()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.None;
        }

        public void ShowScreen()
        {
            ApplyPortingVisibility();
        }

        /// <summary>
        /// <see cref="ResultScreenVR"/>: PC portunda gizlenir. Klasik sonuç ekranı: XR portunda gizlenir (aynı sahneye iki UIDocument koyulduğunda çakışma olmasın).
        /// <see cref="FirePlatformRuntime"/> bootstrap / loader ile aynı <c>PortingVariable</c> kaynağından okunur.
        /// </summary>
        void ApplyPortingVisibility()
        {
            if (_root == null)
                return;

            bool hide;
            if (FirePlatformRuntime.IsSourceInitialized)
            {
                hide = (_vrLayout && FirePlatformRuntime.IsPC)
                    || (!_vrLayout && FirePlatformRuntime.IsVR);
            }
            else
            {
#pragma warning disable CS0618
                bool xrHeadset = XRSettings.isDeviceActive;
#pragma warning restore CS0618
                if (_vrLayout)
                    hide = !xrHeadset;
                else
                    hide = xrHeadset;
            }

            _root.style.display = hide ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void CacheRoots(VisualElement root)
        {
            if (root == null)
                return;

            _root = root;
            _vrLayout = string.Equals(root.name, "result-screen-vr-root", StringComparison.Ordinal)
                        || root.Q<VisualElement>("result-screen-vr-root") != null;

            // Wire the back-to-login button via ClickEvent (more reliable than Button.clicked)
            Button backBtn = root.Q<Button>("btn-back-to-login-inner");
            if (backBtn != null)
            {
                backBtn.UnregisterCallback<ClickEvent>(OnBackToLoginClicked);
                backBtn.RegisterCallback<ClickEvent>(OnBackToLoginClicked);
                Debug.Log("[TrainingResultScreenController] btn-back-to-login-inner found and wired via ClickEvent.", this);
            }
            else if (!_vrLayout)
            {
                Debug.LogWarning("[TrainingResultScreenController] btn-back-to-login-inner NOT FOUND in UI hierarchy.", this);
            }

            Button returnMenuBtn = root.Q<Button>("btn-return-menu");
            if (returnMenuBtn != null)
            {
                returnMenuBtn.UnregisterCallback<ClickEvent>(OnBackToLoginClicked);
                returnMenuBtn.RegisterCallback<ClickEvent>(OnBackToLoginClicked);
            }

            VisualElement modernFire = root.Q<VisualElement>("fire-analysis-grid");
            if (modernFire != null)
            {
                _useModernLayout = true;
                _fireCardsHost = modernFire;
                _evalRowsHost = root.Q<VisualElement>("evaluation-list");
            }
            else
            {
                _useModernLayout = false;
                _fireCardsHost = root.Q<VisualElement>("fire-cards-host");
                _evalRowsHost = root.Q<VisualElement>("eval-rows-host");
            }

            _criticalList = root.Q<VisualElement>("critical-list");
            _advancedLines = root.Q<VisualElement>("advanced-lines")
                ?? root.Q<VisualElement>("advanced-details-list");
            _advancedFoldout = root.Q<Foldout>("advanced-foldout");

            EnsureVrThumbstickScroll();
        }

        void EnsureVrThumbstickScroll()
        {
            if (!_vrLayout)
                return;

            if (_document == null)
                _document = GetComponent<UIDocument>();

            if (_document == null)
                return;

            if (_document.GetComponent<TrainingResultScreenVrThumbstickScroll>() != null)
                return;

            _document.gameObject.AddComponent<TrainingResultScreenVrThumbstickScroll>();
        }

        private void BindModernHeader(TrainingResultHeaderModel h)
        {
            SetLabelText("scenario-title", h.ScenarioTitle);
            SetLabelText("evaluation-status", h.ResultLabel);
            SetLabelText("duration-value", h.SessionDurationDisplay);

            Label unit = _root.Q<Label>("duration-unit");
            if (unit != null)
            {
                unit.text = string.Empty;
                unit.style.display = DisplayStyle.None;
            }

            SetLabelText("first-response-value", h.TimeToFirstResponseDisplay);
            SetLabelText("total-score", $"{h.FinalScorePercent}");

            VisualElement badge = _root.Q<VisualElement>("evaluation-badge");
            if (badge != null)
            {
                badge.RemoveFromClassList("badge-pass");
                badge.RemoveFromClassList("badge-fail");
                badge.RemoveFromClassList("badge-pending");
                switch (h.ResultTone)
                {
                    case "pass":
                        badge.AddToClassList("badge-pass");
                        break;
                    case "fail":
                        badge.AddToClassList("badge-fail");
                        break;
                    default:
                        badge.AddToClassList("badge-pending");
                        break;
                }
            }
        }

        private void SetLabelText(string name, string text)
        {
            Label label = _root.Q<Label>(name);
            if (label != null)
                label.text = text ?? string.Empty;
        }

        private void BindLegacyHeader(TrainingResultHeaderModel h)
        {
            Label scenario = _root.Q<Label>("header-scenario");
            if (scenario == null)
            {
                Debug.LogWarning(
                    $"[{nameof(TrainingResultScreenController)}] Legacy header elements not found; use TrainingResultScreen.uxml or add matching names.",
                    this);
                return;
            }

            scenario.text = h.ScenarioTitle;
            _root.Q<Label>("header-result-text").text = h.ResultLabel;
            _root.Q<Label>("header-score-value").text = $"{h.FinalScorePercent}";
            _root.Q<Label>("header-duration").text = h.SessionDurationDisplay;
            _root.Q<Label>("header-first-response").text = h.TimeToFirstResponseDisplay;

            VisualElement pill = _root.Q<VisualElement>("header-result-pill");
            pill.RemoveFromClassList("tr-pill--pass");
            pill.RemoveFromClassList("tr-pill--fail");
            pill.RemoveFromClassList("tr-pill--pending");
            switch (h.ResultTone)
            {
                case "pass":
                    pill.AddToClassList("tr-pill--pass");
                    break;
                case "fail":
                    pill.AddToClassList("tr-pill--fail");
                    break;
                default:
                    pill.AddToClassList("tr-pill--pending");
                    break;
            }
        }

        private void RebuildFireCards(IReadOnlyList<TrainingResultFireCardModel> cards)
        {
            if (_fireCardsHost == null || _fireCardTemplate == null)
            {
                Debug.LogError(
                    $"[{nameof(TrainingResultScreenController)}] Missing fire card host (fire-analysis-grid or fire-cards-host) or fire card template.",
                    this);
                return;
            }

            _fireCardsHost.Clear();
            foreach (TrainingResultFireCardModel card in cards)
                _fireCardsHost.Add(BuildFireCard(card));
        }

        private VisualElement BuildFireCard(TrainingResultFireCardModel data)
        {
            VisualElement root = CloneFireCardHost();

            if (root.Q<Label>("fire-name") != null)
                return BuildModernFireCard(root, data);
            if (root.Q<Label>("fire-card-title") != null)
                return BuildLegacyFireCardFromRoot(root, data);

            Debug.LogError(
                $"[{nameof(TrainingResultScreenController)}] Fire card template must define either fire-name (modern) or fire-card-title (legacy).",
                this);
            return root;
        }

        private static VisualElement BuildModernFireCard(VisualElement root, TrainingResultFireCardModel data)
        {
            ApplyFireCardStaticLabels(root);

            root.Q<Label>("fire-name").text = data.CardTitle;
            root.Q<Label>("fire-class").text = data.FireClassDisplay;
            root.Q<Label>("fire-required").text = data.RequiredExtinguisherDisplay;
            root.Q<Label>("fire-used").text = data.UsedExtinguisherDisplay;

            BindModernYesNoGood(root.Q<Label>("correct-val"), data.CorrectExtinguisherKnown, data.CorrectExtinguisherSelected, goodWhenYes: true);
            BindModernYesNoGood(root.Q<Label>("extinguished-val"), known: true, data.FireExtinguished, goodWhenYes: true);
            BindModernYesNoGood(root.Q<Label>("depleted-val"), data.DepletionKnown, data.DepletedBeforeCompletion, goodWhenYes: false);

            VisualElement timeRow = root.Q<VisualElement>("fire-time-row");
            if (timeRow != null)
            {
                if (data.HasTimeToExtinguish)
                {
                    timeRow.style.display = DisplayStyle.Flex;
                    root.Q<Label>("fire-time").text = data.TimeToExtinguishDisplay;
                }
                else
                    timeRow.style.display = DisplayStyle.None;
            }

            VisualElement notesPanel = root.Q<VisualElement>("fire-notes-panel");
            Label notesBody = root.Q<Label>("fire-mistakes");
            if (notesPanel != null && notesBody != null)
            {
                if (data.FireExtinguished)
                    notesPanel.style.display = DisplayStyle.None;
                else
                {
                    notesPanel.style.display = DisplayStyle.Flex;
                    if (data.KeyMistakes != null && data.KeyMistakes.Count > 0)
                    {
                        var sb = new StringBuilder();
                        for (int i = 0; i < data.KeyMistakes.Count; i++)
                        {
                            if (i > 0) sb.Append('\n');
                            sb.Append(data.KeyMistakes[i]);
                        }

                        notesBody.text = sb.ToString();
                    }
                    else
                        notesBody.text = LocalizedUiPair.Resolve(
                            $"Fire '{data.CardTitle}' was not fully extinguished.",
                            $"'{data.CardTitle}' yangını tamamen söndürülmedi.");
                }
            }

            return root;
        }

        private static void BindModernYesNoGood(Label label, bool known, bool value, bool goodWhenYes)
        {
            label.RemoveFromClassList("text-success");
            label.RemoveFromClassList("text-error");
            label.RemoveFromClassList("text-warn");

            if (!known)
            {
                label.text = "\u2014";
                return;
            }

            label.text = value
                ? LocalizedUiPair.Resolve("Yes", "Evet")
                : LocalizedUiPair.Resolve("No", "Hayır");
            bool good = goodWhenYes ? value : !value;
            label.AddToClassList(good ? "text-success" : "text-error");
        }

        private static VisualElement BuildLegacyFireCardFromRoot(VisualElement root, TrainingResultFireCardModel data)
        {
            ApplyLegacyFireCardStaticLabels(root);

            root.Q<Label>("fire-card-title").text = data.CardTitle;
            root.Q<Label>("fire-class").text = data.FireClassDisplay;
            root.Q<Label>("fire-required").text = data.RequiredExtinguisherDisplay;
            root.Q<Label>("fire-used").text = data.UsedExtinguisherDisplay;

            BindYesNoChip(root.Q<Label>("fire-correct"), data.CorrectExtinguisherKnown, data.CorrectExtinguisherSelected);
            BindYesNoChip(root.Q<Label>("fire-out"), known: true, value: data.FireExtinguished);
            BindDepletedChip(root.Q<Label>("fire-depleted"), data.DepletionKnown, data.DepletedBeforeCompletion);

            VisualElement timeRow = root.Q<VisualElement>("fire-time-row");
            if (data.HasTimeToExtinguish)
            {
                timeRow.AddToClassList("tr-kv--visible");
                root.Q<Label>("fire-time").text = data.TimeToExtinguishDisplay;
            }
            else
                timeRow.RemoveFromClassList("tr-kv--visible");

            VisualElement mistakesBlock = root.Q<VisualElement>("fire-mistakes-block");
            VisualElement mistakeList = root.Q<VisualElement>("fire-mistake-list");
            mistakeList.Clear();

            if (data.KeyMistakes == null || data.KeyMistakes.Count == 0)
                mistakesBlock.style.display = DisplayStyle.None;
            else
            {
                mistakesBlock.style.display = DisplayStyle.Flex;
                foreach (string m in data.KeyMistakes)
                {
                    var line = new Label(m) { name = "mistake-line" };
                    line.AddToClassList("tr-mistake-item");
                    mistakeList.Add(line);
                }
            }

            return root;
        }

        /// <summary>Wrapper around <see cref="VisualTreeAsset.CloneTree"/>; adds a grid slot class for the modern 2-column fire layout.</summary>
        private VisualElement CloneFireCardHost()
        {
            var host = new VisualElement();
            if (_useModernLayout)
                host.AddToClassList("fire-analysis-grid-slot");
            _fireCardTemplate.CloneTree(host);
            return host;
        }

        private static VisualElement CloneTemplate(VisualTreeAsset asset)
        {
            var host = new VisualElement();
            asset.CloneTree(host);
            return host;
        }

        private static void BindYesNoChip(Label label, bool known, bool value)
        {
            label.RemoveFromClassList("tr-chip-value--yes");
            label.RemoveFromClassList("tr-chip-value--no");
            label.RemoveFromClassList("tr-chip-value--unknown");

            if (!known)
            {
                label.text = "\u2014";
                label.AddToClassList("tr-chip-value--unknown");
                return;
            }

            if (value)
            {
                label.text = LocalizedUiPair.Resolve("Yes", "Evet");
                label.AddToClassList("tr-chip-value--yes");
            }
            else
            {
                label.text = LocalizedUiPair.Resolve("No", "Hayır");
                label.AddToClassList("tr-chip-value--no");
            }
        }

        private static void BindDepletedChip(Label label, bool known, bool depletedBeforeCompletion)
        {
            label.RemoveFromClassList("tr-chip-value--yes");
            label.RemoveFromClassList("tr-chip-value--no");
            label.RemoveFromClassList("tr-chip-value--unknown");

            if (!known)
            {
                label.text = "\u2014";
                label.AddToClassList("tr-chip-value--unknown");
                return;
            }

            if (depletedBeforeCompletion)
            {
                label.text = LocalizedUiPair.Resolve("Yes", "Evet");
                label.AddToClassList("tr-chip-value--no");
            }
            else
            {
                label.text = LocalizedUiPair.Resolve("No", "Hayır");
                label.AddToClassList("tr-chip-value--yes");
            }
        }

        private void RebuildEvalRows(IReadOnlyList<TrainingResultMetricRowModel> rows)
        {
            if (_evalRowsHost == null || _evalRowTemplate == null)
            {
                Debug.LogError(
                    $"[{nameof(TrainingResultScreenController)}] Missing evaluation host (evaluation-list or eval-rows-host) or eval row template.",
                    this);
                return;
            }

            _evalRowsHost.Clear();
            foreach (TrainingResultMetricRowModel row in rows)
                _evalRowsHost.Add(BuildEvalRow(row));
        }

        private VisualElement BuildEvalRow(TrainingResultMetricRowModel data)
        {
            VisualElement instance = CloneTemplate(_evalRowTemplate);

            if (instance.Q<Label>("eval-icon-text") != null)
                return BuildModernEvalRow(instance, data);
            if (instance.Q<Label>("eval-icon") != null)
                return BuildLegacyEvalRowFromInstance(instance, data);

            Debug.LogError(
                $"[{nameof(TrainingResultScreenController)}] Eval row template must define eval-icon-text (modern) or eval-icon (legacy).",
                this);
            return instance;
        }

        private static VisualElement BuildModernEvalRow(VisualElement instance, TrainingResultMetricRowModel data)
        {
            VisualElement box = instance.Q<VisualElement>("eval-icon-box");
            Label iconText = instance.Q<Label>("eval-icon-text");
            if (box == null || iconText == null)
                return instance;

            box.RemoveFromClassList("error-bg");
            box.RemoveFromClassList("success-bg");
            box.RemoveFromClassList("warn-bg");
            iconText.RemoveFromClassList("text-error");
            iconText.RemoveFromClassList("text-success");
            iconText.RemoveFromClassList("text-warn");

            instance.Q<Label>("eval-title").text = data.Title;
            instance.Q<Label>("eval-desc").text = data.DetailDisplay;

            switch (data.StatusTone)
            {
                case "pass":
                    iconText.text = "\u2713";
                    box.AddToClassList("success-bg");
                    iconText.AddToClassList("text-success");
                    break;
                case "fail":
                    iconText.text = "\u2717";
                    box.AddToClassList("error-bg");
                    iconText.AddToClassList("text-error");
                    break;
                default:
                    iconText.text = "\u2026";
                    box.AddToClassList("warn-bg");
                    iconText.AddToClassList("text-warn");
                    break;
            }

            return instance;
        }

        private static VisualElement BuildLegacyEvalRowFromInstance(VisualElement instance, TrainingResultMetricRowModel data)
        {
            Label icon = instance.Q<Label>("eval-icon");
            instance.Q<Label>("eval-title").text = data.Title;
            instance.Q<Label>("eval-detail").text = data.DetailDisplay;

            icon.RemoveFromClassList("tr-eval-row__icon--pass");
            icon.RemoveFromClassList("tr-eval-row__icon--fail");
            icon.RemoveFromClassList("tr-eval-row__icon--unknown");

            switch (data.StatusTone)
            {
                case "pass":
                    icon.text = "\u2713";
                    icon.AddToClassList("tr-eval-row__icon--pass");
                    break;
                case "fail":
                    icon.text = "\u2717";
                    icon.AddToClassList("tr-eval-row__icon--fail");
                    break;
                default:
                    icon.text = "\u2026";
                    icon.AddToClassList("tr-eval-row__icon--unknown");
                    break;
            }

            return instance;
        }

        private void RebuildCriticalMistakes(IReadOnlyList<string> mistakes)
        {
            if (_criticalList == null)
                return;

            _criticalList.Clear();

            if (mistakes == null || mistakes.Count == 0)
            {
                var empty = new Label(LocalizedUiPair.Resolve(
                    "No critical points recorded for this session.",
                    "Bu oturum için kritik nokta kaydı yok."));
                empty.AddToClassList("critical-empty");
                _criticalList.Add(empty);
                return;
            }

            foreach (string m in mistakes)
            {
                if (string.IsNullOrWhiteSpace(m))
                    continue;
                _criticalList.Add(BuildCriticalPointRow(m.Trim()));
            }
        }

        private static VisualElement BuildCriticalPointRow(string text)
        {
            var row = new VisualElement();
            row.AddToClassList("critical-item");

            var accent = new VisualElement();
            accent.AddToClassList("critical-line");

            var lbl = new Label(text);
            lbl.AddToClassList("critical-text");

            row.Add(accent);
            row.Add(lbl);
            return row;
        }

        private void BindAdvanced(TrainingResultAdvancedModel advanced)
        {
            if (_advancedLines == null)
                return;

            _advancedLines.Clear();
            if (advanced?.Rows != null)
            {
                foreach (TrainingResultAdvancedTableRowModel row in advanced.Rows)
                    _advancedLines.Add(BuildAdvancedTableRow(row));
            }

            if (_advancedFoldout != null)
                _advancedFoldout.value = false;
        }

        void ApplyResultScreenStaticChrome()
        {
            if (_root == null)
                return;

            void Lbl(string elementName, string english, string turkish)
            {
                Label l = _root.Q<Label>(elementName);
                if (l != null)
                    l.text = LocalizedUiPair.Resolve(english, turkish);
            }

            Lbl("stat-label-duration", "DURATION", "OTURUM SÜRESİ");
            Lbl("stat-label-first-response", "FIRST RESPONSE", "İLK TEPKİ");
            Lbl("score-label-total", "TOTAL SCORE", "TOPLAM SKOR");
            Lbl("section-title-fire", "FIRE ANALYSIS", "YANGIN ANALİZİ");
            Lbl("section-title-eval", "OVERALL EVALUATION", "GENEL DEĞERLENDİRME");
            Lbl("section-title-critical", "CRITICAL POINTS", "KRİTİK NOKTALAR");
            Lbl("section-title-advanced", "ADVANCED DETAILS", "GELİŞMİŞ AYRINTILAR");
            Lbl("th-metric", "METRIC", "METRİK");
            Lbl("th-recorded", "RECORDED VALUE", "KAYDEDİLEN DEĞER");
            Lbl("th-target", "TARGET / LIMIT", "HEDEF / LİMİT");
            Lbl("th-status", "STATUS", "DURUM");

            Button back = _root.Q<Button>("btn-back-to-login-inner");
            if (back != null)
                back.text = LocalizedUiPair.Resolve("Back to Login", "Girişe dön");

            Button returnMenu = _root.Q<Button>("btn-return-menu");
            if (returnMenu != null)
                returnMenu.text = LocalizedUiPair.Resolve("BACK TO LOGIN", "MENÜYE DÖN");

            Label firstResponseUnit = _root.Q<Label>("first-response-unit");
            if (firstResponseUnit != null)
                firstResponseUnit.text = LocalizedUiPair.Resolve("s", "sn");

            Foldout advancedFoldout = _root.Q<Foldout>("advanced-foldout");
            if (advancedFoldout != null)
                advancedFoldout.text = LocalizedUiPair.Resolve("Advanced details", "Gelişmiş ayrıntılar");

            // Legacy slate (optional names on custom UXML)
            Lbl("legacy-stat-duration", "Duration", "Süre");
            Lbl("legacy-stat-first-response", "First response", "İlk tepki");
            Lbl("legacy-score-caption", "Score", "Skor");
            Lbl("legacy-section-fires", "Fire details", "Yangın detayları");
            Lbl("legacy-section-evaluation", "Evaluation", "Değerlendirme");
        }

        static void ApplyLegacyFireCardStaticLabels(VisualElement root)
        {
            if (root == null)
                return;

            void L(string elementName, string english, string turkish)
            {
                Label x = root.Q<Label>(elementName);
                if (x != null)
                    x.text = LocalizedUiPair.Resolve(english, turkish);
            }

            L("legacy-hdr-fire-class", "Fire class", "Yangın sınıfı");
            L("legacy-hdr-required-ext", "Required extinguisher", "Gerekli söndürücü");
            L("legacy-hdr-used-ext", "Used extinguisher", "Kullanılan söndürücü");
            L("legacy-hdr-correct", "Correct extinguisher", "Doğru söndürücü");
            L("legacy-hdr-extinguished", "Fire extinguished", "Yangın söndürüldü");
            L("legacy-hdr-depleted", "Depleted early", "Erken tükendi");
            L("legacy-hdr-time", "Time to extinguish", "Söndürme süresi");
            L("legacy-hdr-mistakes", "Notes / mistakes", "Notlar / hatalar");
        }

        static void ApplyFireCardStaticLabels(VisualElement root)
        {
            if (root == null)
                return;

            void L(string elementName, string english, string turkish)
            {
                Label x = root.Q<Label>(elementName);
                if (x != null)
                    x.text = LocalizedUiPair.Resolve(english, turkish);
            }

            L("hdr-fire-class", "Fire class", "Yangın sınıfı");
            L("hdr-required-ext", "Required extinguisher", "Gerekli söndürücü");
            L("hdr-used-ext", "Used extinguisher", "Kullanılan söndürücü");
            L("hdr-status-correct", "CORRECT CHOICE", "DOĞRU SEÇİM");
            L("hdr-status-extinguished", "EXTINGUISHED", "SÖNDÜRÜLDÜ");
            L("hdr-status-depleted", "DEPLETED EARLY", "ERKEN TÜKENDİ");
            L("hdr-time-ext", "Time to extinguish", "Söndürme süresi");
            L("hdr-notes-title", "NOTES FOR THIS FIRE", "BU YANGIN İÇİN NOTLAR");
        }

        private static VisualElement BuildAdvancedTableRow(TrainingResultAdvancedTableRowModel data)
        {
            var row = new VisualElement();
            row.AddToClassList("table-row");

            var metric = new Label(data.Metric);
            metric.AddToClassList("col-metric");
            metric.AddToClassList("td-metric");
            row.Add(metric);

            var value = new Label(data.RecordedValue);
            value.AddToClassList("col-value");
            value.AddToClassList("td-value");
            row.Add(value);

            var target = new Label(data.TargetLimit);
            target.AddToClassList("col-target");
            target.AddToClassList("td-target");
            row.Add(target);

            var statusCol = new VisualElement();
            statusCol.AddToClassList("col-status");

            var badge = new VisualElement();
            badge.AddToClassList("status-badge");
            badge.AddToClassList(data.StatusTone switch
            {
                "pass" => "badge-bg-success",
                "fail" => "badge-bg-error",
                _      => "badge-bg-neutral",
            });

            var statusText = new Label(data.StatusLabel);
            statusText.AddToClassList(data.StatusTone switch
            {
                "pass"   => "badge-text-success",
                "fail"   => "badge-text-error",
                _        => "badge-text-neutral",
            });
            badge.Add(statusText);
            statusCol.Add(badge);
            row.Add(statusCol);

            return row;
        }
    }
}
