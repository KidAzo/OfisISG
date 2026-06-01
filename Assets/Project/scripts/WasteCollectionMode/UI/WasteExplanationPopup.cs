using UnityEngine;
using UnityEngine.UIElements;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Post-classification explanation popup. Shares the WasteCollectionUI UIDocument and shows a
    /// correct/wrong status plus the waste's localized explanation text. The flow controller opens
    /// it after a bin is chosen and closes it when the explanation audio finishes. Works the same in
    /// PC and VR because it lives on the same root the VR presenter renders in world space.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteExplanationPopup : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Texture2D correctIcon;
        [SerializeField] private Texture2D incorrectIcon;

        private VisualElement overlay;
        private VisualElement statusIconHost;
        private Label statusLabel;
        private Label explanationLabel;
        private bool bound;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            Bind();
        }

        public bool IsVisible => overlay != null && overlay.style.display == DisplayStyle.Flex;

        public void Show(bool isCorrect, string explanationText)
        {
            Bind();
            if (overlay == null)
                return;

            bool english = WasteCollectionLocalization.IsEnglish;

            if (statusLabel != null)
            {
                statusLabel.text = isCorrect
                    ? WasteCollectionLocalization.StatusCorrect(english)
                    : WasteCollectionLocalization.StatusIncorrect(english);

                statusLabel.RemoveFromClassList("explanation-status--correct");
                statusLabel.RemoveFromClassList("explanation-status--wrong");
                statusLabel.AddToClassList(isCorrect
                    ? "explanation-status--correct"
                    : "explanation-status--wrong");
            }

            ApplyStatusIcon(isCorrect);

            if (explanationLabel != null)
                explanationLabel.text = explanationText ?? string.Empty;

            overlay.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (overlay != null)
                overlay.style.display = DisplayStyle.None;
        }

        private void Bind()
        {
            if (bound)
                return;

            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (uiDocument == null || uiDocument.rootVisualElement == null)
                return;

            VisualElement root = uiDocument.rootVisualElement;
            overlay = root.Q<VisualElement>("ExplanationOverlay");
            statusIconHost = root.Q<VisualElement>("ExplanationStatusIcon");
            statusLabel = root.Q<Label>("ExplanationStatusLabel");
            explanationLabel = root.Q<Label>("ExplanationText");

            if (overlay == null)
                return;

            Hide();
            bound = true;
        }

        private void ApplyStatusIcon(bool isCorrect)
        {
            if (statusIconHost == null)
                return;

            Texture2D icon = isCorrect ? correctIcon : incorrectIcon;
            if (icon == null)
            {
                statusIconHost.style.backgroundImage = StyleKeyword.None;
                return;
            }

            statusIconHost.style.backgroundImage = new StyleBackground(icon);
            statusIconHost.style.unityBackgroundImageTintColor = isCorrect
                ? new Color(0.21f, 0.78f, 0.35f)
                : new Color(0.91f, 0.23f, 0.23f);
            statusIconHost.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        }
    }
}
