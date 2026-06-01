namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Waste Collection platform reads — delegates to Fire module porting (AppMode.PC / AppMode.XR).
    /// </summary>
    public static class WasteCollectionPlatform
    {
        /// <summary>Porting asset says XR (requires <see cref="FirePlatformRuntime"/> initialized).</summary>
        public static bool IsPortingVr =>
            FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR;

        /// <summary>
        /// Use VR waste presentation (world UI follow, grip exit, etc.).
        /// True when porting is XR, or when an active XR rig + head camera exists in the scene.
        /// </summary>
        public static bool IsVR => ShouldUseVrPresentation();

        public static bool IsPC => !ShouldUseVrPresentation();

        public static bool ShouldUseVrPresentation()
        {
            if (IsPortingVr)
                return true;

            return WasteVrHeadCameraResolver.TryGetHeadCamera(null, out _);
        }
    }
}
