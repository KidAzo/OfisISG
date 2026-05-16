using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using System.Collections.Generic;
using FireExtinguisher.Core;
using Woi.Settings;
using WOI.Modules.SDK;
using Woi.Events;
using Woi.Events.Data;

namespace Woi.UI.Navigation
{
    [RequireComponent(typeof(UIDocument))]
    public class LoginScreenController : MonoBehaviour, ILoginView
    {
        public event Action<string, string> OnLoginRequested;
        public event Action<OnLogged> OnFireModuleLoginCompleted;

        [Header("Settings")]
        [Tooltip("The name of the scene group to load (e.g. FireWarehouse)")]
        [SerializeField] private string targetSceneGroupName = "FireWarehouse";

        [Header("VR / layout")]
        [Tooltip("Hides LoginScreen.uxml element 'user-profile-section' (Full Name + User ID). Enable on the VR UIDocument instance.")]
        [SerializeField] private bool omitUserProfileSection;

        [Tooltip(
            "Shows LoginScreen.uxml full-screen dark plate + glows (pc-background-layer). Use on the PC / screen-space instance. Disable on VR / world-space so only the card is visible — do not rely on global AppMode here.")]
        [SerializeField]
        private bool useFullScreenPcBackground = true;

        [Tooltip("Optional second UIDocument using LoginLeaderboard.uxml. Assign so TOP SCORES lives on a separate GameObject (e.g. second VR quad). Leave empty if you do not show the leaderboard.")]
        [SerializeField] private UIDocument leaderboardUiDocument;

        [Header("Data Carrier")]
        [Tooltip("Directly drop the SessionData SO here so the login screen can save exactly what you clicked before transitioning.")]
        [SerializeField] private Woi.Events.Data.SessionDataSO sessionData;

        [Header("Announcements (audio-only on login)")]
        [Tooltip("Tek alan: ya doğrudan AnnouncementDefinition, ya da iki dil için Create → Woi → UI → Localized Announcement (EN + TR) oluşturup onu buraya ver — EN/TR announcement’ları o asset içindeki English/Turkish slotlarına bağlanır.")]
        [SerializeField]
        private ScriptableObject loginScreenAnnouncement;

        [SerializeField]
        private bool playLoginAnnouncementOnStart = true;

        [Header("Events")]
        [Tooltip("Optional: drag the UIDocument’s AudioTrigger here. Invokes Play() via reflection so this assembly does not reference Woi Audio (fixes IDE/OmniSharp). Remove duplicate AudioTrigger.Play from On Login Button Clicked to avoid double sound.")]
        [SerializeField] private MonoBehaviour loginClickAudioTrigger;

        [Tooltip("Invoked when the user clicks the login button (start of the login click handler).")]
        [SerializeField] private UnityEvent onLoginButtonClicked = new UnityEvent();

        private UIDocument _document;

        // UI Elements
        private DropdownField _languageDropdown;
        private Button _loginButton;
        
        // Profile Elements
        private TextField _nameInput;
        private TextField _userIdInput;
        
        // Fire Toggles
        private Toggle _toggleA;
        private Toggle _toggleB;
        private Toggle _toggleC;
        private Toggle _toggleD;
        private Toggle _toggleF;
        private Toggle _toggleElectrical;

        // All / None buttons
        private Button _buttonAll;
        private Button _buttonNone;

        // Localization Labels
        private Label _lblTitleSub;
        private Label _lblTitleMain;
        private Label _lblUserProfile;
        private Label _lblLanguage;
        private Label _lblSelectFireTypes;

        /// <summary>UXML/USS: <see cref="LoginScreen.uss"/> — yalnızca altı yangın türü de seçiliyken eklenir.</summary>
        private const string AllButtonAllTypesSelectedUssClass = "select-btn--all-active";

        /// <summary>LoginLeaderboard.uss: PC top-left panel; VR documents omit this class.</summary>
        private const string LeaderboardDocRootPcClass = "leaderboard-doc-root--pc";

        // Aggregated toggle list for bulk operations
        private List<Toggle> _allToggles;

        // Guard flag – prevents toggle callbacks from firing during bulk updates
        private bool _isUpdatingUI;

        private void Start()
        {
            ValidateBootstrapServices();
            TryPlayLoginAnnouncement();
        }

        public void SetVisible(bool visible)
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();

            if (_document == null)
                return;

            VisualElement root = _document.rootVisualElement;
            if (root != null)
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetLoading(bool loading)
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();

            VisualElement root = _document?.rootVisualElement;
            if (root == null)
                return;

            VisualElement overlay = root.Q<VisualElement>("login-loading-overlay");
            if (overlay == null)
                return;

            overlay.style.display = loading ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ShowError(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (_document == null)
                _document = GetComponent<UIDocument>();

            VisualElement root = _document?.rootVisualElement;
            if (root == null)
            {
                Debug.LogWarning($"[LoginScreenController] ShowError (no UI root): {message}", this);
                return;
            }

            Label errorLabel = root.Q<Label>("login-error-message");
            if (errorLabel == null)
            {
                Debug.LogWarning($"[LoginScreenController] {message}", this);
                return;
            }

            errorLabel.text = message;
            errorLabel.style.display = DisplayStyle.Flex;
        }

        public void ClearError()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();

            VisualElement root = _document?.rootVisualElement;
            if (root == null)
                return;

            Label errorLabel = root.Q<Label>("login-error-message");
            if (errorLabel == null)
                return;

            errorLabel.text = string.Empty;
            errorLabel.style.display = DisplayStyle.None;
        }

        private void ValidateBootstrapServices()
        {
            if (!ServiceLocator.TryGet<ISceneLoaderService>(out _))
                Debug.LogError("[LoginScreenController] ISceneLoaderService is not registered. Ensure FireServiceInstaller ran in the bootstrap scene.");
        }

        private const string AnnouncementsAssemblyShortName = "Woi.UI.Announcements";
        private const string PopupsAssemblyShortName = "Woi.UI.Popups";

        /// <summary>Must match <c>LocalizationService.English</c> / popup language codes.</summary>
        private const string LangEnglish = "en";
        /// <summary>Must match <c>LocalizationService.Turkish</c>.</summary>
        private const string LangTurkish = "tr";

        private static readonly Dictionary<Type, MethodInfo> CachedParameterlessPlayByComponentType = new Dictionary<Type, MethodInfo>();
        private static readonly HashSet<Type> WarnedMissingPlayOnType = new HashSet<Type>();

        private static Type _cachedAnnouncementServiceInterface;
        private static Type _cachedAnnouncementDefinitionType;
        private static Type _cachedLocalizationServiceInterfaceType;
        private static MethodInfo _cachedServiceLocatorTryGet;

        /// <summary>
        /// Plays the login announcement in <see cref="Start"/> only (after ServiceLocator registration).
        /// Uses reflection so this assembly does not reference <c>Woi.UI.Announcements</c> (IDE/OmniSharp often lack Unity-generated .csproj when .sln is gitignored).
        /// </summary>
        private void TryPlayLoginAnnouncement()
        {
            if (!playLoginAnnouncementOnStart || loginScreenAnnouncement == null)
                return;

            var iface = GetAnnouncementServiceInterfaceType();
            var definitionType = GetAnnouncementDefinitionType();
            if (iface == null || definitionType == null)
            {
                Debug.LogWarning(
                    "[LoginScreenController] Could not resolve Woi.UI.Announcements types — login audio announcement skipped.");
                return;
            }

            var tryGet = GetServiceLocatorTryGet(iface);
            if (tryGet == null)
                return;

            var args = new object[] { null };
            if (!(bool)tryGet.Invoke(null, args) || args[0] == null)
            {
                Debug.LogWarning(
                    "[LoginScreenController] IAnnouncementService not registered — login audio announcement skipped. " +
                    "Add AnnouncementService + WoiAnnouncementAudioAdapter + AudioSystem; use UiMessagingServiceInstaller or service registration on components.");
                return;
            }

            var play = iface.GetMethod(
                "Play",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { definitionType },
                null);

            if (play == null)
            {
                Debug.LogWarning("[LoginScreenController] IAnnouncementService.Play not found — login audio announcement skipped.");
                return;
            }

            object payload = loginScreenAnnouncement;
            var localizedPairType = FindTypeInAssembly(AnnouncementsAssemblyShortName, "Woi.UI.Announcements.LocalizedAnnouncementDefinition");
            if (localizedPairType != null && localizedPairType.IsInstanceOfType(loginScreenAnnouncement))
            {
                MethodInfo resolve = localizedPairType.GetMethod(
                    "ResolveForCurrentLanguage",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);

                if (resolve != null)
                {
                    object resolved = resolve.Invoke(loginScreenAnnouncement, null);
                    if (resolved != null)
                        payload = resolved;
                }
            }

            play.Invoke(args[0], new object[] { payload });
        }

        private static Type GetAnnouncementServiceInterfaceType() =>
            _cachedAnnouncementServiceInterface ??=
                FindTypeInAssembly(AnnouncementsAssemblyShortName, "Woi.UI.Announcements.IAnnouncementService");

        private static Type GetAnnouncementDefinitionType() =>
            _cachedAnnouncementDefinitionType ??=
                FindTypeInAssembly(AnnouncementsAssemblyShortName, "Woi.UI.Announcements.AnnouncementDefinition");

        private static MethodInfo GetServiceLocatorTryGet(Type iface)
        {
            if (_cachedServiceLocatorTryGet != null)
                return _cachedServiceLocatorTryGet.MakeGenericMethod(iface);

            foreach (var m in typeof(ServiceLocator).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name == "TryGet" && m.IsGenericMethodDefinition)
                {
                    _cachedServiceLocatorTryGet = m;
                    return m.MakeGenericMethod(iface);
                }
            }

            Debug.LogError("[LoginScreenController] ServiceLocator.TryGet<> not found.");
            return null;
        }

        private static Type FindTypeInAssembly(string assemblyShortName, string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != assemblyShortName)
                    continue;

                var t = asm.GetType(fullName);
                if (t != null)
                    return t;
            }

            return null;
        }

        private static Type GetLocalizationServiceInterfaceType() =>
            _cachedLocalizationServiceInterfaceType ??=
                FindTypeInAssembly(PopupsAssemblyShortName, "Woi.UI.Popups.Localization.ILocalizationService");

        private static object TryGetLocalizationServiceStaticInstance()
        {
            Type t = FindTypeInAssembly(PopupsAssemblyShortName, "Woi.UI.Popups.Localization.LocalizationService");
            if (t == null)
                return null;

            PropertyInfo p = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            return p?.GetValue(null);
        }

        private static bool TryGetLocalizationFromServiceLocator(out object service)
        {
            service = null;
            Type iface = GetLocalizationServiceInterfaceType();
            if (iface == null)
                return false;

            MethodInfo tryGet = GetServiceLocatorTryGet(iface);
            if (tryGet == null)
                return false;

            var args = new object[] { null };
            if (!(bool)tryGet.Invoke(null, args) || args[0] == null)
                return false;

            service = args[0];
            return true;
        }

        private static bool TryGetLocalization(out object loc)
        {
            if (TryGetLocalizationFromServiceLocator(out loc) && loc != null)
                return true;

            loc = TryGetLocalizationServiceStaticInstance();
            return loc != null;
        }

        private static string GetLocalizationCurrentLanguage(object loc)
        {
            if (loc == null)
                return null;

            PropertyInfo p = loc.GetType().GetProperty("CurrentLanguage", BindingFlags.Public | BindingFlags.Instance);
            return p?.GetValue(loc) as string;
        }

        private static void InvokeLocalizationSetLanguage(object loc, string code)
        {
            if (loc == null)
                return;

            MethodInfo m = loc.GetType().GetMethod(
                "SetLanguage",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string) },
                null);

            m?.Invoke(loc, new object[] { code });
        }

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;

            if (root == null) return;

            StretchLoginShellToPanel(root);
            ApplyPcBackgroundVisibility(root);

            if (omitUserProfileSection)
            {
                VisualElement profile = root.Q<VisualElement>("user-profile-section");
                if (profile != null)
                    profile.style.display = DisplayStyle.None;
            }

            // Bind UI
            _languageDropdown = root.Q<DropdownField>("language-dropdown");
            _loginButton = root.Q<Button>("btn-start");

            _nameInput = root.Q<TextField>("input-name");
            _userIdInput = root.Q<TextField>("input-userid");
            _toggleA = root.Q<Toggle>("toggle-class-a");
            _toggleB = root.Q<Toggle>("toggle-class-b");
            _toggleC = root.Q<Toggle>("toggle-class-c");
            _toggleD = root.Q<Toggle>("toggle-class-d");
            _toggleF = root.Q<Toggle>("toggle-class-f");
            _toggleElectrical = root.Q<Toggle>("toggle-electrical");

            _buttonAll  = root.Q<Button>("btn-select-all");
            _buttonNone = root.Q<Button>("btn-select-none");

            _lblTitleSub = root.Q<Label>(className: "title-sub");
            _lblTitleMain = root.Q<Label>(className: "title-main");

            var sections = root.Query<VisualElement>(className: "section-container").ToList();
            if (sections.Count >= 3)
            {
                _lblUserProfile = sections[0].Q<Label>(className: "section-label");
                _lblLanguage = sections[1].Q<Label>(className: "section-label");
                _lblSelectFireTypes = sections[2].Q<Label>(className: "section-label");
            }

            // If UXML buttons weren't found at runtime, build them in code
            if (_buttonAll == null || _buttonNone == null)
                CreateSelectButtons(root);

            // Build the aggregated toggle list
            _allToggles = new List<Toggle>
            {
                _toggleA,
                _toggleB,
                _toggleC,
                _toggleD,
                _toggleF,
                _toggleElectrical
            };

            // Register individual toggle callbacks (guarded)
            foreach (var toggle in _allToggles)
                toggle?.RegisterValueChangedCallback(_ => { if (!_isUpdatingUI) OnSelectionChanged(); });

            // Event Subscriptions
            if (_loginButton != null)
                _loginButton.clicked += OnLoginClicked;

            if (_buttonAll != null)
                _buttonAll.clicked += SelectAll;

            if (_buttonNone != null)
                _buttonNone.clicked += SelectNone;

            if (_languageDropdown != null)
            {
                _languageDropdown.RegisterValueChangedCallback(OnLanguageChanged);
                SyncDropdownFromLocalizationService();
                ApplyLanguageFromDropdown(_languageDropdown.value);
            }

            RefreshAllButtonSelectionVisual();
            RefreshLeaderboardForRoot(root);
            if (leaderboardUiDocument != null)
                RefreshLeaderboardForRoot(leaderboardUiDocument.rootVisualElement);
            ApplyLeaderboardDocumentLayoutMode();
        }

        private void OnDisable()
        {
            ClearLeaderboardDocumentLayoutMode();

            if (_loginButton != null)
                _loginButton.clicked -= OnLoginClicked;

            if (_buttonAll != null)
                _buttonAll.clicked -= SelectAll;

            if (_buttonNone != null)
                _buttonNone.clicked -= SelectNone;

            if (_languageDropdown != null)
                _languageDropdown.UnregisterValueChangedCallback(OnLanguageChanged);
        }

        private void OnLanguageChanged(ChangeEvent<string> evt)
        {
            ApplyLanguageFromDropdown(evt.newValue);
        }

        /// <summary>
        /// Fills the runtime panel so transparent regions do not show the scene camera (Login.unity had a vivid solid camera clear).
        /// </summary>
        static void StretchLoginShellToPanel(VisualElement root)
        {
            if (root == null)
                return;

            static void Stretch(VisualElement el)
            {
                if (el == null)
                    return;
                el.style.flexGrow = 1f;
                el.style.flexShrink = 0;
                el.style.width = Length.Percent(100);
                el.style.height = Length.Percent(100);
            }

            Stretch(root);
            if (!string.Equals(root.name, "login-shell", StringComparison.Ordinal))
            {
                VisualElement shell = root.Q<VisualElement>("login-shell");
                Stretch(shell);
            }
        }

        /// <summary>Full-screen plate + glows when <see cref="useFullScreenPcBackground"/> is enabled on this instance (PC branch), hidden on VR card-only instances.</summary>
        void ApplyPcBackgroundVisibility(VisualElement root)
        {
            if (root == null)
                return;

            var pcBg = root.Q<VisualElement>("pc-background-layer");
            if (pcBg == null)
                return;

            if (useFullScreenPcBackground)
            {
                pcBg.style.display = DisplayStyle.Flex;
                // Matches LoginScreen.uss --bg-dark if USS order ever misses this node.
                pcBg.style.backgroundColor = new Color(7f / 255f, 9f / 255f, 19f / 255f, 1f);
            }
            else
            {
                pcBg.style.display = DisplayStyle.None;
            }
        }

        void ApplyLeaderboardDocumentLayoutMode()
        {
            if (leaderboardUiDocument == null)
                return;

            VisualElement treeRoot = leaderboardUiDocument.rootVisualElement;
            if (treeRoot == null)
                return;

            VisualElement docRoot = treeRoot.Q<VisualElement>("leaderboard-doc-root") ?? treeRoot;

            if (useFullScreenPcBackground)
                docRoot.AddToClassList(LeaderboardDocRootPcClass);
            else
                docRoot.RemoveFromClassList(LeaderboardDocRootPcClass);
        }

        void ClearLeaderboardDocumentLayoutMode()
        {
            if (leaderboardUiDocument == null)
                return;

            VisualElement treeRoot = leaderboardUiDocument.rootVisualElement;
            if (treeRoot == null)
                return;

            VisualElement docRoot = treeRoot.Q<VisualElement>("leaderboard-doc-root") ?? treeRoot;
            docRoot.RemoveFromClassList(LeaderboardDocRootPcClass);
        }

        /// <summary>Maps login dropdown labels (LoginScreen.uxml choices) to ISO codes (en/tr) used by Woi localization.</summary>
        private static string LanguageCodeFromDropdownLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return LangTurkish;

            if (string.Equals(label.Trim(), "English", StringComparison.OrdinalIgnoreCase))
                return LangEnglish;

            return LangTurkish;
        }

        private void SyncDropdownFromLocalizationService()
        {
            if (_languageDropdown == null || !TryGetLocalization(out var loc))
                return;

            string code = GetLocalizationCurrentLanguage(loc)?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(code)) code = LangTurkish;
            string label = code == LangEnglish ? "English" : "Türkçe";

            foreach (var c in _languageDropdown.choices)
            {
                if (c == label)
                {
                    _languageDropdown.SetValueWithoutNotify(label);
                    return;
                }
            }
        }

        private void ApplyLanguageFromDropdown(string dropdownLabel)
        {
            string code = LanguageCodeFromDropdownLabel(dropdownLabel);

            if (TryGetLocalization(out var loc))
                InvokeLocalizationSetLanguage(loc, code);
            else
                Debug.LogWarning(
                    "[LoginScreenController] LocalizationService / ILocalizationService not found — language not applied. Add LocalizationService (DDOL recommended).");

            Debug.Log($"[LoginScreen] Language set to '{code}' ({dropdownLabel}).");

            UpdateUILocaleTexts(code);
        }

        private void UpdateUILocaleTexts(string langCode)
        {
            bool isTr = langCode == LangTurkish;

            if (_lblTitleSub != null) _lblTitleSub.text = isTr ? "YANGIN GÜVENLİĞİ" : "FIRE SAFETY";
            if (_lblTitleMain != null) _lblTitleMain.text = isTr ? "EĞİTİM SİMÜLATÖRÜ" : "TRAINING SIMULATOR";
            
            if (_lblUserProfile != null) _lblUserProfile.text = isTr ? "KULLANICI PROFİLİ" : "USER PROFILE";
            if (_nameInput != null) _nameInput.label = isTr ? "Ad Soyad" : "Full Name";
            if (_userIdInput != null) _userIdInput.label = isTr ? "Kullanıcı ID" : "User ID";

            if (_lblLanguage != null) _lblLanguage.text = isTr ? "DİL" : "LANGUAGE";

            if (_lblSelectFireTypes != null) _lblSelectFireTypes.text = isTr ? "YANGIN TÜRLERİNİ SEÇ" : "SELECT FIRE TYPES";
            
            if (_toggleA != null) _toggleA.label = isTr ? "A-Ahşap" : "A-Woods";
            if (_toggleB != null) _toggleB.label = isTr ? "B-Sıvı" : "B-Oils";
            if (_toggleC != null) _toggleC.label = isTr ? "C-Gaz" : "C-Gases";
            if (_toggleD != null) _toggleD.label = isTr ? "D-Metal" : "D-Metals";
            if (_toggleF != null) _toggleF.label = isTr ? "F-Kızartma Yağı" : "F-Cooking Oil";
            if (_toggleElectrical != null) _toggleElectrical.label = isTr ? "Elektrik" : "Electrical";

            if (_buttonAll != null) _buttonAll.text = isTr ? "\u2714 TÜMÜ" : "\u2714 ALL";
            if (_buttonNone != null) _buttonNone.text = isTr ? "\u2716 HİÇBİRİ" : "\u2716 NONE";

            if (_loginButton != null) _loginButton.text = isTr ? "▶ OTURUMU BAŞLAT" : "▶ START SESSION";
        }

        // ── Bulk selection helpers ────────────────────────────────────────────

        private void SelectAll()
        {
            _isUpdatingUI = true;

            foreach (var toggle in _allToggles)
                if (toggle != null) toggle.value = true;

            _isUpdatingUI = false;

            OnSelectionChanged();
        }

        private void SelectNone()
        {
            _isUpdatingUI = true;

            foreach (var toggle in _allToggles)
                if (toggle != null) toggle.value = false;

            _isUpdatingUI = false;

            OnSelectionChanged();
        }

        // ── Shared selection-change handler ───────────────────────────────────

        /// <summary>
        /// Called whenever any fire-type toggle changes value (individually or via SelectAll/SelectNone).
        /// Persists the current selection to <see cref="sessionData"/> if one is assigned.
        /// </summary>
        private void OnSelectionChanged()
        {
            if (sessionData != null)
            {
                var selectedClasses = BuildSelectedClasses();
                sessionData.SelectedClasses = selectedClasses;
                sessionData.NotifyUpdated();

                Debug.Log($"[LoginScreenController] OnSelectionChanged – {selectedClasses.Count} fire class(es) selected.");
            }

            RefreshAllButtonSelectionVisual();
        }

        // ── Login flow ────────────────────────────────────────────────────────

        /// <summary>
        /// Calls parameterless <c>Play()</c> on the assigned component (e.g. Woi AudioTrigger) without referencing WoiUtils.AudioSystem.
        /// </summary>
        private void TryInvokeLoginClickAudioPlay()
        {
            if (loginClickAudioTrigger == null)
                return;

            Type t = loginClickAudioTrigger.GetType();
            if (!CachedParameterlessPlayByComponentType.TryGetValue(t, out MethodInfo play))
            {
                play = t.GetMethod(
                    "Play",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
                if (play != null)
                    CachedParameterlessPlayByComponentType[t] = play;
            }

            if (play == null)
            {
                if (WarnedMissingPlayOnType.Add(t))
                    Debug.LogWarning($"[LoginScreenController] loginClickAudioTrigger ({t.FullName}) has no public Play(). Assign AudioTrigger or remove field.", loginClickAudioTrigger);
                return;
            }

            play.Invoke(loginClickAudioTrigger, null);
        }

        private void OnLoginClicked()
        {
            TryInvokeLoginClickAudioPlay();

            onLoginButtonClicked?.Invoke();

            // 1. Build the List based on user selection
            List<FireClass> selectedClasses = BuildSelectedClasses();

            // 2. Collect profile data
            string userName = _nameInput != null ? _nameInput.value : "";
            string userId = _userIdInput != null ? _userIdInput.value : "";

            // 3. Save Data Globally Immediately
            if (sessionData != null)
            {
                sessionData.SelectedClasses = selectedClasses;
                sessionData.UserName = userName;
                sessionData.UserId = userId;
                sessionData.NotifyUpdated();
                Debug.Log($"[LoginScreenController] Directly wrote {selectedClasses.Count} fire classes to SessionDataSO!");
            }
            else
            {
                Debug.LogError("[LoginScreenController] FATAL: You forgot to assign SessionData to the LoginScreenController in the Inspector!");
            }

            var logged = new OnLogged
            {
                SelectedClasses = selectedClasses,
                UserName = userName,
                UserId = userId,
                TargetScene = targetSceneGroupName
            };

            OnFireModuleLoginCompleted?.Invoke(logged);
            EventBus.Raise(logged);

            Debug.Log($"[LoginScreenController] Profile: {userName} (ID: {userId}) - Published OnLogged for {selectedClasses.Count} fire classes. Target scene: {targetSceneGroupName}.");
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>Reads the current state of each fire toggle and returns the matching <see cref="FireClass"/> list.</summary>
        private List<FireClass> BuildSelectedClasses()
        {
            var list = new List<FireClass>();
            if (_toggleA         != null && _toggleA.value)         list.Add(FireClass.A);
            if (_toggleB         != null && _toggleB.value)         list.Add(FireClass.B);
            if (_toggleC         != null && _toggleC.value)         list.Add(FireClass.C);
            if (_toggleD         != null && _toggleD.value)         list.Add(FireClass.D);
            if (_toggleF         != null && _toggleF.value)         list.Add(FireClass.F);
            if (_toggleElectrical != null && _toggleElectrical.value) list.Add(FireClass.E);
            return list;
        }

        /// <summary>ALL butonu: yalnızca A,B,C,D,F,Electrical toggles’ının hepsi seçiliyken vurgu sınıfı.</summary>
        private void RefreshAllButtonSelectionVisual()
        {
            if (_buttonAll == null || _allToggles == null)
                return;

            int required = 0;
            foreach (Toggle t in _allToggles)
            {
                if (t == null)
                    continue;

                required++;
                if (!t.value)
                {
                    _buttonAll.EnableInClassList(AllButtonAllTypesSelectedUssClass, false);
                    return;
                }
            }

            bool allOn = required > 0;
            _buttonAll.EnableInClassList(AllButtonAllTypesSelectedUssClass, allOn);
        }

        /// <summary>Fills <c>leaderboard-rows</c> from <see cref="TrainingLeaderboardStore"/>. Use the root of a document built from <c>LoginLeaderboard.uxml</c> (optional second <see cref="UIDocument"/> referenced from the login screen).</summary>
        public static void RefreshLeaderboardForRoot(VisualElement root)
        {
            if (root == null)
                return;

            VisualElement host = root.Q<VisualElement>("leaderboard-rows");
            if (host == null)
                return;

            host.Clear();
            IReadOnlyList<string> lines = TrainingLeaderboardStore.GetDisplayLines();
            int scoreRank = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                var row = new Label(line);
                row.AddToClassList("leaderboard-row");
                if (string.Equals(line, TrainingLeaderboardStore.EmptySlotDisplay, StringComparison.Ordinal))
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

                host.Add(row);
            }
        }

        /// <summary>
        /// Programmatically creates the All / None buttons and inserts them into the visual tree
        /// directly after the checkbox-grid. Called only when the UXML elements are not found at runtime.
        /// </summary>
        private void CreateSelectButtons(VisualElement root)
        {
            // Find the checkbox grid – our anchor point
            var checkboxGrid = root.Q<VisualElement>(className: "checkbox-grid");
            var container    = checkboxGrid?.parent;   // this is the section-container

            if (container == null)
            {
                Debug.LogWarning("[LoginScreenController] Could not find .checkbox-grid parent. Appending select row to root.");
                container = root;
            }

            // ── Row wrapper ──────────────────────────────────────────────────
            var row = new VisualElement { name = "select-row-runtime" };
            row.AddToClassList("select-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop     = 6;
            row.style.marginBottom  = 4;
            row.style.minHeight     = 38;

            // ── ALL button ───────────────────────────────────────────────────
            _buttonAll = new Button { text = "\u2714 ALL", name = "btn-select-all" };
            _buttonAll.AddToClassList("select-btn");
            _buttonAll.AddToClassList("select-btn--all");
            _buttonAll.style.flexGrow   = 1;
            _buttonAll.style.flexShrink = 1;
            _buttonAll.style.flexBasis  = StyleKeyword.Auto;
            _buttonAll.style.marginRight = 5;
            _buttonAll.style.minHeight   = 38;
            _buttonAll.style.borderTopLeftRadius     = 8;
            _buttonAll.style.borderTopRightRadius    = 8;
            _buttonAll.style.borderBottomLeftRadius  = 8;
            _buttonAll.style.borderBottomRightRadius = 8;
            _buttonAll.style.borderTopWidth    = 1;
            _buttonAll.style.borderBottomWidth = 1;
            _buttonAll.style.borderLeftWidth   = 1;
            _buttonAll.style.borderRightWidth  = 1;
            _buttonAll.style.fontSize          = 11;
            _buttonAll.style.unityFontStyleAndWeight = FontStyle.Bold;

            // ── NONE button ──────────────────────────────────────────────────
            _buttonNone = new Button { text = "\u2716 NONE", name = "btn-select-none" };
            _buttonNone.AddToClassList("select-btn");
            _buttonNone.AddToClassList("select-btn--none");
            _buttonNone.style.flexGrow   = 1;
            _buttonNone.style.flexShrink = 1;
            _buttonNone.style.flexBasis  = StyleKeyword.Auto;
            _buttonNone.style.marginLeft = 5;
            _buttonNone.style.minHeight  = 38;
            _buttonNone.style.borderTopLeftRadius     = 8;
            _buttonNone.style.borderTopRightRadius    = 8;
            _buttonNone.style.borderBottomLeftRadius  = 8;
            _buttonNone.style.borderBottomRightRadius = 8;
            _buttonNone.style.borderTopWidth    = 1;
            _buttonNone.style.borderBottomWidth = 1;
            _buttonNone.style.borderLeftWidth   = 1;
            _buttonNone.style.borderRightWidth  = 1;
            _buttonNone.style.borderTopColor    = new Color(0.48f, 0.52f, 0.6f, 0.3f);
            _buttonNone.style.borderBottomColor = new Color(0.48f, 0.52f, 0.6f, 0.3f);
            _buttonNone.style.borderLeftColor   = new Color(0.48f, 0.52f, 0.6f, 0.3f);
            _buttonNone.style.borderRightColor  = new Color(0.48f, 0.52f, 0.6f, 0.3f);
            _buttonNone.style.backgroundColor   = new Color(0.48f, 0.52f, 0.6f, 0.08f);
            _buttonNone.style.color             = new Color(0.48f, 0.52f, 0.6f, 1f);
            _buttonNone.style.fontSize          = 11;
            _buttonNone.style.unityFontStyleAndWeight = FontStyle.Bold;

            row.Add(_buttonAll);
            row.Add(_buttonNone);

            // Insert right after the checkbox-grid (index + 1), or append
            if (checkboxGrid != null)
                container.Insert(container.IndexOf(checkboxGrid) + 1, row);
            else
                container.Add(row);

            Debug.Log("[LoginScreenController] All/None buttons created programmatically (UXML fallback).");
        }
    }
}
