using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Woi.UI
{
    public class LoginScreenController : MonoBehaviour
    {
        [SerializeField] private UIDocument ui;

        // UI refs
        private Label nameLabel, userIdLabel, langLabel;
        private Label namePlaceholder;
        private TextField nameField, userIdField;
        private Button trBtn, enBtn, startBtn;

        // prefs keys
        private const string NameKey = "login_name";
        private const string UserIdKey = "login_userid";
        private const string LangKey = "login_lang";

        private string selectedLang = "TR";

        // Simple localization table
        private readonly Dictionary<string, Dictionary<string, string>> T = new()
        {
            ["TR"] = new()
            {
                ["NameLabel"] = "OYUNCU ADI",
                ["NamePlaceholder"] = "Oyuncu Adını Gir",
                ["UserIdLabel"] = "KULLANICI KİMLİĞİ (ID)",
                ["LangLabel"] = "DİL SEÇİMİ",
                ["StartButton"] = "OYUNA BAŞLA",
            },
            ["EN"] = new()
            {
                ["NameLabel"] = "PLAYER NAME",
                ["NamePlaceholder"] = "Enter player name",
                ["UserIdLabel"] = "USER ID",
                ["LangLabel"] = "LANGUAGE",
                ["StartButton"] = "START GAME",
            }
        };

        private void Reset()
        {
            ui = GetComponent<UIDocument>();
        }

        private void Awake()
        {
            if (ui == null)
                ui = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (ui == null)
            {
                Debug.LogError("UIDocument reference is missing on LoginScreenController.");
                enabled = false;
                return;
            }

            var root = ui.rootVisualElement;

            // Query
            nameLabel = root.Q<Label>("NameLabel");
            userIdLabel = root.Q<Label>("UserIdLabel");
            langLabel = root.Q<Label>("LangLabel");

            nameField = root.Q<TextField>("NameField");
            userIdField = root.Q<TextField>("UserIdField");

            trBtn = root.Q<Button>("TRButton");
            enBtn = root.Q<Button>("ENButton");
            startBtn = root.Q<Button>("StartButton");

            namePlaceholder = root.Q<Label>("NamePlaceholder");

            // Safety checks (optional but useful)
            if (nameField == null || startBtn == null || trBtn == null || enBtn == null)
            {
                Debug.LogError("Login UXML element names mismatch. Check NameField / StartButton / TRButton / ENButton.");
                enabled = false;
                return;
            }

            // Events
            trBtn.clicked += OnTRClicked;
            enBtn.clicked += OnENClicked;
            startBtn.clicked += OnStartClicked;

            nameField.RegisterValueChangedCallback(OnNameChanged);

            // Placeholder bug fix: focus handling
            nameField.RegisterCallback<FocusInEvent>(OnNameFocusIn);
            nameField.RegisterCallback<FocusOutEvent>(OnNameFocusOut);

            // Apply initial state
            SetLanguage(selectedLang);

            var nameWrapper = root.Q<VisualElement>("NameInputWrapper");
            if (nameWrapper != null)
            {
                nameWrapper.RegisterCallback<PointerDownEvent>(_ =>
                {
                    nameField.Focus();
                });
}

            RefreshUIState();
        }

        private void OnDisable()
        {
            if (ui == null || ui.rootVisualElement == null) return;

            // Important: avoid double subscription if object is re-enabled
            if (trBtn != null) trBtn.clicked -= OnTRClicked;
            if (enBtn != null) enBtn.clicked -= OnENClicked;
            if (startBtn != null) startBtn.clicked -= OnStartClicked;

            if (nameField != null)
            {
                nameField.UnregisterValueChangedCallback(OnNameChanged);
                nameField.UnregisterCallback<FocusInEvent>(OnNameFocusIn);
                nameField.UnregisterCallback<FocusOutEvent>(OnNameFocusOut);
            }
        }

        private void OnTRClicked() => SetLanguage("TR");
        private void OnENClicked() => SetLanguage("EN");

        private void SetLanguage(string lang)
        {
            selectedLang = lang;

            // save
            PlayerPrefs.SetString(LangKey, selectedLang);
            PlayerPrefs.Save();

            // selected class
            if (trBtn != null) trBtn.RemoveFromClassList("selected");
            if (enBtn != null) enBtn.RemoveFromClassList("selected");

            if (lang == "TR") trBtn?.AddToClassList("selected");
            else enBtn?.AddToClassList("selected");

            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            var dict = T[selectedLang];

            if (nameLabel != null) nameLabel.text = dict["NameLabel"];
            if (userIdLabel != null) userIdLabel.text = dict["UserIdLabel"];
            if (langLabel != null) langLabel.text = dict["LangLabel"];
            if (startBtn != null) startBtn.text = dict["StartButton"];
            if (namePlaceholder != null) namePlaceholder.text = dict["NamePlaceholder"];
        }

        private void OnNameChanged(ChangeEvent<string> _)
        {
            RefreshUIState();
        }

        private void OnNameFocusIn(FocusInEvent _)
        {
            // Hide placeholder on focus to avoid visual bug
            if (namePlaceholder != null)
                namePlaceholder.style.display = DisplayStyle.None;
        }

        private void OnNameFocusOut(FocusOutEvent _)
        {
            // When leaving field: show placeholder again if still empty
            UpdateNamePlaceholder();
        }

        private void RefreshUIState()
        {
            UpdateStartButtonState();
            UpdateNamePlaceholder();
        }

        private void UpdateStartButtonState()
        {
            bool valid = !string.IsNullOrWhiteSpace(nameField.value);

            startBtn.RemoveFromClassList("enabled");
            startBtn.RemoveFromClassList("disabled");
            startBtn.AddToClassList(valid ? "enabled" : "disabled");
            startBtn.SetEnabled(valid);
        }

        private void UpdateNamePlaceholder()
        {
            if (namePlaceholder == null) return;

            bool empty = string.IsNullOrWhiteSpace(nameField.value);
            namePlaceholder.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnStartClicked()
        {
            Debug.Log($"LOGIN OK | Name={nameField.value} | UserId={userIdField?.value} | Lang={selectedLang}");

            nameField.SetValueWithoutNotify("");
            userIdField.SetValueWithoutNotify("ID: #0000");
           
            // TODO: scene geçişi
            // SceneManager.LoadScene("MainMenu");
        }
    }
}
