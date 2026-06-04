using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Woi.Events;
using Woi.Events.Data;
using Woi.DataHandler;
using Woi.OfficeFire;
using Woi.Player;
using Woi.Settings;
using WOI.Modules.SDK;
using WoiUtils.AudioSystem;

namespace Woi.WasteCollectionMode
{
    [DisallowMultipleComponent]
    public class WasteResultScreenController : MonoBehaviour
    {
        private const string CorrectStatusIconPath =
            "Assets/Project/WasteCollection/UI/IconsPng/circle-check.png";
        private const string IncorrectStatusIconPath =
            "Assets/Project/WasteCollection/UI/IconsPng/circle-x.png";
        private const string ExitAlertIconPath =
            "Assets/Project/WasteCollection/UI/IconsPng/triangle-alert.png";
        private const string ResultShowSoundPath =
            "Assets/Project/WasteCollection/Audio/Result/ResultScreen.asset";

        private static readonly Color CorrectStatusColor = new(0.376f, 0.647f, 0.980f, 1f);
        private static readonly Color IncorrectStatusColor = new(0.957f, 0.247f, 0.369f, 1f);
        private static readonly Color NotFoundStatusColor = new(0.984f, 0.749f, 0.141f, 1f);

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Texture2D correctStatusIcon;
        [SerializeField] private Texture2D incorrectStatusIcon;
        [SerializeField] private Texture2D exitAlertIcon;
        [SerializeField] private WasteCollectTracker collectTracker;
        [SerializeField] private WasteSelectionMenu wasteSelectionMenu;

        [Header("Audio")]
        [Tooltip("TR/EN sound played once when the gameplay scene opens. Assign clips inside ResultScreen_TR / ResultScreen_EN.")]
        [SerializeField] private LocalizedWasteSound resultShowSound;

        [Header("VR")]
        [SerializeField] private WasteVrLocomotionGate vrLocomotionGate;
        [SerializeField] private WasteWorldUiPresenter worldUiPresenter;

        [Header("Player")]
        [SerializeField] private Transform playerRoot;
        [SerializeField] private string playerTag = "Player";

        private VisualElement exitOverlay;
        private Image exitIcon;
        private Label exitTitle;
        private Button cancelButton;
        private Button confirmExitButton;

        private VisualElement overlay;
        private Label resultTitle;
        private Label resultSubtitle;
        private Label correctCountLabel;
        private Label correctStatLabel;
        private Label incorrectCountLabel;
        private Label incorrectStatLabel;
        private Label tableWasteHeader;
        private Label tableSelectedHeader;
        private Label tableCorrectHeader;
        private Label tableStatusHeader;
        private ScrollView tableBody;
        private Button restartButton;
        private Button quitButton;

        private readonly PlayerMovementLookFreeze movementLookFreeze = new();
        private readonly List<WasteUncollectedRecord> uncollectedWastes = new();
        private AudioSystem audioSystem;
        private SoundDefinition sceneIntroSound;
        private bool sceneIntroStopped;
        private bool sceneIntroScheduled;
        private bool inputFrozen;
        private bool isRestarting;
        private bool sessionResultsExported;
        private CursorLockMode savedCursorLockState;
        private bool savedCursorVisible;

        public bool IsVisible =>
            IsExitVisible || IsResultVisible;

        public bool IsExitVisible =>
            exitOverlay != null && exitOverlay.style.display == DisplayStyle.Flex;

        public bool IsResultVisible =>
            overlay != null && overlay.style.display == DisplayStyle.Flex;

        private void Awake()
        {
            if (wasteSelectionMenu == null)
                wasteSelectionMenu = GetComponent<WasteSelectionMenu>();

            if (uiDocument == null && wasteSelectionMenu != null)
                uiDocument = wasteSelectionMenu.GetComponent<UIDocument>();

            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (collectTracker == null)
                collectTracker = FindFirstObjectByType<WasteCollectTracker>();

            if (vrLocomotionGate == null)
                vrLocomotionGate = GetComponent<WasteVrLocomotionGate>();

            if (worldUiPresenter == null)
                worldUiPresenter = GetComponent<WasteWorldUiPresenter>();

            ResolveStatusIcons();
        }

        private void ResolveStatusIcons()
        {
#if UNITY_EDITOR
            if (correctStatusIcon == null)
                correctStatusIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(CorrectStatusIconPath);

            if (incorrectStatusIcon == null)
                incorrectStatusIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(IncorrectStatusIconPath);

            if (exitAlertIcon == null)
                exitAlertIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(ExitAlertIconPath);

            if (resultShowSound == null)
                resultShowSound = UnityEditor.AssetDatabase.LoadAssetAtPath<LocalizedWasteSound>(ResultShowSoundPath);
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (resultShowSound != null)
                return;

            resultShowSound = UnityEditor.AssetDatabase.LoadAssetAtPath<LocalizedWasteSound>(ResultShowSoundPath);
        }
#endif

        private void ApplyExitIcon()
        {
            if (exitIcon == null || exitAlertIcon == null)
                return;

            exitIcon.image = exitAlertIcon;
            exitIcon.tintColor = IncorrectStatusColor;
        }

        private void OnEnable()
        {
            sceneIntroScheduled = false;

            EventBus.Register<WasteCollectedEvent>(OnWasteCollected);

            if (collectTracker == null)
                collectTracker = FindFirstObjectByType<WasteCollectTracker>();

            BindResultIntroToSessionEvents();
            SessionLanguageState.LanguageChanged += OnSessionLanguageChanged;

            if (!TryBindUi())
                return;

            HideExit();
            HideResult();
        }

        private void OnDisable()
        {
            SessionLanguageState.LanguageChanged -= OnSessionLanguageChanged;
            UnbindResultIntroFromSessionEvents();
            EventBus.Deregister<WasteCollectedEvent>(OnWasteCollected);

            if (restartButton != null)
                restartButton.clicked -= OnRestartClicked;

            if (quitButton != null)
                quitButton.clicked -= OnQuitClicked;

            if (restartButton != null)
                restartButton.UnregisterCallback<ClickEvent>(OnRestartClickEvent);

            if (quitButton != null)
                quitButton.UnregisterCallback<ClickEvent>(OnQuitClickEvent);

            if (cancelButton != null)
                cancelButton.UnregisterCallback<ClickEvent>(OnCancelClickEvent);

            if (confirmExitButton != null)
                confirmExitButton.UnregisterCallback<ClickEvent>(OnConfirmExitClickEvent);

            if (cancelButton != null)
                cancelButton.clicked -= OnCancelClicked;

            if (confirmExitButton != null)
                confirmExitButton.clicked -= OnConfirmExitClicked;

            if (isRestarting)
                ApplyMenuCursorForLogin();
            else
                RestorePlayerInput();
        }

        private void Update()
        {
            if (!WasteCollectionPlatform.IsPC)
                return;

            if (Keyboard.current == null || !Keyboard.current.tabKey.wasPressedThisFrame)
                return;

            if (wasteSelectionMenu != null && wasteSelectionMenu.IsVisible)
                return;

            if (IsResultVisible)
            {
                HideResult();
                return;
            }

            if (IsExitVisible)
            {
                HideExit();
                return;
            }

            ShowExit();
        }

        private bool TryBindUi()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
                return false;

            VisualElement root = uiDocument.rootVisualElement;
            exitOverlay = root.Q<VisualElement>("ExitOverlay");
            exitIcon = root.Q<Image>("ExitIcon");
            exitTitle = root.Q<Label>("ExitTitle");
            cancelButton = root.Q<Button>("CancelButton");
            confirmExitButton = root.Q<Button>("ConfirmExitButton");
            overlay = root.Q<VisualElement>("ResultOverlay");
            resultTitle = root.Q<Label>("ResultTitle");
            resultSubtitle = root.Q<Label>("ResultSubtitle");
            correctCountLabel = root.Q<Label>("CorrectCount");
            correctStatLabel = root.Q<Label>("CorrectStatLabel");
            incorrectCountLabel = root.Q<Label>("IncorrectCount");
            incorrectStatLabel = root.Q<Label>("IncorrectStatLabel");
            tableWasteHeader = root.Q<Label>("TableWasteHeader");
            tableSelectedHeader = root.Q<Label>("TableSelectedHeader");
            tableCorrectHeader = root.Q<Label>("TableCorrectHeader");
            tableStatusHeader = root.Q<Label>("TableStatusHeader");
            tableBody = root.Q<ScrollView>("TableBody");
            restartButton = root.Q<Button>("RestartButton");
            quitButton = root.Q<Button>("QuitButton");

            if (overlay == null)
            {
                Debug.LogError(
                    "[WasteResultScreenController] ResultOverlay not found. " +
                    "Re-run Waste Collection/Setup Result Screen In Scene after updating WasteSelectionMenu.uxml.",
                    this);
                return false;
            }

            if (exitOverlay == null)
            {
                Debug.LogError(
                    "[WasteResultScreenController] ExitOverlay not found. " +
                    "Re-run Waste Collection/Setup Result Screen In Scene after updating WasteSelectionMenu.uxml.",
                    this);
                return false;
            }

            if (cancelButton != null)
            {
                cancelButton.clicked -= OnCancelClicked;
                cancelButton.clicked += OnCancelClicked;
                cancelButton.UnregisterCallback<ClickEvent>(OnCancelClickEvent);
                cancelButton.RegisterCallback<ClickEvent>(OnCancelClickEvent);
            }

            if (confirmExitButton != null)
            {
                confirmExitButton.clicked -= OnConfirmExitClicked;
                confirmExitButton.clicked += OnConfirmExitClicked;
                confirmExitButton.UnregisterCallback<ClickEvent>(OnConfirmExitClickEvent);
                confirmExitButton.RegisterCallback<ClickEvent>(OnConfirmExitClickEvent);
            }

            ApplyExitIcon();

            if (restartButton != null)
            {
                restartButton.clicked -= OnRestartClicked;
                restartButton.clicked += OnRestartClicked;
                restartButton.UnregisterCallback<ClickEvent>(OnRestartClickEvent);
                restartButton.RegisterCallback<ClickEvent>(OnRestartClickEvent);
            }

            if (quitButton != null)
            {
                quitButton.clicked -= OnQuitClicked;
                quitButton.clicked += OnQuitClicked;
                quitButton.UnregisterCallback<ClickEvent>(OnQuitClickEvent);
                quitButton.RegisterCallback<ClickEvent>(OnQuitClickEvent);
            }

            ApplyLocalizedTexts();
            return true;
        }

        private void OnSessionLanguageChanged()
        {
            if (exitTitle == null && resultTitle == null && !TryBindUi())
                return;

            ApplyLocalizedTexts();

            if (IsResultVisible)
                RefreshContent();
        }

        private void ApplyLocalizedTexts()
        {
            bool english = WasteCollectionLocalization.IsEnglish;

            if (exitTitle != null)
                exitTitle.text = WasteCollectionLocalization.ExitTitle(english);

            if (cancelButton != null)
                cancelButton.text = WasteCollectionLocalization.CancelButton(english);

            if (confirmExitButton != null)
                confirmExitButton.text = WasteCollectionLocalization.ConfirmExitButton(english);

            if (resultTitle != null)
                resultTitle.text = WasteCollectionLocalization.ResultTitle(english);

            if (resultSubtitle != null)
                resultSubtitle.text = WasteCollectionLocalization.ResultSubtitle(english);

            if (correctStatLabel != null)
                correctStatLabel.text = WasteCollectionLocalization.CorrectStatLabel(english);

            if (incorrectStatLabel != null)
                incorrectStatLabel.text = WasteCollectionLocalization.IncorrectStatLabel(english);

            if (tableWasteHeader != null)
                tableWasteHeader.text = WasteCollectionLocalization.TableWasteHeader(english);

            if (tableSelectedHeader != null)
                tableSelectedHeader.text = WasteCollectionLocalization.TableSelectedHeader(english);

            if (tableCorrectHeader != null)
                tableCorrectHeader.text = WasteCollectionLocalization.TableCorrectHeader(english);

            if (tableStatusHeader != null)
                tableStatusHeader.text = WasteCollectionLocalization.TableStatusHeader(english);

            if (restartButton != null)
                restartButton.text = WasteCollectionLocalization.RestartButton(english);

            if (quitButton != null)
                quitButton.text = WasteCollectionLocalization.QuitGameButton(english);
        }

        public void Show()
        {
            ShowResult();
        }

        public void Hide()
        {
            HideResult();
        }

        public void ToggleExitOverlay()
        {
            if (wasteSelectionMenu != null && wasteSelectionMenu.IsVisible)
                return;

            if (IsResultVisible)
            {
                HideResult();
                return;
            }

            if (IsExitVisible)
            {
                HideExit();
                return;
            }

            ShowExit();
        }

        private void ShowExit()
        {
            if (exitOverlay == null && !TryBindUi())
                return;

            ApplyLocalizedTexts();
            exitOverlay.style.display = DisplayStyle.Flex;
            FreezePlayerInput();
            RefreshVrWorldPanelLayout();
        }

        private void HideExit()
        {
            if (exitOverlay == null)
                return;

            exitOverlay.style.display = DisplayStyle.None;

            if (!IsResultVisible)
                RestorePlayerInput();

            RefreshVrWorldPanelLayout();
        }

        private void ShowResult()
        {
            if (overlay == null && !TryBindUi())
                return;

            ApplyLocalizedTexts();
            RefreshContent();
            ExportSessionResultsIfNeeded();
            overlay.style.display = DisplayStyle.Flex;
            FreezePlayerInput();
            RefreshVrWorldPanelLayout(settleFrames: 12);
            StartCoroutine(DeferredResultPanelColliderSync());
        }

        /// <summary>
        /// Office Fire / VR: wait for <see cref="SessionManager.SessionBecameReady"/> (UDP or auto test session).
        /// Waste login scene has no SessionManager — play intro when the scene opens.
        /// </summary>
        private static bool ShouldDeferResultIntroUntilSessionEvent() =>
            FindSessionManager() != null;

        private static SessionManager FindSessionManager()
        {
            if (SessionManager.Instance != null)
                return SessionManager.Instance;

            return FindFirstObjectByType<SessionManager>(FindObjectsInactive.Include);
        }

        private void BindResultIntroToSessionEvents()
        {
            SessionManager.SessionBecameReady -= OnNetworkSessionBecameReady;
            SessionManager.SessionBecameReady += OnNetworkSessionBecameReady;

            if (!ShouldDeferResultIntroUntilSessionEvent())
            {
                TryScheduleSceneIntro();
                return;
            }

            // Do not use SessionDataSO.UserName — asset may contain editor placeholder data.
        }

        private void UnbindResultIntroFromSessionEvents()
        {
            SessionManager.SessionBecameReady -= OnNetworkSessionBecameReady;
        }

        private void OnNetworkSessionBecameReady(PlayerSession session)
        {
            if (session == null || !session.IsActive)
                return;

            TryScheduleSceneIntro();
        }

        private void TryScheduleSceneIntro()
        {
            if (sceneIntroScheduled)
                return;

            sceneIntroScheduled = true;
            StartCoroutine(PlaySceneIntroSoundWhenReady());
        }

        private IEnumerator PlaySceneIntroSoundWhenReady()
        {
            const float audioSystemTimeoutSeconds = 5f;
            float elapsed = 0f;

            while (elapsed < audioSystemTimeoutSeconds)
            {
                EnsureAudioSystem();
                if (audioSystem != null)
                    break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            PlaySceneIntroSound();
        }

        private void PlaySceneIntroSound()
        {
            if (resultShowSound == null)
            {
                Debug.LogWarning(
                    "[WasteResultScreenController] resultShowSound is not assigned; scene intro audio skipped. " +
                    "Assign Assets/Project/WasteCollection/Audio/Result/ResultScreen.asset on WasteResultScreenController.",
                    this);
                return;
            }

            SoundDefinition sound = resultShowSound.Resolve();
            if (sound == null)
            {
                Debug.LogWarning(
                    "[WasteResultScreenController] resultShowSound resolved to null; check ResultScreen_TR / ResultScreen_EN.",
                    this);
                return;
            }

            EnsureAudioSystem();
            if (audioSystem == null)
            {
                Debug.LogWarning(
                    "[WasteResultScreenController] AudioSystem not found; scene intro audio skipped.",
                    this);
                return;
            }

            sceneIntroSound = sound;
            audioSystem.Play(sound);
        }

        private void EnsureAudioSystem()
        {
            if (audioSystem != null)
                return;

            if (AudioSystem.TryGetFromServiceLocator(out audioSystem) && audioSystem != null)
                return;

            audioSystem = FindFirstObjectByType<AudioSystem>();
        }

        // Cuts off the scene intro ("Sonsöz") announcement the moment the first waste is collected.
        private void OnWasteCollected(WasteCollectedEvent evt)
        {
            if (sceneIntroStopped)
                return;

            sceneIntroStopped = true;

            if (audioSystem != null && sceneIntroSound != null)
                audioSystem.StopAllInstances(sceneIntroSound);
        }

        private void ExportSessionResultsIfNeeded()
        {
            if (sessionResultsExported || collectTracker == null)
                return;

            IReadOnlyList<WasteClassificationRecord> classifications = collectTracker.Classifications;
            if (classifications.Count == 0)
                return;

            string path = WasteSessionResultCsvExporter.ExportSession(classifications);
            sessionResultsExported = true;
            RecordLeaderboardScore(classifications);

            if (string.IsNullOrEmpty(path))
                Debug.LogWarning(
                    "[WasteResultScreenController] Local CSV path unavailable (VR build may still have uploaded to PC).",
                    this);
        }

        private static void RecordLeaderboardScore(IReadOnlyList<WasteClassificationRecord> classifications)
        {
            int correct = 0;
            for (int i = 0; i < classifications.Count; i++)
            {
                if (classifications[i].isCorrect)
                    correct++;
            }

            int successPercent = classifications.Count > 0
                ? Mathf.RoundToInt(correct * 100f / classifications.Count)
                : 0;

            WasteSessionResultCsvExporter.ResolveIdentity(out string userName, out string userId);

            WasteLeaderboardStore.TryRecordScore(userName, userId, successPercent);
        }

        private void HideResult()
        {
            if (overlay == null)
                return;

            overlay.style.display = DisplayStyle.None;

            if (!IsExitVisible)
                RestorePlayerInput();

            RefreshVrWorldPanelLayout();
        }

        private void OnCancelClickEvent(ClickEvent evt)
        {
            evt.StopImmediatePropagation();
            OnCancelClicked();
        }

        private void OnConfirmExitClickEvent(ClickEvent evt)
        {
            evt.StopImmediatePropagation();
            OnConfirmExitClicked();
        }

        private void OnRestartClickEvent(ClickEvent evt)
        {
            evt.StopImmediatePropagation();
            OnRestartClicked();
        }

        private void OnQuitClickEvent(ClickEvent evt)
        {
            evt.StopImmediatePropagation();
            OnQuitClicked();
        }

        private void RefreshVrWorldPanelLayout(int settleFrames = 4)
        {
            if (!WasteCollectionPlatform.ShouldUseVrPresentation())
                return;

            if (worldUiPresenter == null)
                worldUiPresenter = GetComponent<WasteWorldUiPresenter>();

            worldUiPresenter?.NotifyContentLayoutChanged(settleFrames);
        }

        /// <summary>
        /// Result table layout and world-space pick meshes settle a frame after display:flex.
        /// Without this, the first XR trigger on Tekrar Oyna often misses the collider.
        /// </summary>
        private IEnumerator DeferredResultPanelColliderSync()
        {
            if (!WasteCollectionPlatform.ShouldUseVrPresentation())
                yield break;

            yield return null;
            RefreshVrWorldPanelLayout(settleFrames: 12);
            yield return new WaitForEndOfFrame();
            RefreshVrWorldPanelLayout(settleFrames: 8);
            yield return null;
            RefreshVrWorldPanelLayout(settleFrames: 4);
        }

        private void OnCancelClicked()
        {
            HideExit();
        }

        private void OnConfirmExitClicked()
        {
            if (exitOverlay != null)
                exitOverlay.style.display = DisplayStyle.None;

            ShowResult();
        }

        private void RefreshContent()
        {
            IReadOnlyList<WasteClassificationRecord> records = collectTracker != null
                ? collectTracker.Classifications
                : System.Array.Empty<WasteClassificationRecord>();

            int correct = 0;
            int incorrect = 0;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].isCorrect)
                    correct++;
                else
                    incorrect++;
            }

            if (correctCountLabel != null)
                correctCountLabel.text = correct.ToString();

            if (incorrectCountLabel != null)
                incorrectCountLabel.text = incorrect.ToString();

            if (tableBody == null)
                return;

            tableBody.Clear();

            if (collectTracker != null)
                collectTracker.GetUncollectedSceneWastes(uncollectedWastes);
            else
                uncollectedWastes.Clear();

            if (records.Count == 0 && uncollectedWastes.Count == 0)
            {
                tableBody.Add(CreateEmptyRow(WasteCollectionLocalization.EmptyClassification(WasteCollectionLocalization.IsEnglish)));
                return;
            }

            for (int i = 0; i < records.Count; i++)
                tableBody.Add(CreateTableRow(records[i]));

            for (int i = 0; i < uncollectedWastes.Count; i++)
                tableBody.Add(CreateNotFoundTableRow(uncollectedWastes[i]));
        }

        private static VisualElement CreateEmptyRow(string message)
        {
            var row = new VisualElement();
            row.AddToClassList("table-row");

            var label = new Label(message);
            label.AddToClassList("table-cell");
            label.style.flexGrow = 1;
            row.Add(label);
            return row;
        }

        private VisualElement CreateTableRow(WasteClassificationRecord record)
        {
            var row = new VisualElement();
            row.AddToClassList("table-row");

            row.Add(CreateTableCell(WasteNameCatalog.GetDisplayName(record.wasteName), "col-1"));
            row.Add(CreateTableCell(WasteBinCatalog.GetBinName(record.selectedBinId), "col-2"));
            row.Add(CreateTableCell(WasteBinCatalog.GetBinName(record.correctBinId), "col-3"));
            row.Add(CreateStatusCell(record.isCorrect));

            return row;
        }

        private VisualElement CreateNotFoundTableRow(WasteUncollectedRecord record)
        {
            var row = new VisualElement();
            row.AddToClassList("table-row");

            row.Add(CreateTableCell(WasteNameCatalog.GetDisplayName(record.wasteName), "col-1"));
            row.Add(CreateTableCell("-", "col-2"));
            row.Add(CreateTableCell(WasteBinCatalog.GetBinName(record.correctBinId), "col-3"));
            row.Add(CreateNotFoundStatusCell());

            return row;
        }

        private VisualElement CreateNotFoundStatusCell()
        {
            var column = new VisualElement();
            column.AddToClassList("table-col");
            column.AddToClassList("col-4");

            var badge = new VisualElement();
            badge.AddToClassList("status-badge");
            badge.AddToClassList("warning");

            if (exitAlertIcon != null)
            {
                var icon = new Image
                {
                    image = exitAlertIcon,
                    scaleMode = ScaleMode.ScaleToFit,
                    tintColor = NotFoundStatusColor
                };
                icon.AddToClassList("status-icon");
                badge.Add(icon);
            }

            var statusText = new Label(WasteCollectionLocalization.StatusNotFound(WasteCollectionLocalization.IsEnglish));
            statusText.AddToClassList("status-badge-text");
            statusText.AddToClassList("warning");
            badge.Add(statusText);
            column.Add(badge);
            return column;
        }

        private static VisualElement CreateTableCell(string text, string columnClass)
        {
            var column = new VisualElement();
            column.AddToClassList("table-col");
            column.AddToClassList(columnClass);

            var label = new Label(text);
            label.AddToClassList("table-cell");
            column.Add(label);
            return column;
        }

        private VisualElement CreateStatusCell(bool isCorrect)
        {
            var column = new VisualElement();
            column.AddToClassList("table-col");
            column.AddToClassList("col-4");

            var badge = new VisualElement();
            badge.AddToClassList("status-badge");
            badge.AddToClassList(isCorrect ? "success" : "danger");

            Texture2D iconTexture = isCorrect ? correctStatusIcon : incorrectStatusIcon;
            if (iconTexture != null)
            {
                var icon = new Image
                {
                    image = iconTexture,
                    scaleMode = ScaleMode.ScaleToFit,
                    tintColor = isCorrect ? CorrectStatusColor : IncorrectStatusColor
                };
                icon.AddToClassList("status-icon");
                badge.Add(icon);
            }

            var statusText = new Label(isCorrect
                ? WasteCollectionLocalization.StatusCorrect(WasteCollectionLocalization.IsEnglish)
                : WasteCollectionLocalization.StatusIncorrect(WasteCollectionLocalization.IsEnglish));
            statusText.AddToClassList("status-badge-text");
            statusText.AddToClassList(isCorrect ? "success" : "danger");
            badge.Add(statusText);
            column.Add(badge);
            return column;
        }

        private void OnQuitClicked()
        {
            ExportSessionResultsIfNeeded();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnRestartClicked()
        {
            if (isRestarting)
                return;

            SessionFlowRestarter.PrepareForNewSession();

            if (collectTracker != null)
                collectTracker.ClearSession();

            sessionResultsExported = false;
            HideOverlaysWithoutRestoringInput();
            ApplyMenuCursorForLogin();

            if (ServiceLocator.TryGet(out OfficeGameModulesBootstrapper bootstrapper) && bootstrapper != null)
            {
                isRestarting = true;
                if (restartButton != null)
                    restartButton.SetEnabled(false);

                if (WasteCollectionPlatform.IsVR)
                    bootstrapper.LoadWasteCollectorGameplay();
                else
                    bootstrapper.LoadWasteLogin();

                return;
            }

            StartCoroutine(RestartLoginRoutine());
        }

        private void HideOverlaysWithoutRestoringInput()
        {
            if (exitOverlay != null)
                exitOverlay.style.display = DisplayStyle.None;

            if (overlay != null)
                overlay.style.display = DisplayStyle.None;
        }

        private void ApplyMenuCursorForLogin()
        {
            inputFrozen = false;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private IEnumerator RestartLoginRoutine()
        {
            isRestarting = true;
            HideOverlaysWithoutRestoringInput();
            ApplyMenuCursorForLogin();

            if (restartButton != null)
                restartButton.SetEnabled(false);

            if (!TryResolveSceneLoader(out ISceneLoaderService loader))
            {
                Debug.LogError(
                    "[WasteResultScreenController] Scene loader not found. Ensure SceneLoader is registered on ServiceLocator.",
                    this);
                isRestarting = false;
                if (restartButton != null)
                    restartButton.SetEnabled(true);
                yield break;
            }

            Task loadTask;
            try
            {
                string sceneGroup = WasteCollectionPlatform.IsVR
                    ? OfficeGameModulesBootstrapper.WasteCollectorSceneGroup
                    : OfficeGameModulesBootstrapper.WasteLoginSceneGroup;
                loadTask = loader.LoadScene(sceneGroup);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WasteResultScreenController] Restart load failed: {ex.Message}", this);
                isRestarting = false;
                if (restartButton != null)
                    restartButton.SetEnabled(true);
                yield break;
            }

            if (loadTask == null)
            {
                isRestarting = false;
                if (restartButton != null)
                    restartButton.SetEnabled(true);
                yield break;
            }

            while (!loadTask.IsCompleted)
                yield return null;

            if (loadTask.IsFaulted)
            {
                Debug.LogError(
                    "[WasteResultScreenController] Restart load task faulted.",
                    this);
                if (loadTask.Exception != null)
                    Debug.LogException(loadTask.Exception.GetBaseException(), this);

                isRestarting = false;
                if (restartButton != null)
                    restartButton.SetEnabled(true);
            }
        }

        private static bool TryResolveSceneLoader(out ISceneLoaderService loader)
        {
            if (ServiceLocator.TryGet(out ISceneLoaderService service) && service != null)
            {
                loader = service;
                return true;
            }

            if (ServiceLocator.TryGet(out SceneLoader concrete) && concrete != null)
            {
                loader = concrete;
                return true;
            }

            SceneLoader found = FindFirstObjectByType<SceneLoader>();
            if (found != null)
            {
                loader = found;
                return true;
            }

            loader = null;
            return false;
        }

        private void FreezePlayerInput()
        {
            if (inputFrozen)
                return;

            if (WasteCollectionPlatform.IsVR)
            {
                inputFrozen = true;
                return;
            }

            Transform root = ResolvePlayerRoot();
            movementLookFreeze.Freeze(root);

            if (ServiceLocator.TryGet(out IPlayerService playerService))
                playerService.SetPlayerInputEnabled(false);

            savedCursorLockState = UnityEngine.Cursor.lockState;
            savedCursorVisible = UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            inputFrozen = true;
        }

        private void RestorePlayerInput()
        {
            if (!inputFrozen)
                return;

            if (WasteCollectionPlatform.IsVR)
            {
                inputFrozen = false;
                return;
            }

            movementLookFreeze.Restore();

            if (ServiceLocator.TryGet(out IPlayerService playerService))
                playerService.SetPlayerInputEnabled(true);

            if (ServiceLocator.TryGet(out Woi.InputSystem.InputManager inputManager))
                inputManager.EnsurePcGameplayInputEnabled();

            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            inputFrozen = false;
        }

        private Transform ResolvePlayerRoot()
        {
            if (playerRoot != null)
                return playerRoot;

            if (ServiceLocator.TryGet(out IPlayerService playerService))
            {
                Transform serviceRoot = playerService.GetPlayerTransform();
                if (serviceRoot != null)
                    return serviceRoot;
            }

            if (!string.IsNullOrWhiteSpace(playerTag))
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
                if (taggedPlayer != null)
                    return taggedPlayer.transform;
            }

            return null;
        }
    }
}
