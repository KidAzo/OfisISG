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

        private const string PortingVariablePath =
            "Packages/com.woi.module.fire/Runtime/Porting/PortingVariable.asset";

        public static bool ShouldUseVrPresentation()
        {
            EnsurePortingInitialized();

            // The porting asset is the source of truth once initialized: PC stays PC even
            // if an XR rig/head camera happens to exist in the scene.
            if (FirePlatformRuntime.IsSourceInitialized)
                return FirePlatformRuntime.IsVR;

            // Porting truly unavailable → infer from an active XR rig + head camera.
            return WasteVrHeadCameraResolver.TryGetHeadCamera(null, out _);
        }

        private static void EnsurePortingInitialized()
        {
            if (FirePlatformRuntime.IsSourceInitialized)
                return;

#if UNITY_EDITOR
            var porting = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableEnumPortingVariable>(
                PortingVariablePath);
            if (porting != null)
                FirePlatformRuntime.TryInitialize(porting);
#endif
        }
    }
}
