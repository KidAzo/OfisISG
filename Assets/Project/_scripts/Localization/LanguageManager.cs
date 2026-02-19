using Reflex.Attributes;
using UnityEngine;

namespace Woi.Localization
{
    public class LanguageManager : MonoBehaviour
    {
        [SerializeField] private Language defaultLanguage = Language.Turkish;
        [Inject] IGameManager gameManager;

        public static Language CurrentLanguage { get; private set; } = Language.Turkish;

        void Awake()
        {
            CurrentLanguage = gameManager.GetGameSettings().Language;
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

