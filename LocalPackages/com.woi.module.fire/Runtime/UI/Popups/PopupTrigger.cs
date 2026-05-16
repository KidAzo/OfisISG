using UnityEngine;
using UnityEngine.Events;
using Woi.UI.Popups.Localization;

namespace Woi.UI.Popups
{
    /// <summary>
    /// Inspector-friendly hooks for <see cref="PopupService"/> — no announcement/audio dependency.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/UI/Popup Trigger")]
    public sealed class PopupTrigger : MonoBehaviour
    {
        [SerializeField] private PopupService popupService;
        [SerializeField] private PopupDefinition popupDefinition;
        [Tooltip("If set, overrides popupDefinition: picks English or Turkish asset from LocalizationService.")]
        [SerializeField] private LocalizedPopupDefinition localizedPopupDefinition;

        [SerializeField] private bool showOnStart;
        [SerializeField] private bool showOnEnable;

        [SerializeField] private KeyCode testKey = KeyCode.None;

        [Header("ShowWithText defaults (inspector / UnityEvent)")]
        [SerializeField] private string textTitle = "Title";
        [SerializeField] private string textMessage = "Message";
        [SerializeField] private PopupType textType = PopupType.Info;

        public UnityEvent onPopupShown;
        public UnityEvent onPopupHidden;

        private void Awake()
        {
            if (popupService == null)
                popupService = FindFirstObjectByType<PopupService>();
        }

        private void OnEnable()
        {
            if (popupService != null)
            {
                popupService.OnPopupShown += HandlePopupShown;
                popupService.OnPopupHidden += HandlePopupHidden;
            }

            if (showOnEnable)
                Show();
        }

        private void Start()
        {
            if (showOnStart)
                Show();
        }

        private void OnDisable()
        {
            if (popupService != null)
            {
                popupService.OnPopupShown -= HandlePopupShown;
                popupService.OnPopupHidden -= HandlePopupHidden;
            }
        }

        private void Update()
        {
            if (testKey != KeyCode.None && Input.GetKeyDown(testKey))
                Show();
        }

        private void HandlePopupShown(PopupDefinition _) => onPopupShown?.Invoke();
        private void HandlePopupHidden() => onPopupHidden?.Invoke();

        public void Show()
        {
            if (popupService == null)
                return;

            PopupDefinition def = localizedPopupDefinition != null
                ? localizedPopupDefinition.ResolveForCurrentLanguage()
                : popupDefinition;

            if (def == null)
                return;

            popupService.Show(def);
        }

        public void Hide() => popupService?.Hide();

        /// <summary>Uses <see cref="textType"/> from this component.</summary>
        public void ShowWithText(string title, string message) =>
            popupService?.ShowText(title, message, textType);

        /// <summary>Inspector button: uses serialized title/message fields.</summary>
        public void ShowWithTextFromFields() =>
            ShowWithText(textTitle, textMessage);

        public void ShowWithText(string title, string message, PopupType type) =>
            popupService?.ShowText(title, message, type);
    }
}
