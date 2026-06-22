namespace Woi.OfficeFire
{
    /// <summary>
    /// Session language resolution shared across Office Fire assemblies (Core, PCVR, scenarios).
    /// </summary>
    public static class OfficeFireSessionLanguage
    {
        public const string English = "en";
        public const string Turkish = "tr";

        private static string _runtimeLanguageCode;

        public static void SetRuntimeLanguageCode(string languageCode)
        {
            _runtimeLanguageCode = string.IsNullOrWhiteSpace(languageCode)
                ? null
                : languageCode.Trim().ToLowerInvariant();
        }

        public static bool UseTurkish()
        {
            return !IsEnglish(ResolveLanguageCode());
        }

        public static string ResolveLanguageCode()
        {
            if (!string.IsNullOrEmpty(_runtimeLanguageCode))
            {
                return _runtimeLanguageCode;
            }

            if (!string.IsNullOrWhiteSpace(OfficeFireLoginSession.LanguageCode))
            {
                return OfficeFireLoginSession.LanguageCode.Trim().ToLowerInvariant();
            }

            return Turkish;
        }

        private static bool IsEnglish(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            code = code.Trim().ToLowerInvariant();
            return code == English || code == "english";
        }
    }
}
