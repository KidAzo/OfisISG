using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
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
        private const string XrTestUserName = "VR Test Kullanıcı";
        private const string XrTestUserId = "vr-test-001";

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
            if (!FirePlatformRuntime.IsVR)
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
            if (!FirePlatformRuntime.IsVR)
                ApplyMenuCursor();

            if (FirePlatformRuntime.IsVR)
            {
                OfficeFireVrExtinguisherRigBootstrap.EnsureWired();
                OfficeFireInputSync.RequestDelayedSync(this, "OfficeFireModule_Login");
            }

            GetComponent<OfficeFireLoginWorldUiPresenter>()?.NotifyContentReady();
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
            ApplyXrTestProfileDefaults();
            ClearError();
            return startButton != null;
        }

        private void ApplyXrTestProfileDefaults()
        {
            if (!FirePlatformRuntime.IsVR)
                return;

            if (userNameField != null)
                userNameField.SetValueWithoutNotify(XrTestUserName);

            if (userIdField != null)
                userIdField.SetValueWithoutNotify(XrTestUserId);
        }

        private void ApplyGaussBackground()
        {
            if (loginBackground == null)
                return;

            if (FirePlatformRuntime.IsVR)
            {
                loginBackground.style.display = DisplayStyle.None;
                return;
            }

            loginBackground.style.display = DisplayStyle.Flex;

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

            if (!OfficeFireBootstrapper.HubLaunchRequested)
            {
                yield return LoadStandaloneGameplay();
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
            OfficeFireVrExtinguisherRigBootstrap.EnsureWired();
            OfficeFireInputSync.RequestDelayedSync(this, "Login→FireModule_Office");
            isLoading = false;
            if (startButton != null)
                startButton.SetEnabled(true);
        }

        struct StandaloneSceneEntry
        {
            public string SceneName;
            public string AddressableKey;
            public bool IsActive;
        }

        static List<StandaloneSceneEntry> _standaloneEntries;
        const string StandaloneLoginSceneName = "OfficeFireModule_Login";

        /// <summary>
        /// Standalone Play leaves OfficeFireModule_Login at scene index 0, which the group loader cannot unload.
        /// The active scene of the FireModule_Office group replaces login. The group's other scenes then load additively.
        /// Hub keeps <see cref="ISceneLoaderService.LoadScene"/>.
        /// </summary>
        IEnumerator LoadStandaloneGameplay()
        {
            if (!TryCopyFireGameplayGroup(out List<StandaloneSceneEntry> entries))
            {
                Debug.LogError(
                    "[FireStandalone] SceneLoader has no FireModule_Office group. Login will be replaced, but the additive set cannot be read.");
                entries = new List<StandaloneSceneEntry>
                {
                    new StandaloneSceneEntry
                    {
                        SceneName = TargetSceneGroup,
                        AddressableKey = TargetSceneGroup,
                        IsActive = true
                    }
                };
            }

            _standaloneEntries = entries;
            OfficeFireStandaloneSceneGate.BeginWaitingForAdditiveScenes();

            StandaloneSceneEntry activeEntry = FindActiveEntry(entries);
            string loadKey = string.IsNullOrEmpty(activeEntry.AddressableKey)
                ? activeEntry.SceneName
                : activeEntry.AddressableKey;
            BeginSingleBaseLoad(loadKey, activeEntry.SceneName);
            yield break;
        }

        static bool TryCopyFireGameplayGroup(out List<StandaloneSceneEntry> entries)
        {
            entries = new List<StandaloneSceneEntry>();
            if (!ServiceLocator.TryGet(out SceneLoader loader) || loader == null)
                return false;

            SceneGroup group = loader.FindSceneGroup(TargetSceneGroup);
            if (group == null || group.Scenes == null || group.Scenes.Count == 0)
                return false;

            for (int i = 0; i < group.Scenes.Count; i++)
            {
                SceneData scene = group.Scenes[i];
                if (scene == null)
                    continue;
                if (string.IsNullOrEmpty(scene.SceneName) && string.IsNullOrEmpty(scene.AddressableKey))
                    continue;

                entries.Add(new StandaloneSceneEntry
                {
                    SceneName = scene.SceneName,
                    AddressableKey = scene.AddressableKey,
                    IsActive = scene.SceneType == SceneType.ActiveScene
                });
            }

            return entries.Count > 0;
        }

        static StandaloneSceneEntry FindActiveEntry(List<StandaloneSceneEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].IsActive)
                    return entries[i];
            }

            return entries[0];
        }

        static void BeginSingleBaseLoad(string addressableKey, string sceneName)
        {
            string key = string.IsNullOrEmpty(addressableKey) ? sceneName : addressableKey;
            bool addressableKeyFound = false;
            string addressableError = null;
            AsyncOperationHandle<IList<IResourceLocation>> locations = default;
            bool locationsHandleOpen = false;
            try
            {
                locations = Addressables.LoadResourceLocationsAsync(key);
                locationsHandleOpen = true;
            }
            catch (System.Exception exception)
            {
                addressableError = exception.Message;
            }

            if (!locationsHandleOpen)
            {
                LoadBaseThroughSceneManager(sceneName, addressableError);
                return;
            }

            locations.Completed += locationResult =>
            {
                addressableKeyFound =
                    locationResult.Status == AsyncOperationStatus.Succeeded
                    && locationResult.Result != null
                    && locationResult.Result.Count > 0;
                if (!addressableKeyFound)
                {
                    addressableError = locationResult.OperationException != null
                        ? locationResult.OperationException.Message
                        : "Addressables has no location for " + key;
                }

                if (locationResult.IsValid())
                    Addressables.Release(locationResult);

                if (!addressableKeyFound)
                {
                    LoadBaseThroughSceneManager(string.IsNullOrEmpty(sceneName) ? key : sceneName, addressableError);
                    return;
                }

                AsyncOperationHandle<SceneInstance> handle;
                try
                {
                    handle = Addressables.LoadSceneAsync(key, LoadSceneMode.Single);
                }
                catch (System.Exception exception)
                {
                    LoadBaseThroughSceneManager(string.IsNullOrEmpty(sceneName) ? key : sceneName, exception.Message);
                    return;
                }

                handle.Completed += completed =>
                {
                    if (completed.Status == AsyncOperationStatus.Succeeded)
                    {
                        ContinueAdditiveGroupLoad();
                        return;
                    }

                    if (completed.IsValid())
                        Addressables.Release(completed);

                    LoadBaseThroughSceneManager(
                        string.IsNullOrEmpty(sceneName) ? key : sceneName,
                        completed.OperationException != null
                            ? completed.OperationException.Message
                            : "Addressables load failed");
                };
            };
        }

        static void LoadBaseThroughSceneManager(string sceneName, string addressableError)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                FailStandaloneBeforeGameplay(
                    (addressableError ?? "no location") + "; SceneManager could not load " + sceneName);
                return;
            }

            operation.completed += _ =>
            {
                if (SceneManager.GetSceneByName(sceneName).isLoaded)
                {
                    ContinueAdditiveGroupLoad();
                    return;
                }

                FailStandaloneBeforeGameplay(addressableError ?? "SceneManager load failed");
            };
        }

        static void FailStandaloneBeforeGameplay(string error)
        {
            OfficeFireStandaloneSceneGate.CancelWaiting();
            Debug.LogError("[FireStandalone] Base gameplay scene failed: " + error);
            OfficeFireLoginScreenController screen =
                UnityEngine.Object.FindFirstObjectByType<OfficeFireLoginScreenController>();
            if (screen != null)
                screen.RestoreAfterFailedStandaloneLoad(error);

            LogFullGameplayLoad(false);
        }

        void RestoreAfterFailedStandaloneLoad(string error)
        {
            ShowError(error);
            isLoading = false;
            if (startButton != null)
                startButton.SetEnabled(true);
        }

        static void ContinueAdditiveGroupLoad()
        {
            OfficeFireBootstrapper host = OfficeFireBootstrapper.TryGetInstance();
            if (host == null)
            {
                Debug.LogError("[FireStandalone] DontDestroyOnLoad bootstrapper is missing. Additive Fire scenes were not loaded.");
                LogFullGameplayLoad(false);
                return;
            }

            host.StartCoroutine(LoadAdditiveGroupScenes());
        }

        static IEnumerator LoadAdditiveGroupScenes()
        {
            List<StandaloneSceneEntry> entries = _standaloneEntries;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    StandaloneSceneEntry entry = entries[i];
                    if (entry.IsActive)
                        continue;

                    string sceneName = string.IsNullOrEmpty(entry.SceneName) ? entry.AddressableKey : entry.SceneName;
                    if (!string.IsNullOrEmpty(sceneName) && SceneManager.GetSceneByName(sceneName).isLoaded)
                        continue;

                    yield return LoadAdditiveEntry(entry);
                }
            }

            string activeName = ResolveActiveSceneName(entries);
            Scene active = SceneManager.GetSceneByName(activeName);
            if (active.IsValid() && active.isLoaded)
                SceneManager.SetActiveScene(active);

            bool ready = AreRequiredScenesLoaded(entries);
            LogFullGameplayLoad(ready);
            if (!ready)
                yield break;

            OfficeFireBootstrapper host = OfficeFireBootstrapper.TryGetInstance();
            OfficeFireGameplayCameraSetup.RequestEnsureReady(host, "Login→FireModule_Office");
            OfficeFireVrExtinguisherRigBootstrap.EnsureWired();
            OfficeFireInputSync.RequestDelayedSync(host, "Login→FireModule_Office");
            OfficeFireStandaloneSceneGate.MarkAdditiveScenesReady();
        }

        static IEnumerator LoadAdditiveEntry(StandaloneSceneEntry entry)
        {
            string sceneName = string.IsNullOrEmpty(entry.SceneName) ? entry.AddressableKey : entry.SceneName;
            if (!string.IsNullOrEmpty(entry.AddressableKey))
            {
                AsyncOperationHandle<SceneInstance> handle = default;
                bool started = false;
                try
                {
                    handle = Addressables.LoadSceneAsync(entry.AddressableKey, LoadSceneMode.Additive);
                    started = true;
                }
                catch (System.Exception exception)
                {
                    Debug.LogError("[FireStandalone] Additive Addressables load failed for " + entry.AddressableKey + ": " + exception.Message);
                }

                if (started)
                {
                    while (!handle.IsDone)
                        yield return null;

                    if (handle.Status == AsyncOperationStatus.Succeeded)
                        yield break;

                    Debug.LogError(
                        "[FireStandalone] Additive Addressables load failed for " + entry.AddressableKey + ": " +
                        (handle.OperationException != null ? handle.OperationException.Message : "unknown"));
                    if (handle.IsValid())
                        Addressables.Release(handle);
                }
            }

            yield return LoadAdditiveThroughSceneManager(sceneName);
        }

        static IEnumerator LoadAdditiveThroughSceneManager(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                yield break;

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                Debug.LogError("[FireStandalone] SceneManager could not additive-load " + sceneName);
                yield break;
            }

            while (!operation.isDone)
                yield return null;
        }

        static string ResolveActiveSceneName(List<StandaloneSceneEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return TargetSceneGroup;

            StandaloneSceneEntry active = FindActiveEntry(entries);
            if (!string.IsNullOrEmpty(active.SceneName))
                return active.SceneName;
            if (!string.IsNullOrEmpty(active.AddressableKey))
                return active.AddressableKey;
            return TargetSceneGroup;
        }

        static bool AreRequiredScenesLoaded(List<StandaloneSceneEntry> entries)
        {
            if (SceneManager.GetSceneByName(StandaloneLoginSceneName).isLoaded)
                return false;
            if (entries == null || entries.Count == 0)
                return false;

            for (int i = 0; i < entries.Count; i++)
            {
                string sceneName = string.IsNullOrEmpty(entries[i].SceneName)
                    ? entries[i].AddressableKey
                    : entries[i].SceneName;
                if (string.IsNullOrEmpty(sceneName) || !SceneManager.GetSceneByName(sceneName).isLoaded)
                    return false;
            }

            return true;
        }

        static void LogFullGameplayLoad(bool requiredScenesReady)
        {
            bool loginLoaded = SceneManager.GetSceneByName(StandaloneLoginSceneName).isLoaded;
            var names = new List<string>(SceneManager.sceneCount);
            for (int i = 0; i < SceneManager.sceneCount; i++)
                names.Add(SceneManager.GetSceneAt(i).name);

            Debug.Log(
                "[FireStandalone]\n" +
                "stage=full-gameplay-load-complete\n" +
                "loginLoaded=" + (loginLoaded ? "true" : "false") + "\n" +
                "activeScene=" + SceneManager.GetActiveScene().name + "\n" +
                "loadedScenes=" + string.Join(",", names) + "\n" +
                "scenario=" + OfficeFireLoginSession.SelectedScenarioId + "\n" +
                "requiredScenesReady=" + (requiredScenesReady ? "true" : "false"));
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
