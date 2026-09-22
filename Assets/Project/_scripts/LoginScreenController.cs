using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Woi.Events;

namespace Woi.UI
{
    public class LoginScreenController : MonoBehaviour
    {
        [SerializeField] private UIDocument ui;
        // UI refs
        private Label nameLabel, userIdLabel, langLabel;
        private Label namePlaceholder;
        private TextField nameField, userIdField;
        private Button trBtn, enBtn, startBtn, closeBtn;
        private VisualElement nameWrapper;
        private Label userIdPlaceholder;


        private string selectedLang = "TR";

        private readonly Dictionary<string, Dictionary<string, string>> T = new()
        {
            ["TR"] = new()
            {
                ["NameLabel"] = "OYUNCU ADI",
                ["NamePlaceholder"] = "Oyuncu Adını Gir",
                ["UserIdLabel"] = "KULLANICI SİCİL NUMARASI (ID)",
                ["LangLabel"] = "DİL SEÇİMİ",
                ["StartButton"] = "OYUNA BAŞLA",
                ["Loading"] = "YÜKLENİYOR...",
            },
            ["EN"] = new()
            {
                ["NameLabel"] = "PLAYER NAME",
                ["NamePlaceholder"] = "Enter player name",
                ["UserIdLabel"] = "USER ID",
                ["LangLabel"] = "LANGUAGE",
                ["StartButton"] = "START GAME",
                ["Loading"] = "LOADING...",
            }
        };

        VisualElement loadingOverlay;
        VisualElement loadingBarFill;
        Label loadingStatus;
        bool starting;

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
            var root = ui.rootVisualElement;

            // Query
            nameLabel = root.Q<Label>("NameLabel");
            userIdLabel = root.Q<Label>("UserIdLabel");
            langLabel = root.Q<Label>("LangLabel");


            nameField = root.Q<TextField>("NameField");
            userIdField = root.Q<TextField>("UserIdField");

            namePlaceholder = root.Q<Label>("NamePlaceholder");
            nameWrapper = root.Q<VisualElement>("NameInputWrapper");

            trBtn = root.Q<Button>("TRButton");
            enBtn = root.Q<Button>("ENButton");
            startBtn = root.Q<Button>("StartButton");
            closeBtn = root.Q<Button>("CloseButton");
            loadingOverlay = root.Q<VisualElement>("LoadingOverlay");
            loadingBarFill = root.Q<VisualElement>("LoadingBarFill");
            loadingStatus = root.Q<Label>("LoadingStatus");

            userIdPlaceholder = root.Q<Label>("UserIdPlaceholder");

            userIdField.RegisterValueChangedCallback(_ => UpdateUserIdPlaceholder());

            // Safety
            if (nameField == null || userIdField == null || startBtn == null)
            {
                Debug.LogError("UXML name mismatch.");
                enabled = false;
                return;
            }

            // --- EVENTS ---
            trBtn.clicked += () => SetLanguage("TR");
            enBtn.clicked += () => SetLanguage("EN");
            startBtn.clicked += OnStartClicked;
            closeBtn.clicked += QuitGame;   

            nameField.RegisterValueChangedCallback(_ => RefreshUIState());

            // Placeholder focus behavior
            nameField.RegisterCallback<FocusInEvent>(_ =>
            {
                namePlaceholder.style.display = DisplayStyle.None;
            });

            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                UpdateNamePlaceholder();
            });

            // Wrapper click → focus name (SADECE name)
            if (nameWrapper != null)
            {
                nameWrapper.RegisterCallback<PointerDownEvent>(_ =>
                {
                    nameField.Focus();
                });
            }

            userIdField.RegisterValueChangedCallback(OnUserIdChanged);


            // --- INIT ---
            nameField.SetValueWithoutNotify("");
            userIdField.SetValueWithoutNotify("");   // ID alanı boş, SEÇİLEBİLİR
            selectedLang = "TR";

            SetLanguage(selectedLang);
            RefreshUIState();
        }

        private void OnUserIdChanged(ChangeEvent<string> evt)
        {
            if (userIdField == null) return;

            // sadece rakamları tut
            string digitsOnly = "";
            foreach (char c in evt.newValue)
            {
                if (char.IsDigit(c))
                    digitsOnly += c;
            }

            // değiştiyse geri yaz (loop yapmaması için notify kapalı)
            if (digitsOnly != evt.newValue)
            {
                userIdField.SetValueWithoutNotify(digitsOnly);
            }

            UpdateUserIdPlaceholder();
        }


        private void UpdateUserIdPlaceholder()
        {
            if (userIdPlaceholder == null) return;

            bool empty = string.IsNullOrWhiteSpace(userIdField.value);
            userIdPlaceholder.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetLanguage(string lang)
        {
            selectedLang = lang;

            trBtn.RemoveFromClassList("selected");
            enBtn.RemoveFromClassList("selected");

            if (lang == "TR") trBtn.AddToClassList("selected");
            else enBtn.AddToClassList("selected");

            ApplyLocalization();
        }

        void QuitGame()
        {
            Application.Quit();            
        }

        private void ApplyLocalization()
        {
            var dict = T[selectedLang];

            nameLabel.text = dict["NameLabel"];
            userIdLabel.text = dict["UserIdLabel"];
            langLabel.text = dict["LangLabel"];
            startBtn.text = dict["StartButton"];
            namePlaceholder.text = dict["NamePlaceholder"];
        }

        private void RefreshUIState()
        {
            UpdateStartButton();
            UpdateNamePlaceholder();
        }

        private void UpdateStartButton()
        {
            if (starting)
                return;

            bool valid = !string.IsNullOrWhiteSpace(nameField.value);

            startBtn.SetEnabled(valid);
            startBtn.EnableInClassList("enabled", valid);
            startBtn.EnableInClassList("disabled", !valid);
        }

        private void UpdateNamePlaceholder()
        {
            bool empty = string.IsNullOrWhiteSpace(nameField.value);
            namePlaceholder.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnStartClicked()
        {
            if (starting)
                return;

            if (string.IsNullOrWhiteSpace(nameField.value))
                return;

            starting = true;
            startBtn.SetEnabled(false);
            startBtn.EnableInClassList("enabled", false);
            startBtn.EnableInClassList("disabled", false);
            startBtn.EnableInClassList("loading", true);
            startBtn.text = T[selectedLang]["Loading"];

            if (loadingStatus != null)
                loadingStatus.text = T[selectedLang]["Loading"];
            if (loadingOverlay != null)
            {
                loadingOverlay.AddToClassList("visible");
                loadingOverlay.style.display = DisplayStyle.Flex;
                loadingOverlay.pickingMode = PickingMode.Position;
            }
            SetLoadProgress(0f);

            string playerName = nameField.value;
            int playerId = int.TryParse(userIdField.value, out int id) ? id : 0;
            int language = selectedLang == "TR" ? 0 : 1;
            Debug.Log($"LOGIN | Name={playerName} | ID={userIdField.value} | Lang={selectedLang}");

            StartCoroutine(RaiseLoggedAfterOverlayFrame(playerName, playerId, language));
        }

        IEnumerator RaiseLoggedAfterOverlayFrame(string playerName, int playerId, int language)
        {
            // UI Toolkit resolves style after Update. Wait until that pass has painted
            // before HazardHuntLogged starts Office loading.
            yield return null;
            yield return new WaitForEndOfFrame();

            if (!starting)
                yield break;

            EventBus.Raise(new HazardHuntLogged(playerName, playerId, language));
        }

        public void SetLoadProgress(float progress)
        {
            if (loadingBarFill == null)
                return;

            float clamped = Mathf.Clamp01(progress);
            loadingBarFill.style.width = Length.Percent(clamped * 100f);
        }

        public void RecoverFromFailedLoad()
        {
            starting = false;
            if (loadingOverlay != null)
            {
                loadingOverlay.RemoveFromClassList("visible");
                loadingOverlay.style.display = StyleKeyword.Null;
                loadingOverlay.pickingMode = PickingMode.Ignore;
            }
            startBtn.EnableInClassList("loading", false);
            RefreshUIState();
            ApplyLocalization();
        }
    }
}
