namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Session context written on the waste login screen before loading the gameplay scene group.
    /// </summary>
    public static class WasteLoginSession
    {
        public static string UserName { get; private set; } = string.Empty;
        public static string UserId { get; private set; } = string.Empty;
        public static string LanguageCode { get; private set; } = "tr";

        public static bool IsSet { get; private set; }

        public static void Set(string userName, string userId, string languageCode)
        {
            UserName = userName ?? string.Empty;
            UserId = userId ?? string.Empty;
            LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "tr" : languageCode.Trim().ToLowerInvariant();
            IsSet = true;
        }

        public static void Clear()
        {
            UserName = string.Empty;
            UserId = string.Empty;
            LanguageCode = "tr";
            IsSet = false;
        }
    }
}
