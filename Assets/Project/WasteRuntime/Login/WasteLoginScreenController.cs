using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Woi.DataHandler;
using Woi.Events.Data;
using Woi.OfficeFire;
using Woi.OfficeFire.VrBridge;
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
        private static readonly Color LoginIconTint = new(0f, 1f, 0.698f, 1f);

        // Temporary standalone VR test entry: no controller interaction on the login panel.
        private const string VrAutoStartParticipantName = "VR Tester";
        private const string VrAutoStartParticipantId = "VR-TEST";
        private const string VrAutoStartLanguageLabel = "English";
        private const float VrAutoStartLoaderTimeoutSeconds = 10f;
        private static bool vrAutoStartConsumed;

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

            if (!vrAutoStartConsumed && IsStandaloneVrAutoStartLaunch())
                StartCoroutine(VrAutoStartRoutine());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetVrAutoStart()
        {
            vrAutoStartConsumed = false;
        }

        private static bool IsStandaloneVrAutoStartLaunch()
        {
            if (WasteHubLaunch.IsHubLaunch || StandalonePlatform.HasHubOverride)
                return false;

            if (WasteBridgeRun.IsActive || VrBridgeRuntime.IsReady)
                return false;

            return WasteCollectionPlatform.ShouldUseVrPresentation();
        }

        private IEnumerator VrAutoStartRoutine()
        {
            vrAutoStartConsumed = true;
            Debug.Log("[WasteVrAutoStart] begin", this);

            yield return null;

            float deadline = Time.realtimeSinceStartup + VrAutoStartLoaderTimeoutSeconds;
            while (!TryResolveSceneLoader(out _) && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (isLoading || !IsStandaloneVrAutoStartLaunch())
            {
                Debug.Log("[WasteVrAutoStart] skipped (loading already started or launch is no longer standalone VR)", this);
                yield break;
            }

            SessionProfileLanguagePreference.RecordUserChoice(WasteCollectionLocalization.LangEnglish);
            if (languageDropdown != null)
                languageDropdown.value = VrAutoStartLanguageLabel;
            Debug.Log("[WasteVrAutoStart] language=English", this);

            if (userNameField != null)
                userNameField.value = VrAutoStartParticipantName;
            if (userIdField != null)
                userIdField.value = VrAutoStartParticipantId;
            Debug.Log(
                "[WasteVrAutoStart] participantName=" + VrAutoStartParticipantName +
                " participantId=" + VrAutoStartParticipantId,
                this);

            Debug.Log("[WasteVrAutoStart] invoking existing start flow", this);
            OnStartClicked();

            if (isLoading)
                Debug.Log("[WasteVrAutoStart] gameplay-load-requested", this);
            else
                Debug.LogWarning("[WasteVrAutoStart] start flow did not begin loading; see login error label.", this);
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

            if (!WasteHubLaunch.IsHubLaunch)
            {
                yield return LoadStandaloneOffice();
                yield break;
            }

            Task loadTask;
            try
            {
                loadTask = loader.LoadScene(WasteModuleBootstrap.OfficeSceneGroup);
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

        /// <summary>
        /// Standalone Play leaves WasteLogin at scene index 0, which the group loader cannot unload.
        /// Load Waste_Office in Single mode. Hub keeps <see cref="ISceneLoaderService.LoadScene"/>.
        /// </summary>
        IEnumerator LoadStandaloneOffice()
        {
            const string key = WasteModuleBootstrap.OfficeSceneGroup;
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

            if (locationsHandleOpen)
            {
                while (!locations.IsDone)
                    yield return null;

                addressableKeyFound =
                    locations.Status == AsyncOperationStatus.Succeeded
                    && locations.Result != null
                    && locations.Result.Count > 0;
                if (!addressableKeyFound)
                {
                    addressableError = locations.OperationException != null
                        ? locations.OperationException.Message
                        : "Addressables has no location for " + key;
                }

                if (locations.IsValid())
                    Addressables.Release(locations);
            }

            if (addressableKeyFound)
            {
                AsyncOperationHandle<SceneInstance> handle;
                try
                {
                    handle = Addressables.LoadSceneAsync(key, LoadSceneMode.Single);
                }
                catch (System.Exception exception)
                {
                    FailStandaloneOffice(exception.Message);
                    yield break;
                }

                while (!handle.IsDone)
                    yield return null;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                    yield break;

                if (handle.IsValid())
                    Addressables.Release(handle);

                addressableError = handle.OperationException != null
                    ? handle.OperationException.Message
                    : "Addressables load failed";
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(key, LoadSceneMode.Single);
            if (operation == null)
            {
                FailStandaloneOffice(
                    (addressableError ?? "no location") + "; SceneManager could not load " + key);
                yield break;
            }

            while (!operation.isDone)
                yield return null;

            if (!SceneManager.GetSceneByName(key).isLoaded)
                FailStandaloneOffice(addressableError ?? "SceneManager load failed");
        }

        void FailStandaloneOffice(string error)
        {
            ShowError(error);
            isLoading = false;
            if (startButton != null)
                startButton.SetEnabled(true);
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
