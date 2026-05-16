using UnityEngine;

/// <summary>
/// Runtime read access for the existing <see cref="ScriptableEnumPortingVariable"/> (<c>AppMode.PC</c> / <c>AppMode.XR</c>).
/// Initialized from the bootstrap / scene loader path that already references the porting asset.
/// </summary>
public static class FirePlatformRuntime
{
    static ScriptableEnumPortingVariable _porting;
    static bool _warnedMissingPorting;

    /// <summary>True after a non-null porting asset was registered.</summary>
    public static bool IsSourceInitialized => _porting != null;

    /// <summary>
    /// Registers the porting ScriptableObject. Safe to call multiple times; the first non-null assignment wins.
    /// </summary>
    public static void Initialize(ScriptableEnumPortingVariable portingVariable)
    {
        if (portingVariable == null)
        {
            Debug.LogWarning(
                "[FirePlatformRuntime] Initialize was called with a null ScriptableEnumPortingVariable. " +
                "Assign the same PortingVariable asset used by SceneLoader / InputManager until PC/XR mode is available.");
            return;
        }

        if (_porting == null)
            _porting = portingVariable;
    }

    /// <summary>Same as <see cref="Initialize"/> but does not replace an existing non-null source.</summary>
    public static void TryInitialize(ScriptableEnumPortingVariable portingVariable)
    {
        if (_porting != null || portingVariable == null)
            return;
        Initialize(portingVariable);
    }

    /// <summary>Current <see cref="AppMode"/> from the porting asset, or <see cref="AppMode.PC"/> if uninitialized.</summary>
    public static AppMode CurrentMode
    {
        get
        {
            if (_porting != null)
                return _porting.CurrentValue;

            WarnPortingMissingOnce();
            return AppMode.PC;
        }
    }

    public static bool IsPC => CurrentMode == AppMode.PC;

    /// <summary>True when porting is set to XR (VR target).</summary>
    public static bool IsVR => CurrentMode == AppMode.XR;

    static void WarnPortingMissingOnce()
    {
        if (_warnedMissingPorting)
            return;
        _warnedMissingPorting = true;
        Debug.LogWarning(
            "[FirePlatformRuntime] Porting settings are not initialized (no ScriptableEnumPortingVariable registered). " +
            "Defaulting reads to AppMode.PC. Assign the asset on FireTrainBootstrapper and/or LoadingScreenController (same asset as existing flow).");
    }
}
