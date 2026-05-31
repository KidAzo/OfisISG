using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Woi.WasteCollectionMode
{
    [RequireComponent(typeof(UIDocument))]
    public class WasteSelectionMenu : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        private VisualElement overlay;
        private VisualElement gridContainer;
        private Label activeItemLabel;
        private Button closeButton;

        private readonly List<WasteBinData> wasteBins = new()
        {
            new WasteBinData("1", "Kağıt Atıklar", new Color(0.14f, 0.38f, 0.88f)),
            new WasteBinData("2", "Karton Atıklar", new Color(0.31f, 0.27f, 0.89f)),
            new WasteBinData("3", "Plastik Atıklar", new Color(0.91f, 0.69f, 0.05f)),
            new WasteBinData("4", "Cam Atıklar", new Color(0.02f, 0.53f, 0.40f)),
            new WasteBinData("5", "Organik Yemek", new Color(0.57f, 0.25f, 0.11f)),
            new WasteBinData("6", "Kullanılmış Pil", new Color(0.86f, 0.15f, 0.15f)),
            new WasteBinData("7", "Toner & Kartuş", new Color(0.62f, 0.07f, 0.24f)),
            new WasteBinData("8", "Elektronik Atık", new Color(0.58f, 0.19f, 0.87f)),
            new WasteBinData("9", "Plastik Kapak", new Color(0.22f, 0.73f, 0.95f)),
            new WasteBinData("10", "Metal Kutu", new Color(0.58f, 0.65f, 0.72f)),
            new WasteBinData("11", "Sigara İzmariti", new Color(0.32f, 0.32f, 0.35f)),
            new WasteBinData("12", "Geri Dönüşmez", new Color(0.15f, 0.15f, 0.15f)),
            new WasteBinData("13", "Tehlikeli Atık", new Color(0.98f, 0.75f, 0.18f)),
            new WasteBinData("14", "Tıbbi Atık", new Color(0.86f, 0.15f, 0.15f)),
            new WasteBinData("15", "Ampul/Floresan", new Color(0.97f, 0.43f, 0.11f))
        };

        public event Action<string> BinSelected;
        public event Action Dismissed;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
                return;

            VisualElement root = uiDocument.rootVisualElement;
            overlay = root.Q<VisualElement>("Overlay");
            gridContainer = root.Q<VisualElement>("GridContainer");
            activeItemLabel = root.Q<Label>("ActiveItemName");
            closeButton = root.Q<Button>("CloseButton");

            if (closeButton != null)
                closeButton.clicked += OnCloseClicked;

            if (gridContainer != null && gridContainer.childCount == 0)
                GenerateGrid();

            Hide();
        }

        private void OnDisable()
        {
            if (closeButton != null)
                closeButton.clicked -= OnCloseClicked;
        }

        public void Show(string itemName)
        {
            if (activeItemLabel != null)
                activeItemLabel.text = string.IsNullOrWhiteSpace(itemName) ? "-" : itemName;

            if (overlay != null)
                overlay.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (overlay != null)
                overlay.style.display = DisplayStyle.None;
        }

        private void GenerateGrid()
        {
            gridContainer.Clear();

            foreach (WasteBinData bin in wasteBins)
            {
                WasteBinData captured = bin;
                var button = new Button();
                button.AddToClassList("bin-button");
                button.clicked += () => OnBinSelected(captured.Id);

                var colorLine = new VisualElement();
                colorLine.AddToClassList("bin-button-color-line");
                colorLine.style.backgroundColor = bin.ThemeColor;
                button.Add(colorLine);

                var iconContainer = new VisualElement();
                iconContainer.AddToClassList("bin-button-icon-container");
                var icon = new VisualElement();
                icon.AddToClassList("bin-button-icon");
                iconContainer.Add(icon);
                iconContainer.style.backgroundColor = bin.ThemeColor * 0.35f;
                button.Add(iconContainer);

                var nameLabel = new Label(bin.Name);
                nameLabel.AddToClassList("bin-button-text");
                button.Add(nameLabel);

                gridContainer.Add(button);
            }
        }

        private void OnBinSelected(string binId)
        {
            BinSelected?.Invoke(binId);
            Hide();
            Dismissed?.Invoke();
        }

        private void OnCloseClicked()
        {
            Hide();
            Dismissed?.Invoke();
        }

        private readonly struct WasteBinData
        {
            public WasteBinData(string id, string name, Color themeColor)
            {
                Id = id;
                Name = name;
                ThemeColor = themeColor;
            }

            public string Id { get; }
            public string Name { get; }
            public Color ThemeColor { get; }
        }
    }
}
