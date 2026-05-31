using UnityEngine;
using UnityEngine.UIElements;
using Woi.Events;

namespace Woi.WasteCollectionMode
{
    [DisallowMultipleComponent]
    public class WasteCollectionCounterUI : MonoBehaviour
    {
        private const string CounterIconPath =
            "Assets/Project/WasteCollection/UI/IconsPng/trash-2.png";

        private static readonly Color CounterIconTint = new(0f, 1f, 0.698f, 1f);

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Texture2D counterIcon;

        private Label counterLabel;
        private Image counterIconImage;
        private int totalCount;
        private int collectedCount;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            ResolveCounterIcon();
            totalCount = CountSceneWastes();
        }

        private void OnEnable()
        {
            EventBus.Register<WasteCollectedEvent>(OnWasteCollected);

            if (WasteCollectTracker.TryGetActive(out WasteCollectTracker tracker))
                collectedCount = tracker.Records.Count;

            TryBindUi();
            RefreshLabel();
        }

        private void OnDisable()
        {
            EventBus.Deregister<WasteCollectedEvent>(OnWasteCollected);
        }

        private void ResolveCounterIcon()
        {
#if UNITY_EDITOR
            if (counterIcon == null)
                counterIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(CounterIconPath);
#endif
        }

        private bool TryBindUi()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
                return false;

            VisualElement root = uiDocument.rootVisualElement;
            counterLabel = root.Q<Label>("WasteCounterLabel");
            counterIconImage = root.Q<Image>("WasteCounterIcon");

            if (counterLabel == null)
            {
                Debug.LogError(
                    "[WasteCollectionCounterUI] WasteCounterLabel not found. " +
                    "Update WasteSelectionMenu.uxml and re-run Waste Collection/Setup Result Screen In Scene.",
                    this);
                return false;
            }

            ApplyCounterIcon();
            return true;
        }

        private void ApplyCounterIcon()
        {
            if (counterIconImage == null || counterIcon == null)
                return;

            counterIconImage.image = counterIcon;
            counterIconImage.tintColor = CounterIconTint;
        }

        private void OnWasteCollected(WasteCollectedEvent evt)
        {
            collectedCount = evt.TotalCollected;
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (counterLabel == null)
                return;

            counterLabel.text = $"{collectedCount}/{totalCount}";
        }

        private static int CountSceneWastes()
        {
            WasteController[] wastes = FindObjectsByType<WasteController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            return wastes != null ? wastes.Length : 0;
        }
    }
}
