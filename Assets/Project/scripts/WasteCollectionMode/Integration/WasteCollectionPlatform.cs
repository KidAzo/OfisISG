namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Waste Collection platform reads — delegates to Fire module porting (AppMode.PC / AppMode.XR).
    /// </summary>
    public static class WasteCollectionPlatform
    {
        public static bool IsVR =>
            FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR;

        public static bool IsPC => !IsVR;
    }
}
