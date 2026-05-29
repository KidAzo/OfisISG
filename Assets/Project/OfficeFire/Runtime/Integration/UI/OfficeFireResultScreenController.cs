using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using WOI.Modules.SDK;

namespace Woi.OfficeFire
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Result Screen Controller")]
    public sealed class OfficeFireResultScreenController : MonoBehaviour
    {
        [SerializeField]
        private UIDocument uiDocument;

        [SerializeField]
        private UnityEvent onContinueClicked;

        private VisualElement _root;
        private VisualElement _statusBadge;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Label _statusLabel;
        private Label _reactionTimeLabel;
        private Label _reactionTimeValue;
        private Label _fireControlledLabel;
        private Label _fireControlledValue;
        private Label _evacuatedLabel;
        private Label _evacuatedValue;
        private Label _correctSectionTitle;
        private Label _mistakesSectionTitle;
        private ScrollView _correctList;
        private ScrollView _mistakesList;
        private Button _continueButton;

        private OfficeFireScenarioReport _lastReport;
        private bool _uiBound;
        private Coroutine _deferredBindRoutine;

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }
        }

        private void OnEnable()
        {
            TryBindUi();
        }

        private void OnDisable()
        {
            if (_deferredBindRoutine != null)
            {
                StopCoroutine(_deferredBindRoutine);
                _deferredBindRoutine = null;
            }

            if (_continueButton != null)
            {
                _continueButton.clicked -= HandleContinueClicked;
            }
        }

        private void LateUpdate()
        {
            if (!_uiBound || _lastReport == null)
            {
                return;
            }

            bool turkish = ResolveTurkish();
            if (_lastPresentTurkish == turkish)
            {
                return;
            }

            Present(_lastReport);
        }

        private bool _lastPresentTurkish;

        public void Present(OfficeFireScenarioReport report)
        {
            _lastReport = report;
            if (!TryBindUi())
            {
                return;
            }

            bool turkish = ResolveTurkish();
            _lastPresentTurkish = turkish;
            OfficeFireResultScreenModel model = OfficeFireResultScreenMapper.FromReport(report, turkish);
            ApplyModel(model);
            ShowScreen();
        }

        public void HideScreen()
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
            }

            gameObject.SetActive(false);
        }

        public void ShowScreen()
        {
            gameObject.SetActive(true);

            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
            }
        }

        private void ApplyModel(OfficeFireResultScreenModel model)
        {
            if (model == null)
            {
                return;
            }

            _titleLabel.text = model.Title;
            _subtitleLabel.text = model.Subtitle;
            _statusLabel.text = model.StatusLabel;
            _reactionTimeLabel.text = model.ReactionTimeLabel;
            _reactionTimeValue.text = model.ReactionTimeValue;
            _fireControlledLabel.text = model.FireControlledLabel;
            _fireControlledValue.text = model.FireControlledValue;
            _evacuatedLabel.text = model.EvacuatedLabel;
            _evacuatedValue.text = model.EvacuatedValue;
            _correctSectionTitle.text = model.CorrectSectionTitle;
            _mistakesSectionTitle.text = model.MistakesSectionTitle;
            _continueButton.text = model.ContinueButtonText;

            _statusBadge.RemoveFromClassList("status-badge--pass");
            _statusBadge.RemoveFromClassList("status-badge--fail");
            _statusBadge.AddToClassList(model.Passed ? "status-badge--pass" : "status-badge--fail");

            _fireControlledValue.RemoveFromClassList("text-emerald");
            _fireControlledValue.RemoveFromClassList("text-red");
            _fireControlledValue.AddToClassList(
                _lastReport != null && _lastReport.fireControlled ? "text-emerald" : "text-red");

            _evacuatedValue.RemoveFromClassList("text-emerald");
            _evacuatedValue.RemoveFromClassList("text-red");
            _evacuatedValue.AddToClassList(
                _lastReport != null && _lastReport.evacuated ? "text-emerald" : "text-red");

            RebuildList(_correctList, model.CorrectActions, model.EmptyCorrectText, isMistake: false);
            RebuildList(_mistakesList, model.Mistakes, model.EmptyMistakesText, isMistake: true);
        }

        private static void RebuildList(ScrollView list, System.Collections.Generic.List<string> items, string emptyText, bool isMistake)
        {
            if (list == null)
            {
                return;
            }

            list.Clear();

            if (items == null || items.Count == 0)
            {
                Label empty = new Label(emptyText);
                empty.AddToClassList("result-empty-text");
                list.Add(empty);
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                list.Add(CreateRow(items[i], isMistake));
            }
        }

        private static VisualElement CreateRow(string text, bool isMistake)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("result-row");
            row.AddToClassList(isMistake ? "result-row--mistake" : "result-row--correct");

            Label marker = new Label(isMistake ? "✕" : "✓");
            marker.AddToClassList("result-row-marker");

            Label label = new Label(text);
            label.AddToClassList("result-row-text");

            row.Add(marker);
            row.Add(label);
            return row;
        }

        private bool TryBindUi()
        {
            if (_uiBound)
            {
                return true;
            }

            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            VisualElement root = uiDocument != null ? uiDocument.rootVisualElement : null;
            if (root == null)
            {
                if (_deferredBindRoutine == null)
                {
                    _deferredBindRoutine = StartCoroutine(DeferredBindRoutine());
                }

                return false;
            }

            BindUi(root);
            return true;
        }

        private IEnumerator DeferredBindRoutine()
        {
            const int maxFrames = 120;
            for (int i = 0; i < maxFrames; i++)
            {
                VisualElement root = uiDocument != null ? uiDocument.rootVisualElement : null;
                if (root != null)
                {
                    BindUi(root);
                    if (_lastReport != null)
                    {
                        Present(_lastReport);
                    }

                    _deferredBindRoutine = null;
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning("[OfficeFireResultScreenController] UIDocument root did not appear in time.", this);
            _deferredBindRoutine = null;
        }

        private void BindUi(VisualElement root)
        {
            _root = root.Q<VisualElement>("office-fire-result-root") ?? root;
            _statusBadge = _root.Q<VisualElement>("result-status-badge");
            _titleLabel = _root.Q<Label>("result-title");
            _subtitleLabel = _root.Q<Label>("result-subtitle");
            _statusLabel = _root.Q<Label>("result-status-label");
            _reactionTimeLabel = _root.Q<Label>("reaction-time-label");
            _reactionTimeValue = _root.Q<Label>("reaction-time-value");
            _fireControlledLabel = _root.Q<Label>("fire-controlled-label");
            _fireControlledValue = _root.Q<Label>("fire-controlled-value");
            _evacuatedLabel = _root.Q<Label>("evacuated-label");
            _evacuatedValue = _root.Q<Label>("evacuated-value");
            _correctSectionTitle = _root.Q<Label>("correct-section-title");
            _mistakesSectionTitle = _root.Q<Label>("mistakes-section-title");
            _correctList = _root.Q<ScrollView>("correct-actions-list");
            _mistakesList = _root.Q<ScrollView>("mistakes-list");
            _continueButton = _root.Q<Button>("btn-result-continue");

            if (_continueButton != null)
            {
                _continueButton.clicked -= HandleContinueClicked;
                _continueButton.clicked += HandleContinueClicked;
            }

            _uiBound = _titleLabel != null && _correctList != null && _mistakesList != null;
            if (!_uiBound)
            {
                Debug.LogWarning("[OfficeFireResultScreenController] Required UXML elements were not found.", this);
            }
        }

        private void HandleContinueClicked()
        {
            HideScreen();
            onContinueClicked?.Invoke();
        }

        private static bool ResolveTurkish()
        {
            if (ServiceLocator.TryGet(out OfficeFireLanguageResolver resolver) && resolver != null)
            {
                return resolver.IsTurkish();
            }

            OfficeFireLanguageResolver found = FindFirstObjectByType<OfficeFireLanguageResolver>();
            return found == null || found.IsTurkish();
        }
    }
}
