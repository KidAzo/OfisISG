using UnityEngine;

namespace Woi.Localization
{
    public class LanguageManager : MonoBehaviour
    {
        [SerializeField] private Language defaultLanguage = Language.Turkish;

        public static Language CurrentLanguage { get; private set; } = Language.Turkish;

        void Awake()
        {
            var gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager != null)
            {
                var settings = gameManager.GetGameSettings();
                CurrentLanguage = settings.Language;
                return;
            }

            CurrentLanguage = defaultLanguage;
        }

        public static void SetLanguage(Language language)
        {
            CurrentLanguage = language;
        }
    }

    public enum Language
    {
        Turkish,
        English
    }
}
