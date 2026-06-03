using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Woi.UI.Popups.Localization;

namespace Woi.DataHandler
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(-100)]
    public sealed class SessionProfileOverlayController : MonoBehaviour
    {
        private const string WaitingName = "—";
        private const string WaitingId = "—";
        private const string ActiveLangButtonClass = "session-lang-button--active";

        [SerializeField] private UIDocument uiDocument;

        private VisualElement overlayRoot;
        private Label sessionTitleSub;
        private Label sessionTitleMain;
        private Label statusLabel;
        private Label profileSectionLabel;
        private Label playerNameFieldLabel;
        private Label playerIdFieldLabel;
        private Label languageSectionLabel;
        private Button languageTrButton;
        private Button languageEnButton;
        private Label playerNameValue;
        private Label playerIdValue;
        private Coroutine showRoutine;
        private bool languageButtonsRegistered;
        private bool showingReadySession;
        private PlayerSession boundSession;
        private string activeLanguageCode = LocalizationService.Turkish;

        public bool IsVisible => overlayRoot != null && overlayRoot.style.display == DisplayStyle.Flex;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
        }

        private void OnDisable()
        {
            UnregisterLanguageButtons();
        }

        private void Update()
        {
            if (!IsVisible)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                SelectLanguage(LocalizationService.Turkish);

            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                SelectLanguage(LocalizationService.English);
        }

        public void SetVisible(bool visible)
        {
            if (!TryBindUi())
                return;

            // Same as OfficeFireInteractPopupHost / Waste modals: visibility only, UIDocument stays alive.
            overlayRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (visible)
                EnableInteraction();
            else
                ApplyPickingMode(PickingMode.Ignore);
        }

        public void EnableInteraction()
        {
            if (!TryBindUi())
                return;

            ApplyPickingMode(PickingMode.Position);

            VisualElement card = overlayRoot?.Q<VisualElement>(className: "session-profile-card");
            if (card != null)
                card.pickingMode = PickingMode.Position;
        }

        public void DisableInteraction()
        {
            ApplyPickingMode(PickingMode.Ignore);
        }

        public void ShowWaiting()
        {
            showingReadySession = false;
            boundSession = null;

            if (showRoutine != null)
                StopCoroutine(showRoutine);

            showRoutine = StartCoroutine(ShowWhenDocumentReady(ApplyWaitingContent));
        }

        public void ShowSession(PlayerSession session)
        {
            if (session == null)
                return;

            showingReadySession = true;
            boundSession = session;

            if (showRoutine != null)
                StopCoroutine(showRoutine);

            showRoutine = StartCoroutine(ShowWhenDocumentReady(() => ApplySessionContent(session)));
        }

        private IEnumerator ShowWhenDocumentReady(System.Action applyContent)
        {
            if (uiDocument != null)
                uiDocument.enabled = true;

            const int maxFrames = 60;
            for (int i = 0; i < maxFrames; i++)
            {
                if (TryBindUi())
                    break;

                yield return null;
            }

            if (!TryBindUi())
            {
                Debug.LogError(
                    "[SessionProfileOverlayController] UIDocument root not ready — check UXML assignment.",
                    this);
                showRoutine = null;
                yield break;
            }

            RegisterLanguageButtons();
            activeLanguageCode = SessionProfileLanguagePreference.ResolveForOverlay();
            UpdateLanguageButtonStyles();
            ApplyLanguage(activeLanguageCode);

            SetVisible(true);
            applyContent?.Invoke();
            showRoutine = null;
        }

        private void RegisterLanguageButtons()
        {
            if (languageButtonsRegistered)
                return;

            WireLanguageButton(languageTrButton, OnTurkishClicked, OnTurkishPointerUp);
            WireLanguageButton(languageEnButton, OnEnglishClicked, OnEnglishPointerUp);
            languageButtonsRegistered = true;
        }

        private void UnregisterLanguageButtons()
        {
            if (!languageButtonsRegistered)
                return;

            UnwireLanguageButton(languageTrButton, OnTurkishClicked, OnTurkishPointerUp);
            UnwireLanguageButton(languageEnButton, OnEnglishClicked, OnEnglishPointerUp);
            languageButtonsRegistered = false;
        }

        private static void WireLanguageButton(Button button, System.Action onClick, EventCallback<PointerUpEvent> onPointerUp)
        {
            if (button == null)
                return;

            button.focusable = true;
            button.pickingMode = PickingMode.Position;
            button.clicked -= onClick;
            button.clicked += onClick;
            button.UnregisterCallback(onPointerUp);
            button.RegisterCallback(onPointerUp, TrickleDown.NoTrickleDown);
        }

        private static void UnwireLanguageButton(Button button, System.Action onClick, EventCallback<PointerUpEvent> onPointerUp)
        {
            if (button == null)
                return;

            button.clicked -= onClick;
            button.UnregisterCallback(onPointerUp);
        }

        private void OnTurkishPointerUp(PointerUpEvent evt)
        {
            if (evt.button == 0)
                OnTurkishClicked();
        }

        private void OnEnglishPointerUp(PointerUpEvent evt)
        {
            if (evt.button == 0)
                OnEnglishClicked();
        }

        private void OnTurkishClicked() => SelectLanguage(LocalizationService.Turkish);

        private void OnEnglishClicked() => SelectLanguage(LocalizationService.English);

        private void SelectLanguage(string languageCode)
        {
            if (string.Equals(activeLanguageCode, languageCode, System.StringComparison.OrdinalIgnoreCase))
                return;

            activeLanguageCode = languageCode;
            SessionProfileLanguagePreference.RecordUserChoice(languageCode);
            UpdateLanguageButtonStyles();
            ApplyLanguage(languageCode);
        }

        private void UpdateLanguageButtonStyles()
        {
            bool turkish = !SessionProfileLocalization.IsEnglishLanguageCode(activeLanguageCode);

            if (languageTrButton != null)
                languageTrButton.EnableInClassList(ActiveLangButtonClass, turkish);

            if (languageEnButton != null)
                languageEnButton.EnableInClassList(ActiveLangButtonClass, !turkish);
        }

        private void ApplyLanguage(string languageCode)
        {
            bool english = SessionProfileLocalization.IsEnglishLanguageCode(languageCode);
            ApplyLanguageToGame(languageCode);

            if (sessionTitleSub != null)
                sessionTitleSub.text = SessionProfileLocalization.TitleSub(english);

            if (sessionTitleMain != null)
                sessionTitleMain.text = SessionProfileLocalization.TitleMain(english);

            if (profileSectionLabel != null)
                profileSectionLabel.text = SessionProfileLocalization.ProfileSection(english);

            if (playerNameFieldLabel != null)
                playerNameFieldLabel.text = SessionProfileLocalization.NameFieldLabel(english);

            if (playerIdFieldLabel != null)
                playerIdFieldLabel.text = SessionProfileLocalization.IdFieldLabel(english);

            if (languageSectionLabel != null)
                languageSectionLabel.text = SessionProfileLocalization.LanguageSection(english);

            if (showingReadySession && boundSession != null)
                ApplySessionContent(boundSession);
            else
                ApplyWaitingContent();
        }

        private static void ApplyLanguageToGame(string languageCode) =>
            SessionProfileLanguagePreference.ApplyToGame(languageCode);

        private void ApplyWaitingContent()
        {
            bool english = SessionProfileLocalization.IsEnglishLanguageCode(activeLanguageCode);

            statusLabel.text = SessionProfileLocalization.StatusWaiting(english);
            statusLabel.RemoveFromClassList("session-status-label--ready");

            playerNameValue.text = WaitingName;
            playerIdValue.text = WaitingId;
            playerNameValue.EnableInClassList("session-field-value--waiting", true);
            playerNameValue.EnableInClassList("session-field-value--ready", false);
            playerIdValue.EnableInClassList("session-field-value--waiting", true);
            playerIdValue.EnableInClassList("session-field-value--ready", false);
        }

        private void ApplySessionContent(PlayerSession session)
        {
            bool english = SessionProfileLocalization.IsEnglishLanguageCode(activeLanguageCode);

            statusLabel.text = SessionProfileLocalization.StatusReady(english);
            statusLabel.AddToClassList("session-status-label--ready");

            playerNameValue.text = string.IsNullOrWhiteSpace(session.PlayerName)
                ? WaitingName
                : session.PlayerName.Trim();
            playerIdValue.text = session.PlayerID > 0
                ? session.PlayerID.ToString()
                : WaitingId;

            playerNameValue.EnableInClassList("session-field-value--waiting", false);
            playerNameValue.EnableInClassList("session-field-value--ready", true);
            playerIdValue.EnableInClassList("session-field-value--waiting", false);
            playerIdValue.EnableInClassList("session-field-value--ready", true);
        }

        private void ApplyPickingMode(PickingMode mode)
        {
            if (overlayRoot == null)
                return;

            SetPickingModeRecursive(overlayRoot, mode);
        }

        private static void SetPickingModeRecursive(VisualElement element, PickingMode mode)
        {
            if (element == null)
                return;

            element.pickingMode = mode;
            int count = element.childCount;
            for (int i = 0; i < count; i++)
                SetPickingModeRecursive(element[i], mode);
        }

        private bool TryBindUi()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (uiDocument == null || uiDocument.rootVisualElement == null)
                return false;

            if (overlayRoot != null)
                return true;

            VisualElement root = uiDocument.rootVisualElement;
            overlayRoot = root.Q<VisualElement>("SessionOverlayRoot");
            sessionTitleSub = root.Q<Label>("SessionTitleSub");
            sessionTitleMain = root.Q<Label>("SessionTitleMain");
            statusLabel = root.Q<Label>("SessionStatusLabel");
            profileSectionLabel = root.Q<Label>("SessionProfileSectionLabel");
            playerNameFieldLabel = root.Q<Label>("PlayerNameFieldLabel");
            playerIdFieldLabel = root.Q<Label>("PlayerIdFieldLabel");
            languageSectionLabel = root.Q<Label>("SessionLanguageSectionLabel");
            languageTrButton = root.Q<Button>("SessionLangTrButton");
            languageEnButton = root.Q<Button>("SessionLangEnButton");
            playerNameValue = root.Q<Label>("PlayerNameValue");
            playerIdValue = root.Q<Label>("PlayerIdValue");

            if (overlayRoot == null || statusLabel == null || playerNameValue == null || playerIdValue == null)
            {
                Debug.LogError(
                    "[SessionProfileOverlayController] UXML bindings missing. " +
                    "Assign SessionProfileOverlay.uxml on the UIDocument.",
                    this);
                return false;
            }

            return true;
        }
    }
}
