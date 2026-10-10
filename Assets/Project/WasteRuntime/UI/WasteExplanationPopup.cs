using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Post-classification explanation popup. Shares the WasteCollectionUI UIDocument and shows a
    /// correct/wrong status plus the waste's localized explanation text. The flow controller opens
    /// it after a bin is chosen. The visual hides itself after <see cref="explanationPopupDuration"/>
    /// and does not wait for the explanation voice. Works the same in PC and VR because it lives on
    /// the same root the VR presenter renders in world space.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteExplanationPopup : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Texture2D correctIcon;
        [SerializeField] private Texture2D incorrectIcon;
        [SerializeField] private float explanationPopupDuration = 2.5f;

        private VisualElement overlay;
        private VisualElement statusIconHost;
        private Label statusLabel;
        private Label explanationLabel;
        private bool bound;
        private int hideGeneration;
        private Coroutine hideRoutine;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            CancelPendingHide();
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
            ScheduleAutoHide();
        }

        public void Hide()
        {
            CancelPendingHide();
            ApplyHidden();
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

            ApplyHidden();
            bound = true;
        }

        private void ScheduleAutoHide()
        {
            CancelPendingHide();
            if (!isActiveAndEnabled)
                return;

            int generation = hideGeneration;
            hideRoutine = StartCoroutine(HideAfterDuration(generation));
        }

        private void CancelPendingHide()
        {
            hideGeneration++;
            if (hideRoutine == null)
                return;

            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        private IEnumerator HideAfterDuration(int generation)
        {
            float duration = Mathf.Max(0f, explanationPopupDuration);
            if (duration > 0f)
                yield return new WaitForSeconds(duration);

            if (generation != hideGeneration)
                yield break;

            hideRoutine = null;
            ApplyHidden();
        }

        private void ApplyHidden()
        {
            if (overlay != null)
                overlay.style.display = DisplayStyle.None;
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
