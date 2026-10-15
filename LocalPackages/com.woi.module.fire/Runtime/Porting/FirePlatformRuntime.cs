using UnityEngine;

/// <summary>
/// Runtime read access for the existing <see cref="ScriptableEnumPortingVariable"/> (<c>AppMode.PC</c> / <c>AppMode.XR</c>).
/// Initialized from the bootstrap / scene loader path that already references the porting asset.
/// </summary>
public static class FirePlatformRuntime
{
    static ScriptableEnumPortingVariable _porting;
    static bool _warnedMissingPorting;
    static bool _resolvedTraced;

    /// <summary>Hub platform string before pc/vr normalization. Diagnostic only.</summary>
    public static string RawHubPlatform { get; private set; } = "<not-set>";

    public static void NoteRawHubPlatform(string raw)
    {
        RawHubPlatform = string.IsNullOrEmpty(raw) ? "<empty>" : raw;
        Debug.Log("[FireTrace] HubPlatform raw=" + RawHubPlatform + " beforeNormalization");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetTrace()
    {
        _resolvedTraced = false;
        RawHubPlatform = "<not-set>";
    }

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

    /// <summary>Current <see cref="AppMode"/> from the porting asset, or XR device fallback if uninitialized.</summary>
    public static AppMode CurrentMode
    {
        get
        {
            TraceResolved("CurrentMode");
            if (_porting != null)
                return _porting.CurrentValue;

            if (IsXrDeviceActiveFallback())
                return AppMode.XR;

            WarnPortingMissingOnce();
            return AppMode.PC;
        }
    }

    public static bool IsPC => !IsVR && CurrentMode == AppMode.PC;

    /// <summary>
    /// True on VR targets. Quest/Android uses an active XR device even when the porting asset still says PC
    /// (Hub Addressables can register a PC ScriptableObject instance before module wiring runs).
    /// </summary>
    public static bool IsVR
    {
        get
        {
            TraceResolved("IsVR");
            return IsXrDeviceActiveFallback()
                   || (_porting != null && _porting.CurrentValue == AppMode.XR);
        }
    }

    static void TraceResolved(string source)
    {
        if (_resolvedTraced)
            return;

        _resolvedTraced = true;
        bool device = IsXrDeviceActiveFallback();
        bool vr = device || (_porting != null && _porting.CurrentValue == AppMode.XR);
        Debug.Log(
            "[FireTrace] FirePlatformRuntime resolved=" + (vr ? "XR" : "PC") +
            " source=" + source +
            " porting=" + (_porting == null ? "<null>" : _porting.CurrentValue.ToString()) +
            " deviceFallback=" + device.ToString().ToLowerInvariant() +
            " rawHubPlatform=" + RawHubPlatform);
    }

    public static bool IsXrDeviceActiveFallback()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return UnityEngine.XR.XRSettings.isDeviceActive;
#else
        return false;
#endif
    }

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
