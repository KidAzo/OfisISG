namespace Woi.OfficeFire
{
    /// <summary>
    /// Resolves hover instruction text for the active platform (PC vs VR) and session language (EN vs TR).
    /// </summary>
    public static class OfficeFireInstructionPromptText
    {
        public const string DefaultVrInteractEnglish = "Pull trigger to interact";
        public const string DefaultVrInteractTurkish = "Etkileşim için tetiğe basın";

        public static bool HasAnyText(
            string pcEnglish,
            string pcTurkish,
            string vrEnglish = null,
            string vrTurkish = null) =>
            !string.IsNullOrWhiteSpace(pcEnglish)
            || !string.IsNullOrWhiteSpace(pcTurkish)
            || !string.IsNullOrWhiteSpace(vrEnglish)
            || !string.IsNullOrWhiteSpace(vrTurkish);

        public static string Resolve(
            string pcEnglish,
            string pcTurkish,
            string vrEnglish = null,
            string vrTurkish = null)
        {
            bool isVr = FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR;
            bool useTurkish = OfficeFireSessionLanguage.UseTurkish();

            if (isVr)
            {
                if (useTurkish && !string.IsNullOrWhiteSpace(vrTurkish))
                {
                    return vrTurkish;
                }

                if (!string.IsNullOrWhiteSpace(vrEnglish))
                {
                    return vrEnglish;
                }
            }
            else
            {
                if (useTurkish && !string.IsNullOrWhiteSpace(pcTurkish))
                {
                    return pcTurkish;
                }

                if (!string.IsNullOrWhiteSpace(pcEnglish))
                {
                    return pcEnglish;
                }
            }

            if (useTurkish)
            {
                if (!string.IsNullOrWhiteSpace(isVr ? pcTurkish : vrTurkish))
                {
                    return isVr ? pcTurkish : vrTurkish;
                }
            }

            if (!string.IsNullOrWhiteSpace(isVr ? pcEnglish : vrEnglish))
            {
                return isVr ? pcEnglish : vrEnglish;
            }

            return useTurkish ? pcTurkish : pcEnglish;
        }
    }
}
