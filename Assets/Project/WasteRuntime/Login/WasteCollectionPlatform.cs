namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Waste platform. Same decision as Hazard Hunt: Hub, then Quest, then StandalonePlatformMode, then PC.
    /// </summary>
    public static class WasteCollectionPlatform
    {
        public static bool IsPortingVr =>
            FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR;

        public static bool IsVR => ShouldUseVrPresentation();

        public static bool IsPC => !ShouldUseVrPresentation();

        public static bool ShouldUseVrPresentation()
        {
            return StandalonePlatform.UseVr(WasteHubLaunch.IsHubLaunch, WasteHubLaunch.IsVr);
        }
    }
}
