namespace Woi.OfficeFire
{
    /// <summary>
    /// Session context set on the Office Fire login screen before loading the gameplay scene.
    /// <see cref="OfficeFireScenarioBootstrapper"/> reads <see cref="SelectedScenarioId"/> in Start()
    /// when <see cref="IsSet"/> is true.
    /// </summary>
    public static class OfficeFireLoginSession
    {
        public static string UserName { get; private set; } = string.Empty;
        public static string UserId { get; private set; } = string.Empty;
        public static string LanguageCode { get; private set; } = "tr";
        public static OfficeFireScenarioId SelectedScenarioId { get; private set; } = OfficeFireScenarioId.ServerRoom;

        public static bool IsSet { get; private set; }

        public static void Set(
            string userName,
            string userId,
            string languageCode,
            OfficeFireScenarioId scenarioId)
        {
            UserName = userName ?? string.Empty;
            UserId = userId ?? string.Empty;
            LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "tr" : languageCode.Trim().ToLowerInvariant();
            SelectedScenarioId = scenarioId;
            IsSet = true;
        }

        public static void Clear()
        {
            UserName = string.Empty;
            UserId = string.Empty;
            LanguageCode = "tr";
            SelectedScenarioId = OfficeFireScenarioId.ServerRoom;
            IsSet = false;
        }

        /// <summary>Clears only the pending scenario selection flag; keeps login identity for result/CSV.</summary>
        public static void MarkScenarioConsumed()
        {
            IsSet = false;
        }
    }
}
