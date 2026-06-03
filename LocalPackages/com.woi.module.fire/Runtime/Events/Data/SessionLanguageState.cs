using System;

namespace Woi.Events.Data
{
    /// <summary>
    /// Cross-scene language chosen on the session overlay (or login). Readable from fire, waste, and office assemblies.
    /// </summary>
    public static class SessionLanguageState
    {
        public static event Action LanguageChanged;
        public const string English = "en";
        public const string Turkish = "tr";

        private static string languageCode = Turkish;
        private static bool hasUserChoice;

        public static bool HasUserChoice => hasUserChoice;

        public static string LanguageCode => languageCode;

        public static bool IsEnglish =>
            languageCode == English || string.Equals(languageCode, "english", System.StringComparison.OrdinalIgnoreCase);

        public static void RecordUserChoice(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return;

            languageCode = code.Trim().ToLowerInvariant();
            hasUserChoice = true;
            LanguageChanged?.Invoke();
        }

        public static void Clear()
        {
            languageCode = Turkish;
            hasUserChoice = false;
            LanguageChanged?.Invoke();
        }
    }
}
