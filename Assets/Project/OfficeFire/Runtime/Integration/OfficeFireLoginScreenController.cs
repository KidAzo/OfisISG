using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Woi.Events.Data;
using Woi.Settings;
using Woi.UI.Popups.Localization;
using WOI.Modules.SDK;

namespace Woi.OfficeFire
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class OfficeFireLoginScreenController : MonoBehaviour
    {
        private const string TargetSceneGroup = "FireModule_Office";

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Texture2D gaussBackgroundImage;

        private TextField userNameField;
        private TextField userIdField;
        private DropdownField scenarioDropdown;
        private DropdownField languageDropdown;
        private Button startButton;
        private Label errorLabel;
        private VisualElement loginBackground;
        private Label loginTitleSub;
        private Label loginTitleMain;
        private Label profileSectionLabel;
        private Label scenarioSectionLabel;
        private Label languageSectionLabel;
        private Label leaderboardTitle;
        private VisualElement leaderboardRows;
        private Coroutine bindRoutine;

        private bool isLoading;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

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
                EnsureDefaultLanguageSelection();
                languageDropdown.RegisterValueChangedCallback(OnLanguageChanged);
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

        private void ResolveGaussBackground()
        {
            if (gaussBackgroundImage != null)
                return;

#if UNITY_EDITOR
            gaussBackgroundImage = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Project/Sprites/gaussImage.jpg");
#endif
        }

        private bool TryBindUi()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
                return false;

            VisualElement root = uiDocument.rootVisualElement;
            userNameField = root.Q<TextField>("UserNameField");
            userIdField = root.Q<TextField>("UserIdField");
            scenarioDropdown = root.Q<DropdownField>("ScenarioDropdown");
            languageDropdown = root.Q<DropdownField>("LanguageDropdown");
            startButton = root.Q<Button>("StartButton");
            errorLabel = root.Q<Label>("LoginErrorLabel");
            loginBackground = root.Q<VisualElement>("LoginBackground");
            loginTitleSub = root.Q<Label>("LoginTitleSub");
            loginTitleMain = root.Q<Label>("LoginTitleMain");
            profileSectionLabel = root.Q<Label>("ProfileSectionLabel");
            scenarioSectionLabel = root.Q<Label>("ScenarioSectionLabel");
            languageSectionLabel = root.Q<Label>("LanguageSectionLabel");
            leaderboardTitle = root.Q<Label>("LeaderboardTitle");
            leaderboardRows = root.Q<VisualElement>("leaderboard-rows");

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

        private void OnLanguageChanged(ChangeEvent<string> evt)
        {
            ApplyLanguage(evt.newValue);
        }

        private void EnsureDefaultLanguageSelection()
        {
            if (languageDropdown == null)
            {
                return;
            }

            languageDropdown.choices = new List<string> { "Türkçe", "English" };
            languageDropdown.SetValueWithoutNotify("Türkçe");
            ApplyLanguage("Türkçe");
            SessionLanguageState.RecordUserChoice("tr");
            SyncLocalizationService("tr");
            OfficeFireSessionLanguage.SetRuntimeLanguageCode("tr");
        }

        private void ApplyLanguage(string dropdownLabel)
        {
            bool english = IsEnglish(dropdownLabel);

            if (loginTitleSub != null)
                loginTitleSub.text = english ? "FIRE SAFETY" : "YANGIN GÜVENLİĞİ";

            if (loginTitleMain != null)
                loginTitleMain.text = english ? "TRAINING SIMULATOR" : "EĞİTİM SİMÜLATÖRÜ";

            if (profileSectionLabel != null)
                profileSectionLabel.text = english ? "USER PROFILE" : "KULLANICI PROFİLİ";

            if (scenarioSectionLabel != null)
                scenarioSectionLabel.text = english ? "SCENARIO" : "SENARYO";

            if (languageSectionLabel != null)
                languageSectionLabel.text = english ? "LANGUAGE" : "DİL";

            if (userNameField != null)
                userNameField.label = english ? "Full Name" : "Ad Soyad";

            if (userIdField != null)
                userIdField.label = english ? "User ID" : "Kullanıcı ID";

            if (scenarioDropdown != null)
            {
                string currentValue = scenarioDropdown.value;
                OfficeFireScenarioId currentId = ScenarioIdFromDropdown(currentValue);

                scenarioDropdown.choices = english
                    ? new List<string> { "Server Room", "Archive Room", "Kitchen-Cafe" }
                    : new List<string> { "Sunucu Odası", "Arşiv Odası", "Mutfak-Kafe" };

                scenarioDropdown.SetValueWithoutNotify(ScenarioLabelForLanguage(currentId, english));
            }

            if (startButton != null)
                startButton.text = english ? "▶ START GAME" : "▶ OYUNU BAŞLAT";

            if (leaderboardTitle != null)
                leaderboardTitle.text = english ? "LEADERBOARD" : "BAŞARI TABLOSU";
        }

        private void OnStartClicked()
        {
            if (isLoading)
                return;

            bool english = languageDropdown != null && IsEnglish(languageDropdown.value);

            string userName = userNameField != null ? userNameField.value.Trim() : string.Empty;
            string userId = userIdField != null ? userIdField.value.Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(userName))
            {
                ShowError(english ? "Please enter your full name." : "Lütfen adınızı girin.");
                return;
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                ShowError(english ? "Please enter your user ID." : "Lütfen kullanıcı ID'nizi girin.");
                return;
            }

            string langCode = english ? "en" : "tr";
            OfficeFireScenarioId scenarioId = ScenarioIdFromDropdown(
                scenarioDropdown != null ? scenarioDropdown.value : string.Empty);

            SessionLanguageState.RecordUserChoice(langCode);
            SyncLocalizationService(langCode);
            OfficeFireSessionLanguage.SetRuntimeLanguageCode(langCode);
            OfficeFireLoginSession.Set(userName, userId, langCode, scenarioId);

            ClearError();
            StartCoroutine(LoadGameplayRoutine());
        }

        private IEnumerator LoadGameplayRoutine()
        {
            isLoading = true;

            if (startButton != null)
                startButton.SetEnabled(false);

            bool english = languageDropdown != null && IsEnglish(languageDropdown.value);

            const int maxAttempts = 60;
            ISceneLoaderService loader = null;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (TryResolveSceneLoader(out loader))
                    break;

                yield return null;
            }

            if (loader == null)
            {
                ShowError(english
                    ? "Scene loader not found on ServiceLocator. Ensure SceneLoader is registered (OfficeFireSceneLoaderServiceBinder or FireServiceInstaller)."
                    : "ServiceLocator üzerinde sahne yükleyici bulunamadı. SceneLoader kaydını kontrol edin (OfficeFireSceneLoaderServiceBinder veya FireServiceInstaller).");
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
                ShowError(loadTask.Exception?.GetBaseException().Message
                    ?? (english ? "Scene load failed." : "Sahne yüklenemedi."));
                isLoading = false;
                if (startButton != null)
                    startButton.SetEnabled(true);
                yield break;
            }

            OfficeFireGameplayCameraSetup.RequestEnsureReady(this, "Login→FireModule_Office");
            isLoading = false;
            if (startButton != null)
                startButton.SetEnabled(true);
        }

        private static bool TryResolveSceneLoader(out ISceneLoaderService loader)
        {
            if (ServiceLocator.TryGet(out loader) && loader != null)
                return true;

            if (ServiceLocator.TryGet(out SceneLoader concreteLoader) && concreteLoader != null)
            {
                loader = concreteLoader;
                return true;
            }

            loader = null;
            return false;
        }

        private void ShowError(string message)
        {
            if (errorLabel == null)
            {
                Debug.LogWarning($"[OfficeFireLoginScreenController] {message}", this);
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

            IReadOnlyList<string> lines = TrainingLeaderboardStore.GetDisplayLines();
            int scoreRank = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                var row = new Label(line);
                row.AddToClassList("leaderboard-row");

                if (string.Equals(line, TrainingLeaderboardStore.EmptySlotDisplay, System.StringComparison.Ordinal))
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

        private static bool IsEnglish(string dropdownLabel)
        {
            return string.Equals(dropdownLabel?.Trim(), "English", System.StringComparison.OrdinalIgnoreCase);
        }

        private static OfficeFireScenarioId ScenarioIdFromDropdown(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return OfficeFireScenarioId.ServerRoom;

            string trimmed = label.Trim();

            if (trimmed == "Arşiv Odası" || trimmed == "Archive Room")
                return OfficeFireScenarioId.ArchiveRoom;

            if (trimmed == "Mutfak-Kafe" || trimmed == "Kitchen-Cafe")
                return OfficeFireScenarioId.KitchenCafe;

            return OfficeFireScenarioId.ServerRoom;
        }

        private static string ScenarioLabelForLanguage(OfficeFireScenarioId id, bool english)
        {
            return id switch
            {
                OfficeFireScenarioId.ArchiveRoom => english ? "Archive Room" : "Arşiv Odası",
                OfficeFireScenarioId.KitchenCafe => english ? "Kitchen-Cafe" : "Mutfak-Kafe",
                _ => english ? "Server Room" : "Sunucu Odası",
            };
        }

        private static void SyncLocalizationService(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return;
            }

            if (ServiceLocator.TryGet<ILocalizationService>(out ILocalizationService service) &&
                service is LocalizationService localizationService)
            {
                localizationService.SetLanguage(languageCode);
                return;
            }

            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.SetLanguage(languageCode);
            }
        }
    }
}
