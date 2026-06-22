using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Woi.Settings;
using WOI.Modules.SDK;
using Woi.InputSystem;

namespace Woi.OfficeFire
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Result Screen Controller")]
    public sealed class OfficeFireResultScreenController : MonoBehaviour
    {
        private const string LoginSceneGroup = "OfficeFireModule_Login";

        [SerializeField]
        private UIDocument uiDocument;

        [SerializeField]
        private UnityEvent onContinueClicked;

        [Header("Scene Flow")]
        [SerializeField]
        private bool loadLoginSceneOnContinue = true;

        [SerializeField]
        private string loginSceneGroupName = LoginSceneGroup;

        [SerializeField]
        private bool allowKeyboardContinue = true;

        [Header("Player Input")]
        [SerializeField]
        private Transform playerRoot;

        [SerializeField]
        private string playerTag = "Player";

        [Tooltip("Disabled while the result screen is visible.")]
        [SerializeField]
        private Behaviour[] playerInputBehaviours;

        [SerializeField]
        private bool autoFindPlayerInputBehaviours = true;

        [SerializeField]
        private string[] autoFindBehaviourTypeNames =
        {
            "PCHoverInteractor",
            "PCSelectableInteractor",
            "PlayerLeanController",
        };

        [SerializeField]
        private bool disablePlayerInputWhileVisible = true;

        [Header("PC Controls HUD")]
        [Tooltip("Sol alttaki klavye / tuş HUD. Boşsa sahnede aranır.")]
        [SerializeField]
        private OfficeFireControlsHUDController pcControlsHud;

        private static readonly string[] MovementSpeedFieldNames =
        {
            "_walkSpeed",
            "walkSpeed",
            "_sprintSpeed",
            "sprintSpeed",
        };

        private static readonly string[] MouseSensitivityFieldNames =
        {
            "_mouseSensitivity",
            "mouseSensitivity",
        };

        private static readonly BindingFlags BehaviourFieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private sealed class FloatFieldSnapshot
        {
            public Behaviour Target;
            public FieldInfo Field;
            public float OriginalValue;
        }

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
        private Label _missingSectionTitle;
        private Label _mistakesSectionTitle;
        private ScrollView _correctList;
        private ScrollView _missingList;
        private ScrollView _mistakesList;
        private Button _continueButton;

        private OfficeFireScenarioReport _lastReport;
        private bool _uiBound;
        private bool _sessionResultsExported;
        private bool _isReturningToLogin;
        private Coroutine _deferredBindRoutine;
        private bool _playerInputCaptured;
        private bool[] _savedBehaviourEnabledStates;
        private Behaviour[] _capturedBehaviours;
        private readonly List<FloatFieldSnapshot> _frozenFloatFields = new List<FloatFieldSnapshot>();
        private UnityEngine.CursorLockMode _savedCursorLockState;
        private bool _savedCursorVisible;
        private bool _savedGameplayInteractEnabled = true;
        private bool _savedGameplayEquipEnabled = true;
        private bool _gameplayInteractCaptured;

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (_lastReport == null && !_isReturningToLogin)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_lastReport != null)
            {
                TryBindUi();
            }
        }

        private void OnDisable()
        {
            if (_deferredBindRoutine != null)
            {
                StopCoroutine(_deferredBindRoutine);
                _deferredBindRoutine = null;
            }

            if (!_isReturningToLogin)
            {
                ReleasePlayerInput();
            }

            ReleaseUiBinding();
        }

        private void LateUpdate()
        {
            if (_uiBound && _lastReport != null && allowKeyboardContinue && !_isReturningToLogin)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null
                    && (keyboard.enterKey.wasPressedThisFrame
                        || keyboard.numpadEnterKey.wasPressedThisFrame
                        || keyboard.spaceKey.wasPressedThisFrame))
                {
                    BeginReturnToLogin();
                    return;
                }
            }

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
            ResetPresentationState();
            _lastReport = report;
            ShowScreen();

            if (!TryBindUi())
            {
                return;
            }

            bool turkish = ResolveTurkish();
            _lastPresentTurkish = turkish;
            OfficeFireResultScreenModel model = OfficeFireResultScreenMapper.FromReport(report, turkish);
            ApplyModel(model);
            ExportSessionResultsIfNeeded(report, turkish);
        }

        private void ResetPresentationState()
        {
            _isReturningToLogin = false;
            _sessionResultsExported = false;

            if (_playerInputCaptured)
            {
                ReleasePlayerInput();
            }

            ReleaseUiBinding();

            if (_continueButton != null)
            {
                _continueButton.SetEnabled(true);
            }
        }

        private void ExportSessionResultsIfNeeded(OfficeFireScenarioReport report, bool turkish)
        {
            if (_sessionResultsExported || report == null || report.scenarioId == OfficeFireScenarioId.None)
            {
                return;
            }

            string path = OfficeFireSessionResultCsvExporter.ExportSession(report, turkish);
            _sessionResultsExported = true;

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning(
                    "[OfficeFireResultScreenController] CSV export path unavailable.",
                    this);
            }
        }

        public void HideScreen()
        {
            ReleasePlayerInput();
            HideOverlayOnly();
            gameObject.SetActive(false);
            SetPcControlsHudVisible(true);
        }

        public void HideOverlayOnly()
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
            }
        }

        public void ShowScreen()
        {
            gameObject.SetActive(true);
            CapturePlayerInput();
            SetPcControlsHudVisible(false);

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
            _missingSectionTitle.text = model.MissingSectionTitle;
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

            RebuildList(_correctList, model.CompletedObjectives, model.EmptyCorrectText, ResultRowKind.Completed);
            RebuildList(_missingList, model.MissingObjectives, model.EmptyMissingText, ResultRowKind.Missing);
            RebuildList(_mistakesList, model.Mistakes, model.EmptyMistakesText, ResultRowKind.Mistake);
        }

        private enum ResultRowKind
        {
            Completed,
            Missing,
            Mistake,
        }

        private static void RebuildList(
            ScrollView list,
            System.Collections.Generic.List<string> items,
            string emptyText,
            ResultRowKind rowKind)
        {
            if (list == null)
            {
                return;
            }

            VisualElement container = list.contentContainer;
            container.Clear();

            if (items == null || items.Count == 0)
            {
                Label empty = new Label(emptyText);
                empty.AddToClassList("result-empty-text");
                container.Add(empty);
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                container.Add(CreateRow(items[i], rowKind));
            }
        }

        private static VisualElement CreateRow(string text, ResultRowKind rowKind)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("result-row");

            string marker;
            switch (rowKind)
            {
                case ResultRowKind.Missing:
                    row.AddToClassList("result-row--missing");
                    marker = "!";
                    break;
                case ResultRowKind.Mistake:
                    row.AddToClassList("result-row--mistake");
                    marker = "✕";
                    break;
                default:
                    row.AddToClassList("result-row--correct");
                    marker = "✓";
                    break;
            }

            Label markerLabel = new Label(marker);
            markerLabel.AddToClassList("result-row-marker");

            Label label = new Label(text);
            label.AddToClassList("result-row-text");

            row.Add(markerLabel);
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

        private void ReleaseUiBinding()
        {
            if (_continueButton != null)
            {
                _continueButton.clicked -= HandleContinueClicked;
            }

            _uiBound = false;
            _root = null;
            _statusBadge = null;
            _titleLabel = null;
            _subtitleLabel = null;
            _statusLabel = null;
            _reactionTimeLabel = null;
            _reactionTimeValue = null;
            _fireControlledLabel = null;
            _fireControlledValue = null;
            _evacuatedLabel = null;
            _evacuatedValue = null;
            _correctSectionTitle = null;
            _missingSectionTitle = null;
            _mistakesSectionTitle = null;
            _correctList = null;
            _missingList = null;
            _mistakesList = null;
            _continueButton = null;
        }

        private void BindUi(VisualElement root)
        {
            ReleaseUiBinding();

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
            _missingSectionTitle = _root.Q<Label>("missing-section-title");
            _mistakesSectionTitle = _root.Q<Label>("mistakes-section-title");
            _correctList = _root.Q<ScrollView>("correct-actions-list");
            _missingList = _root.Q<ScrollView>("missing-objectives-list");
            _mistakesList = _root.Q<ScrollView>("mistakes-list");
            _continueButton = _root.Q<Button>("btn-result-continue");

            if (_continueButton != null)
            {
                _continueButton.clicked -= HandleContinueClicked;
                _continueButton.clicked += HandleContinueClicked;
            }

            _uiBound = _titleLabel != null
                && _correctList != null
                && _missingList != null
                && _mistakesList != null;
            if (!_uiBound)
            {
                Debug.LogWarning("[OfficeFireResultScreenController] Required UXML elements were not found.", this);
            }
        }

        private void HandleContinueClicked()
        {
            BeginReturnToLogin();
        }

        private void BeginReturnToLogin()
        {
            if (_isReturningToLogin)
            {
                return;
            }

            _isReturningToLogin = true;

            if (_continueButton != null)
            {
                _continueButton.SetEnabled(false);
            }

            HideOverlayOnly();
            ApplyMenuCursorForLogin();
            onContinueClicked?.Invoke();

            if (!loadLoginSceneOnContinue)
            {
                return;
            }

            string sceneGroup = string.IsNullOrWhiteSpace(loginSceneGroupName)
                ? LoginSceneGroup
                : loginSceneGroupName.Trim();

            if (ServiceLocator.TryGet(out OfficeGameModulesBootstrapper bootstrapper) && bootstrapper != null)
            {
                bootstrapper.LoadScene(sceneGroup);
                return;
            }

            LoginReturnRunner.StartLoad(sceneGroup);
        }

        private static void ApplyMenuCursorForLogin()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private static bool TryResolveSceneLoader(out ISceneLoaderService loader)
        {
            if (ServiceLocator.TryGet(out loader) && loader != null)
            {
                return true;
            }

            if (ServiceLocator.TryGet(out SceneLoader concreteLoader) && concreteLoader != null)
            {
                loader = concreteLoader;
                return true;
            }

            loader = null;
            return false;
        }

        private void CapturePlayerInput()
        {
            if (!disablePlayerInputWhileVisible || _playerInputCaptured)
            {
                return;
            }

            FreezePlayerMovementAndLook();

            _capturedBehaviours = ResolvePlayerInputBehaviours();
            if (_capturedBehaviours != null && _capturedBehaviours.Length > 0)
            {
                _savedBehaviourEnabledStates = new bool[_capturedBehaviours.Length];
                for (int i = 0; i < _capturedBehaviours.Length; i++)
                {
                    Behaviour behaviour = _capturedBehaviours[i];
                    if (behaviour == null)
                    {
                        continue;
                    }

                    _savedBehaviourEnabledStates[i] = behaviour.enabled;
                    behaviour.enabled = false;
                }
            }

            _savedCursorLockState = UnityEngine.Cursor.lockState;
            _savedCursorVisible = UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            CaptureGameplayInteract();
            _playerInputCaptured = true;
        }

        private void CaptureGameplayInteract()
        {
            if (_gameplayInteractCaptured)
            {
                return;
            }

            GameplayInputContext gameplay = ResolvePcGameplayContext();
            if (gameplay == null)
            {
                return;
            }

            _savedGameplayInteractEnabled = true;
            _savedGameplayEquipEnabled = true;
            gameplay.SetInteractEnabled(false);
            gameplay.SetEquipEnabled(false);
            _gameplayInteractCaptured = true;
        }

        private void ReleaseGameplayInteract()
        {
            if (!_gameplayInteractCaptured)
            {
                return;
            }

            GameplayInputContext gameplay = ResolvePcGameplayContext();
            if (gameplay != null)
            {
                if (_savedGameplayInteractEnabled)
                {
                    gameplay.SetInteractEnabled(true);
                }

                if (_savedGameplayEquipEnabled)
                {
                    gameplay.SetEquipEnabled(true);
                }
            }

            _gameplayInteractCaptured = false;
        }

        private static GameplayInputContext ResolvePcGameplayContext()
        {
            InputManager inputManager = FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);
            return inputManager != null ? inputManager.GetPcGameplayContext() : null;
        }

        private void ReleasePlayerInput()
        {
            if (!_playerInputCaptured)
            {
                return;
            }

            RestoreFrozenFloatFields();

            if (_capturedBehaviours != null && _savedBehaviourEnabledStates != null)
            {
                for (int i = 0; i < _capturedBehaviours.Length; i++)
                {
                    Behaviour behaviour = _capturedBehaviours[i];
                    if (behaviour == null || i >= _savedBehaviourEnabledStates.Length)
                    {
                        continue;
                    }

                    behaviour.enabled = _savedBehaviourEnabledStates[i];
                }
            }

            UnityEngine.Cursor.lockState = _savedCursorLockState;
            UnityEngine.Cursor.visible = _savedCursorVisible;
            ReleaseGameplayInteract();
            _playerInputCaptured = false;
            _capturedBehaviours = null;
            _savedBehaviourEnabledStates = null;
        }

        private void FreezePlayerMovementAndLook()
        {
            Transform root = ResolvePlayerRoot();
            if (root == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                FreezeFloatFields(behaviour, MovementSpeedFieldNames);
                FreezeFloatFields(behaviour, MouseSensitivityFieldNames);
            }
        }

        private void FreezeFloatFields(MonoBehaviour behaviour, string[] fieldNames)
        {
            System.Type type = behaviour.GetType();
            for (int i = 0; i < fieldNames.Length; i++)
            {
                FieldInfo field = type.GetField(fieldNames[i], BehaviourFieldFlags);
                if (field == null || field.FieldType != typeof(float))
                {
                    continue;
                }

                if (TryFindExistingSnapshot(behaviour, field, out FloatFieldSnapshot existing))
                {
                    field.SetValue(behaviour, 0f);
                    continue;
                }

                float originalValue = (float)field.GetValue(behaviour);
                _frozenFloatFields.Add(new FloatFieldSnapshot
                {
                    Target = behaviour,
                    Field = field,
                    OriginalValue = originalValue,
                });
                field.SetValue(behaviour, 0f);
            }
        }

        private bool TryFindExistingSnapshot(Behaviour target, FieldInfo field, out FloatFieldSnapshot snapshot)
        {
            for (int i = 0; i < _frozenFloatFields.Count; i++)
            {
                FloatFieldSnapshot candidate = _frozenFloatFields[i];
                if (candidate.Target == target && candidate.Field == field)
                {
                    snapshot = candidate;
                    return true;
                }
            }

            snapshot = null;
            return false;
        }

        private void RestoreFrozenFloatFields()
        {
            for (int i = 0; i < _frozenFloatFields.Count; i++)
            {
                FloatFieldSnapshot snapshot = _frozenFloatFields[i];
                if (snapshot.Target == null || snapshot.Field == null)
                {
                    continue;
                }

                snapshot.Field.SetValue(snapshot.Target, snapshot.OriginalValue);
            }

            _frozenFloatFields.Clear();
        }

        private Transform ResolvePlayerRoot()
        {
            if (playerRoot != null)
            {
                return playerRoot;
            }

            if (!string.IsNullOrWhiteSpace(playerTag))
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
                if (taggedPlayer != null)
                {
                    return taggedPlayer.transform;
                }
            }

            return null;
        }

        private Behaviour[] ResolvePlayerInputBehaviours()
        {
            List<Behaviour> resolved = new List<Behaviour>();

            if (playerInputBehaviours != null)
            {
                for (int i = 0; i < playerInputBehaviours.Length; i++)
                {
                    AddUniqueBehaviour(resolved, playerInputBehaviours[i]);
                }
            }

            if (autoFindPlayerInputBehaviours && autoFindBehaviourTypeNames != null)
            {
                for (int i = 0; i < autoFindBehaviourTypeNames.Length; i++)
                {
                    Behaviour[] found = FindBehavioursByTypeName(autoFindBehaviourTypeNames[i]);
                    if (found == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < found.Length; j++)
                    {
                        AddUniqueBehaviour(resolved, found[j]);
                    }
                }
            }

            return resolved.Count > 0 ? resolved.ToArray() : null;
        }

        private static void AddUniqueBehaviour(List<Behaviour> resolved, Behaviour behaviour)
        {
            if (behaviour == null)
            {
                return;
            }

            for (int i = 0; i < resolved.Count; i++)
            {
                if (resolved[i] == behaviour)
                {
                    return;
                }
            }

            resolved.Add(behaviour);
        }

        private static Behaviour[] FindBehavioursByTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            List<Behaviour> matches = new List<Behaviour>();
            for (int i = 0; i < allBehaviours.Length; i++)
            {
                MonoBehaviour behaviour = allBehaviours[i];
                if (behaviour != null && behaviour.GetType().Name == typeName)
                {
                    matches.Add(behaviour);
                }
            }

            return matches.Count > 0 ? matches.ToArray() : null;
        }

        private void SetPcControlsHudVisible(bool visible)
        {
            OfficeFireControlsHUDController hud = pcControlsHud;
            if (hud == null)
            {
                hud = FindAnyObjectByType<OfficeFireControlsHUDController>(FindObjectsInactive.Include);
            }

            if (hud != null)
            {
                hud.SetHudVisible(visible);
            }
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

        /// <summary>
        /// Loads login on a DontDestroyOnLoad runner so scene unload does not cancel the load task.
        /// </summary>
        private sealed class LoginReturnRunner : MonoBehaviour
        {
            private const string RunnerName = "OfficeFireResultLoginReturn";

            public static void StartLoad(string sceneGroup)
            {
                GameObject existing = GameObject.Find(RunnerName);
                if (existing != null)
                {
                    return;
                }

                GameObject runner = new GameObject(RunnerName);
                DontDestroyOnLoad(runner);
                runner.AddComponent<LoginReturnRunner>().Begin(sceneGroup);
            }

            private void Begin(string sceneGroup)
            {
                StartCoroutine(LoadRoutine(sceneGroup));
            }

            private IEnumerator LoadRoutine(string sceneGroup)
            {
                if (!TryResolveSceneLoader(out ISceneLoaderService loader))
                {
                    Debug.LogError(
                        "[OfficeFireResultScreenController] Scene loader not found. Ensure SceneLoader is registered on ServiceLocator.");
                    Destroy(gameObject);
                    yield break;
                }

                Task loadTask;
                try
                {
                    loadTask = loader.LoadScene(sceneGroup);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[OfficeFireResultScreenController] Login scene load failed: {ex.Message}");
                    Destroy(gameObject);
                    yield break;
                }

                if (loadTask == null)
                {
                    Destroy(gameObject);
                    yield break;
                }

                while (!loadTask.IsCompleted)
                {
                    yield return null;
                }

                if (loadTask.IsFaulted)
                {
                    Debug.LogError("[OfficeFireResultScreenController] Login scene load task faulted.");
                    if (loadTask.Exception != null)
                    {
                        Debug.LogException(loadTask.Exception.GetBaseException());
                    }
                }

                Destroy(gameObject);
            }
        }
    }
}
