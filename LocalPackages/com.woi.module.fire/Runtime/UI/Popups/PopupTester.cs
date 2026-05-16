using UnityEngine;
using Woi.UI.Popups.Localization;

namespace Woi.UI.Popups
{
    /// <summary>Runtime keyboard tests for <see cref="PopupService"/> and <see cref="LocalizationService"/>.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/UI/Tests/Popup Tester")]
    public sealed class PopupTester : MonoBehaviour
    {
        [SerializeField] private PopupService popupService;
        [SerializeField] private LocalizationService localizationService;

        [SerializeField] private PopupDefinition[] popups = new PopupDefinition[0];

        [Header("Keys")]
        [SerializeField] private KeyCode showPopup0 = KeyCode.F9;
        [SerializeField] private KeyCode showPopup1 = KeyCode.F10;
        [SerializeField] private KeyCode showPopup2 = KeyCode.F11;

        [Header("Language")]
        [SerializeField] private KeyCode englishKey = KeyCode.Alpha1;
        [SerializeField] private KeyCode turkishKey = KeyCode.Alpha2;
        [SerializeField] private KeyCode customLanguageKey = KeyCode.Alpha3;
        [SerializeField] private string customLanguageCode = "de";

        private void Awake()
        {
            if (popupService == null)
                popupService = FindFirstObjectByType<PopupService>();

            if (localizationService == null)
                localizationService = LocalizationService.Instance;
        }

        private void Update()
        {
            if (popupService == null)
                return;

            if (Input.GetKeyDown(showPopup0))
                TryShow(0);

            if (Input.GetKeyDown(showPopup1))
                TryShow(1);

            if (Input.GetKeyDown(showPopup2))
                TryShow(2);

            if (localizationService == null)
                return;

            if (Input.GetKeyDown(englishKey))
                localizationService.SetLanguage(LocalizationService.English);

            if (Input.GetKeyDown(turkishKey))
                localizationService.SetLanguage(LocalizationService.Turkish);

            if (Input.GetKeyDown(customLanguageKey) && !string.IsNullOrWhiteSpace(customLanguageCode))
                localizationService.SetLanguage(customLanguageCode);
        }

        private void TryShow(int index)
        {
            if (popups == null || index < 0 || index >= popups.Length || popups[index] == null)
                return;

            popupService.Show(popups[index]);
        }
    }
}
