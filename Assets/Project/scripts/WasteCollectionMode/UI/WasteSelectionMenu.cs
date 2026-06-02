using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Woi.WasteCollectionMode
{
    [RequireComponent(typeof(UIDocument))]
    public class WasteSelectionMenu : MonoBehaviour
    {
        private const string DefaultLibraryPath = "Assets/Project/WasteCollection/UI/WasteBinIconLibrary.asset";

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private WasteBinIconLibrary iconLibrary;

        private VisualElement overlay;
        private VisualElement headerIconHost;
        private VisualElement gridContainer;
        private Label activeItemLabel;
        private Label headerSubtitle;
        private Label headerDesc;
        private Button closeButton;

        private readonly List<WasteBinData> wasteBins = new()
        {
            new WasteBinData("1", "Kağıt-Karton Atıklar", "file-text", new Color(0.14f, 0.38f, 0.88f)),
            new WasteBinData("3", "Plastik Atıklar", "beaker", new Color(0.91f, 0.69f, 0.05f)),
            new WasteBinData("4", "Cam Atıklar", "glass-water", new Color(0.02f, 0.53f, 0.40f)),
            new WasteBinData("5", "Bio-Bozunur Atıklar", "apple", new Color(0.57f, 0.25f, 0.11f)),
            new WasteBinData("6", "Kullanılmış Pil", "battery", new Color(0.86f, 0.15f, 0.15f)),
            new WasteBinData("8", "Elektronik Atık", "monitor", new Color(0.58f, 0.19f, 0.87f)),
            new WasteBinData("9", "Plastik Kapak", "disc", new Color(0.22f, 0.73f, 0.95f)),
            new WasteBinData("10", "Metal Atıklar", "cylinder", new Color(0.58f, 0.65f, 0.72f)),
            new WasteBinData("12", "Geri Kazanılabilir", "trash-2", new Color(0.13f, 0.60f, 0.30f)),
            new WasteBinData("14", "Tıbbi Atık", "briefcase-medical", new Color(0.86f, 0.15f, 0.15f)),
            new WasteBinData("15", "Kompozit Atık", "package", new Color(0.72f, 0.45f, 0.20f))
        };

        public event Action<string> BinSelected;
        public event Action Dismissed;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            ResolveIconLibrary();
        }

        private void OnEnable()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
                return;

            ResolveIconLibrary();

            VisualElement root = uiDocument.rootVisualElement;
            overlay = root.Q<VisualElement>("Overlay");
            headerIconHost = root.Q<VisualElement>("HeaderIcon");
            gridContainer = root.Q<VisualElement>("GridContainer");
            activeItemLabel = root.Q<Label>("ActiveItemName");
            headerSubtitle = root.Q<Label>("HeaderSubtitle");
            headerDesc = root.Q<Label>("HeaderDesc");
            closeButton = root.Q<Button>("CloseButton");

            ApplyLocalizedTexts();

            if (closeButton != null)
                closeButton.clicked += OnCloseClicked;

            ApplyHeaderIcon();

            if (gridContainer != null)
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
            {
                activeItemLabel.text = string.IsNullOrWhiteSpace(itemName)
                    ? "-"
                    : WasteNameCatalog.GetDisplayName(itemName);
            }

            if (overlay != null)
                overlay.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (overlay != null)
                overlay.style.display = DisplayStyle.None;
        }

        public bool IsVisible => overlay != null && overlay.style.display == DisplayStyle.Flex;

        private void ApplyLocalizedTexts()
        {
            bool english = WasteCollectionLocalization.IsEnglish;

            if (headerSubtitle != null)
                headerSubtitle.text = WasteCollectionLocalization.SelectionHeaderSubtitle(english);

            if (headerDesc != null)
                headerDesc.text = WasteCollectionLocalization.SelectionHeaderDesc(english);
        }

        private void ResolveIconLibrary()
        {
            if (iconLibrary != null)
                return;

#if UNITY_EDITOR
            iconLibrary = UnityEditor.AssetDatabase.LoadAssetAtPath<WasteBinIconLibrary>(DefaultLibraryPath);
#endif
        }

        private void ApplyHeaderIcon()
        {
            if (headerIconHost == null || iconLibrary == null || iconLibrary.HeaderIcon == null)
                return;

            headerIconHost.Clear();
            ApplyTextureIcon(headerIconHost, iconLibrary.HeaderIcon, new Color(0f, 1f, 0.698f, 1f), 32f);
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
                iconContainer.style.backgroundColor = bin.ThemeColor * 0.35f;

                if (iconLibrary != null && iconLibrary.TryGetIcon(bin.IconKey, out Texture2D texture))
                {
                    var icon = new VisualElement();
                    icon.AddToClassList("bin-button-icon");
                    ApplyTextureIcon(icon, texture, Color.white, 28f);
                    iconContainer.Add(icon);
                }

                button.Add(iconContainer);

                var nameLabel = new Label(WasteBinCatalog.GetBinName(captured.Id));
                nameLabel.AddToClassList("bin-button-text");
                button.Add(nameLabel);

                gridContainer.Add(button);
            }
        }

        private static void ApplyTextureIcon(VisualElement target, Texture2D texture, Color tint, float size)
        {
            target.style.width = size;
            target.style.height = size;
            target.style.flexShrink = 0;
            target.style.backgroundImage = new StyleBackground(texture);
            target.style.unityBackgroundImageTintColor = tint;
            target.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
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
            public WasteBinData(string id, string name, string iconKey, Color themeColor)
            {
                Id = id;
                Name = name;
                IconKey = iconKey;
                ThemeColor = themeColor;
            }

            public string Id { get; }
            public string Name { get; }
            public string IconKey { get; }
            public Color ThemeColor { get; }
        }
    }
}
