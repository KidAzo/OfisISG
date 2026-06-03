using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.UIElements;
using Woi.Events.Data;
using Woi.OfficeFire;
using Woi.Settings;
using WOI.Modules.SDK;

namespace Woi.WasteCollectionMode
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class WasteLoginScreenController : MonoBehaviour
    {
        private const string LoginIconPath =
            "Assets/Project/WasteCollection/UI/IconsPng/trash-2.png";
        private const string GaussBackgroundPath =
            "Assets/Project/Sprites/gaussImage.jpg";
        private const string TargetSceneGroup = OfficeGameModulesBootstrapper.WasteCollectorSceneGroup;

        private static readonly Color LoginIconTint = new(0f, 1f, 0.698f, 1f);

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Texture2D loginIcon;
        [SerializeField] private Texture2D gaussBackgroundImage;

        private TextField userNameField;
        private TextField userIdField;
        private DropdownField languageDropdown;
        private Button startButton;
        private Label errorLabel;
        private Image loginIconImage;
        private VisualElement loginBackground;
        private Label loginTitleSub;
        private Label loginTitleMain;
        private Label profileSectionLabel;
        private Label languageSectionLabel;
        private Label leaderboardTitle;
        private VisualElement leaderboardRows;
        private Coroutine bindRoutine;

        private bool isLoading;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            ResolveLoginIcon();
            ResolveGaussBackground();
        }

        private void OnEnable()
        {
            ApplyMenuCursor();

            if (bindRoutine != null)
                StopCoroutine(bindRoutine);

            bindRoutine = StartCoroutine(BindUiWhenReady());
        }

        private void OnDisable()
        {
            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
                bindRoutine = null;
            }

            if (startButton != null)
                startButton.clicked -= OnStartClicked;

            if (languageDropdown != null)
                languageDropdown.UnregisterValueChangedCallback(OnLanguageChanged);
        }

        private IEnumerator BindUiWhenReady()
        {
            int safety = 120;
            while (safety-- > 0 && enabled && (uiDocument == null || uiDocument.rootVisualElement == null))
                yield return null;

            if (!enabled || uiDocument == null || uiDocument.rootVisualElement == null)
                yield break;

            if (!TryBindUi())
                yield break;

            if (startButton != null)
            {
                startButton.clicked -= OnStartClicked;
                startButton.clicked += OnStartClicked;
            }

            if (languageDropdown != null)
            {
                languageDropdown.UnregisterValueChangedCallback(OnLanguageChanged);
                languageDropdown.RegisterValueChangedCallback(OnLanguageChanged);
                ApplyLanguage(languageDropdown.value);
            }

            RefreshLeaderboard();
            ApplyMenuCursor();
            bindRoutine = null;
        }

        private static void ApplyMenuCursor()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void ResolveLoginIcon()
        {
            if (loginIcon != null)
                return;

#if UNITY_EDITOR
            loginIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(LoginIconPath);
#endif
        }

        private void ResolveGaussBackground()
        {
            if (gaussBackgroundImage != null)
                return;

#if UNITY_EDITOR
            gaussBackgroundImage = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(GaussBackgroundPath);
#endif
        }

        private bool TryBindUi()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
                return false;

            VisualElement root = uiDocument.rootVisualElement;
            userNameField = root.Q<TextField>("UserNameField");
            userIdField = root.Q<TextField>("UserIdField");
            languageDropdown = root.Q<DropdownField>("LanguageDropdown");
            startButton = root.Q<Button>("StartButton");
            errorLabel = root.Q<Label>("LoginErrorLabel");
            loginIconImage = root.Q<Image>("LoginIconHost");
            loginBackground = root.Q<VisualElement>("LoginBackground");
            loginTitleSub = root.Q<Label>("LoginTitleSub");
            loginTitleMain = root.Q<Label>("LoginTitleMain");
            profileSectionLabel = root.Q<Label>("ProfileSectionLabel");
            languageSectionLabel = root.Q<Label>("LanguageSectionLabel");
            leaderboardTitle = root.Q<Label>("LeaderboardTitle");
            leaderboardRows = root.Q<VisualElement>("leaderboard-rows");

            ApplyLoginIcon();
            ApplyGaussBackground();
            ClearError();
            return startButton != null;
        }

        private void ApplyGaussBackground()
        {
            if (loginBackground == null)
                return;

            ResolveGaussBackground();

            if (gaussBackgroundImage == null)
                return;

            loginBackground.style.backgroundImage = new StyleBackground(gaussBackgroundImage);
            loginBackground.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            loginBackground.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            loginBackground.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
        }

        private void ApplyLoginIcon()
        {
            if (loginIconImage == null)
                return;

            ResolveLoginIcon();

            if (loginIcon != null)
                loginIconImage.image = loginIcon;

            loginIconImage.tintColor = LoginIconTint;
            loginIconImage.scaleMode = ScaleMode.ScaleToFit;
        }

        private void OnLanguageChanged(ChangeEvent<string> evt)
        {
            ApplyLanguage(evt.newValue);
        }

        private void ApplyLanguage(string dropdownLabel)
        {
            bool english = WasteCollectionLocalization.IsEnglishFromDropdown(dropdownLabel);

            if (loginTitleSub != null)
                loginTitleSub.text = WasteCollectionLocalization.LoginTitleSub(english);

            if (loginTitleMain != null)
                loginTitleMain.text = WasteCollectionLocalization.LoginTitleMain(english);

            if (profileSectionLabel != null)
                profileSectionLabel.text = WasteCollectionLocalization.ProfileSection(english);

            if (languageSectionLabel != null)
                languageSectionLabel.text = WasteCollectionLocalization.LanguageSection(english);

            if (userNameField != null)
                userNameField.label = WasteCollectionLocalization.UserNameLabel(english);

            if (userIdField != null)
                userIdField.label = WasteCollectionLocalization.UserIdLabel(english);

            if (startButton != null)
                startButton.text = WasteCollectionLocalization.StartButton(english);

            if (leaderboardTitle != null)
                leaderboardTitle.text = WasteCollectionLocalization.LeaderboardTitle(english);
        }

        private void OnStartClicked()
        {
            if (isLoading)
                return;

            string userName = userNameField != null ? userNameField.value.Trim() : string.Empty;
            string userId = userIdField != null ? userIdField.value.Trim() : string.Empty;
            string languageCode = LanguageCodeFromDropdownLabel(
                languageDropdown != null ? languageDropdown.value : "Türkçe");
            bool english = languageCode == WasteCollectionLocalization.LangEnglish;

            if (string.IsNullOrWhiteSpace(userName))
            {
                ShowError(WasteCollectionLocalization.ErrorNameRequired(english));
                return;
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                ShowError(WasteCollectionLocalization.ErrorIdRequired(english));
                return;
            }

            WasteLoginSession.Set(userName, userId, languageCode);
            GameSessionData.Set(new List<FireClass>(), userName, userId);

            ClearError();
            StartCoroutine(LoadGameplayRoutine());
        }

        private IEnumerator LoadGameplayRoutine()
        {
            isLoading = true;

            if (startButton != null)
                startButton.SetEnabled(false);

            if (!TryResolveSceneLoader(out ISceneLoaderService loader))
            {
                bool english = languageDropdown != null &&
                    WasteCollectionLocalization.IsEnglishFromDropdown(languageDropdown.value);
                ShowError(WasteCollectionLocalization.ErrorSceneLoaderMissing(english));
                isLoading = false;
                if (startButton != null)
                    startButton.SetEnabled(true);
                yield break;
            }

            Task loadTask;
            try
            {
                loadTask = loader.LoadScene(TargetSceneGroup);
            }
            catch (System.Exception ex)
            {
                ShowError(ex.Message);
                isLoading = false;
                if (startButton != null)
                    startButton.SetEnabled(true);
                yield break;
            }

            if (loadTask == null)
            {
                isLoading = false;
                if (startButton != null)
                    startButton.SetEnabled(true);
                yield break;
            }

            while (!loadTask.IsCompleted)
                yield return null;

            if (loadTask.IsFaulted)
            {
                bool english = languageDropdown != null &&
                    WasteCollectionLocalization.IsEnglishFromDropdown(languageDropdown.value);
                ShowError(loadTask.Exception?.GetBaseException().Message
                    ?? WasteCollectionLocalization.ErrorSceneLoadFailed(english));
                isLoading = false;
                if (startButton != null)
                    startButton.SetEnabled(true);
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

        private static string LanguageCodeFromDropdownLabel(string label)
        {
            if (WasteCollectionLocalization.IsEnglishFromDropdown(label))
                return WasteCollectionLocalization.LangEnglish;

            return WasteCollectionLocalization.LangTurkish;
        }

        private void ShowError(string message)
        {
            if (errorLabel == null)
            {
                Debug.LogWarning($"[WasteLoginScreenController] {message}", this);
                return;
            }

            errorLabel.text = message;
            errorLabel.style.display = DisplayStyle.Flex;
        }

        private void ClearError()
        {
            if (errorLabel == null)
                return;

            errorLabel.text = string.Empty;
            errorLabel.style.display = DisplayStyle.None;
        }

        private void RefreshLeaderboard()
        {
            if (leaderboardRows == null)
                return;

            leaderboardRows.Clear();
            IReadOnlyList<string> lines = WasteLeaderboardStore.GetDisplayLines();
            int scoreRank = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                var row = new Label(line);
                row.AddToClassList("leaderboard-row");

                if (string.Equals(line, WasteLeaderboardStore.EmptySlotDisplay, System.StringComparison.Ordinal))
                {
                    row.AddToClassList("leaderboard-row--empty");
                }
                else
                {
                    if (scoreRank == 0)
                        row.AddToClassList("leaderboard-row--rank1");
                    else if (scoreRank == 1)
                        row.AddToClassList("leaderboard-row--rank2");
                    else if (scoreRank == 2)
                        row.AddToClassList("leaderboard-row--rank3");
                    else
                        row.AddToClassList("leaderboard-row--rank-plain");

                    scoreRank++;
                }

                leaderboardRows.Add(row);
            }
        }
    }
}
