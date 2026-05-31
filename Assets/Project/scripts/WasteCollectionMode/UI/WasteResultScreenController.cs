using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Woi.Player;
using WOI.Modules.SDK;

namespace Woi.WasteCollectionMode
{
    [DisallowMultipleComponent]
    public class WasteResultScreenController : MonoBehaviour
    {
        private const string CorrectStatusIconPath =
            "Assets/Project/WasteCollection/UI/IconsPng/circle-check.png";
        private const string IncorrectStatusIconPath =
            "Assets/Project/WasteCollection/UI/IconsPng/circle-x.png";

        private static readonly Color CorrectStatusColor = new(0.376f, 0.647f, 0.980f, 1f);
        private static readonly Color IncorrectStatusColor = new(0.957f, 0.247f, 0.369f, 1f);

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Texture2D correctStatusIcon;
        [SerializeField] private Texture2D incorrectStatusIcon;
        [SerializeField] private WasteCollectTracker collectTracker;
        [SerializeField] private WasteSelectionMenu wasteSelectionMenu;

        [Header("Player")]
        [SerializeField] private Transform playerRoot;
        [SerializeField] private string playerTag = "Player";

        private VisualElement overlay;
        private Label correctCountLabel;
        private Label incorrectCountLabel;
        private ScrollView tableBody;
        private Button restartButton;

        private readonly PlayerMovementLookFreeze movementLookFreeze = new();
        private bool inputFrozen;
        private CursorLockMode savedCursorLockState;
        private bool savedCursorVisible;

        public bool IsVisible => overlay != null && overlay.style.display == DisplayStyle.Flex;

        private void Awake()
        {
            if (wasteSelectionMenu == null)
                wasteSelectionMenu = GetComponent<WasteSelectionMenu>();

            if (uiDocument == null && wasteSelectionMenu != null)
                uiDocument = wasteSelectionMenu.GetComponent<UIDocument>();

            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (collectTracker == null)
                collectTracker = FindFirstObjectByType<WasteCollectTracker>();

            ResolveStatusIcons();
        }

        private void ResolveStatusIcons()
        {
#if UNITY_EDITOR
            if (correctStatusIcon == null)
                correctStatusIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(CorrectStatusIconPath);

            if (incorrectStatusIcon == null)
                incorrectStatusIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(IncorrectStatusIconPath);
#endif
        }

        private void OnEnable()
        {
            if (collectTracker == null)
                collectTracker = FindFirstObjectByType<WasteCollectTracker>();

            if (!TryBindUi())
                return;

            Hide();
        }

        private void OnDisable()
        {
            if (restartButton != null)
                restartButton.clicked -= OnRestartClicked;

            RestorePlayerInput();
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.tabKey.wasPressedThisFrame)
                return;

            if (wasteSelectionMenu != null && wasteSelectionMenu.IsVisible)
                return;

            if (IsVisible)
                Hide();
            else
                Show();
        }

        private bool TryBindUi()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
                return false;

            VisualElement root = uiDocument.rootVisualElement;
            overlay = root.Q<VisualElement>("ResultOverlay");
            correctCountLabel = root.Q<Label>("CorrectCount");
            incorrectCountLabel = root.Q<Label>("IncorrectCount");
            tableBody = root.Q<ScrollView>("TableBody");
            restartButton = root.Q<Button>("RestartButton");

            if (overlay == null)
            {
                Debug.LogError(
                    "[WasteResultScreenController] ResultOverlay not found. " +
                    "Re-run Waste Collection/Setup Result Screen In Scene after updating WasteSelectionMenu.uxml.",
                    this);
                return false;
            }

            if (restartButton != null)
            {
                restartButton.clicked -= OnRestartClicked;
                restartButton.clicked += OnRestartClicked;
            }

            return true;
        }

        public void Show()
        {
            if (overlay == null && !TryBindUi())
                return;

            RefreshContent();
            overlay.style.display = DisplayStyle.Flex;
            FreezePlayerInput();
        }

        public void Hide()
        {
            if (overlay == null)
                return;

            overlay.style.display = DisplayStyle.None;
            RestorePlayerInput();
        }

        private void RefreshContent()
        {
            IReadOnlyList<WasteClassificationRecord> records = collectTracker != null
                ? collectTracker.Classifications
                : System.Array.Empty<WasteClassificationRecord>();

            int correct = 0;
            int incorrect = 0;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].isCorrect)
                    correct++;
                else
                    incorrect++;
            }

            if (correctCountLabel != null)
                correctCountLabel.text = correct.ToString();

            if (incorrectCountLabel != null)
                incorrectCountLabel.text = incorrect.ToString();

            if (tableBody == null)
                return;

            tableBody.Clear();

            if (records.Count == 0)
            {
                tableBody.Add(CreateEmptyRow("Henüz sınıflandırma yapılmadı."));
                return;
            }

            for (int i = 0; i < records.Count; i++)
                tableBody.Add(CreateTableRow(records[i]));
        }

        private static VisualElement CreateEmptyRow(string message)
        {
            var row = new VisualElement();
            row.AddToClassList("table-row");

            var label = new Label(message);
            label.AddToClassList("table-cell");
            label.style.flexGrow = 1;
            row.Add(label);
            return row;
        }

        private VisualElement CreateTableRow(WasteClassificationRecord record)
        {
            var row = new VisualElement();
            row.AddToClassList("table-row");

            row.Add(CreateTableCell(record.wasteName, "col-1"));
            row.Add(CreateTableCell(WasteBinCatalog.GetBinName(record.selectedBinId), "col-2"));
            row.Add(CreateTableCell(WasteBinCatalog.GetBinName(record.correctBinId), "col-3"));
            row.Add(CreateStatusCell(record.isCorrect));

            return row;
        }

        private static VisualElement CreateTableCell(string text, string columnClass)
        {
            var column = new VisualElement();
            column.AddToClassList("table-col");
            column.AddToClassList(columnClass);

            var label = new Label(text);
            label.AddToClassList("table-cell");
            column.Add(label);
            return column;
        }

        private VisualElement CreateStatusCell(bool isCorrect)
        {
            var column = new VisualElement();
            column.AddToClassList("table-col");
            column.AddToClassList("col-4");

            var badge = new VisualElement();
            badge.AddToClassList("status-badge");
            badge.AddToClassList(isCorrect ? "success" : "danger");

            Texture2D iconTexture = isCorrect ? correctStatusIcon : incorrectStatusIcon;
            if (iconTexture != null)
            {
                var icon = new Image
                {
                    image = iconTexture,
                    scaleMode = ScaleMode.ScaleToFit,
                    tintColor = isCorrect ? CorrectStatusColor : IncorrectStatusColor
                };
                icon.AddToClassList("status-icon");
                badge.Add(icon);
            }

            var statusText = new Label(isCorrect ? "DOĞRU" : "HATALI");
            statusText.AddToClassList("status-badge-text");
            statusText.AddToClassList(isCorrect ? "success" : "danger");
            badge.Add(statusText);
            column.Add(badge);
            return column;
        }

        private void OnRestartClicked()
        {
            if (collectTracker != null)
                collectTracker.ClearSession();

            Hide();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void FreezePlayerInput()
        {
            if (inputFrozen)
                return;

            Transform root = ResolvePlayerRoot();
            movementLookFreeze.Freeze(root);

            if (ServiceLocator.TryGet(out IPlayerService playerService))
                playerService.SetPlayerInputEnabled(false);

            savedCursorLockState = UnityEngine.Cursor.lockState;
            savedCursorVisible = UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            inputFrozen = true;
        }

        private void RestorePlayerInput()
        {
            if (!inputFrozen)
                return;

            movementLookFreeze.Restore();

            if (ServiceLocator.TryGet(out IPlayerService playerService))
                playerService.SetPlayerInputEnabled(true);

            UnityEngine.Cursor.lockState = savedCursorLockState;
            UnityEngine.Cursor.visible = savedCursorVisible;
            inputFrozen = false;
        }

        private Transform ResolvePlayerRoot()
        {
            if (playerRoot != null)
                return playerRoot;

            if (ServiceLocator.TryGet(out IPlayerService playerService))
            {
                Transform serviceRoot = playerService.GetPlayerTransform();
                if (serviceRoot != null)
                    return serviceRoot;
            }

            if (!string.IsNullOrWhiteSpace(playerTag))
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
                if (taggedPlayer != null)
                    return taggedPlayer.transform;
            }

            return null;
        }
    }
}
