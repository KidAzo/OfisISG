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
        private const string TargetSceneGroup = OfficeGameModulesBootstrapper.WasteCollectorSceneGroup;

        private const string LangEnglish = "en";
        private const string LangTurkish = "tr";

        private static readonly Color LoginIconTint = new(0f, 1f, 0.698f, 1f);

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Texture2D loginIcon;

        private TextField userNameField;
        private TextField userIdField;
        private DropdownField languageDropdown;
        private Button startButton;
        private Label errorLabel;
        private VisualElement loginIconHost;
        private Label leaderboardTitle;
        private VisualElement leaderboardRows;
        private Coroutine bindRoutine;

        private bool isLoading;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            ResolveLoginIcon();
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
#if UNITY_EDITOR
            if (loginIcon == null)
                loginIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(LoginIconPath);
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
            loginIconHost = root.Q<VisualElement>("LoginIconHost");
            leaderboardTitle = root.Q<Label>("LeaderboardTitle");
            leaderboardRows = root.Q<VisualElement>("leaderboard-rows");

            ApplyLoginIcon();
            ClearError();
            return startButton != null;
        }

        private void ApplyLoginIcon()
        {
            if (loginIconHost == null || loginIcon == null)
                return;

            loginIconHost.style.width = 32f;
            loginIconHost.style.height = 32f;
            loginIconHost.style.flexShrink = 0;
            loginIconHost.style.backgroundImage = new StyleBackground(loginIcon);
            loginIconHost.style.unityBackgroundImageTintColor = LoginIconTint;
            loginIconHost.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        }

        private void OnLanguageChanged(ChangeEvent<string> evt)
        {
            ApplyLanguage(evt.newValue);
        }

        private void ApplyLanguage(string dropdownLabel)
        {
            bool isTurkish = LanguageCodeFromDropdownLabel(dropdownLabel) == LangTurkish;

            if (userNameField != null)
                userNameField.label = isTurkish ? "Ad Soyad" : "Full Name";

            if (userIdField != null)
                userIdField.label = isTurkish ? "Kullanıcı ID" : "User ID";

            if (startButton != null)
                startButton.text = isTurkish ? "OYUNU BAŞLAT" : "START GAME";

            if (leaderboardTitle != null)
                leaderboardTitle.text = isTurkish ? "BAŞARI TABLOSU" : "LEADERBOARD";
        }

        private void OnStartClicked()
        {
            if (isLoading)
                return;

            string userName = userNameField != null ? userNameField.value.Trim() : string.Empty;
            string userId = userIdField != null ? userIdField.value.Trim() : string.Empty;
            string languageCode = LanguageCodeFromDropdownLabel(
                languageDropdown != null ? languageDropdown.value : "Türkçe");

            if (string.IsNullOrWhiteSpace(userName))
            {
                ShowError(languageCode == LangEnglish
                    ? "Please enter your name."
                    : "Lütfen ad soyad girin.");
                return;
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                ShowError(languageCode == LangEnglish
                    ? "Please enter your user ID."
                    : "Lütfen kullanıcı ID girin.");
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
                ShowError("Scene loader bulunamadı. Waste Collection/Setup Login Scene menüsünü çalıştırın.");
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
                ShowError(loadTask.Exception?.GetBaseException().Message ?? "Sahne yüklenemedi.");
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
            if (string.Equals(label?.Trim(), "English", System.StringComparison.OrdinalIgnoreCase))
                return LangEnglish;

            return LangTurkish;
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
