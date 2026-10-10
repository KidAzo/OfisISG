using System;
using UnityEngine;

/// <summary>
/// One platform decision for Hazard Hunt and Waste Collector.
/// Priority: Hub/Bridge explicit platform, then Quest runtime, then <see cref="StandalonePlatformSettings"/>, then PC.
/// Does not change Fire training. Fire keeps reading <see cref="FirePlatformRuntime"/> on its own.
/// </summary>
public static class StandalonePlatform
{
    public const string ResourceName = "StandalonePlatformMode";

    static bool _hubActive;
    static bool _hubIsVr;
    static bool _loggedStandalone;
    static StandalonePlatformSettings _settings;

    /// <summary>
    /// Editor-only. The Editor assembly assigns this so player builds do not reference XR Management.
    /// True keeps or starts the OpenXR loader. False stops it.
    /// </summary>
    public static Action<bool> EditorXrLoaderSync;

    public static bool HasHubOverride => _hubActive;

    public static AppMode SelectorMode
    {
        get
        {
            StandalonePlatformSettings settings = Settings;
            if (settings == null)
                return AppMode.PC;
            return settings.Mode;
        }
    }

    /// <summary>Active mode for login scenes that do not know which module booted them.</summary>
    public static bool IsVrActive => UseVr(_hubActive, _hubIsVr);

    public static bool UseVr(bool hubLaunch, bool hubIsVr)
    {
        return Resolve(hubLaunch, hubIsVr) == AppMode.XR;
    }

    public static AppMode Resolve(bool hubLaunch, bool hubIsVr)
    {
        if (hubLaunch)
            return hubIsVr ? AppMode.XR : AppMode.PC;

        if (FirePlatformRuntime.IsXrDeviceActiveFallback())
            return AppMode.XR;

        return SelectorMode;
    }

    public static string Describe(bool hubLaunch, bool hubIsVr)
    {
        if (hubLaunch)
            return hubIsVr ? "hub:VR" : "hub:PC";
        if (FirePlatformRuntime.IsXrDeviceActiveFallback())
            return "quest:VR";
        return SelectorMode == AppMode.XR ? "selector:VR" : "selector:PC";
    }

    /// <summary>
    /// Clears a Hub override left from an earlier Play Mode session when domain reload is off.
    /// A Hub <see cref="NotifyHubLaunch"/> in the current session still wins after this.
    /// </summary>
    public static void ClearHubOverride()
    {
        _hubActive = false;
        _hubIsVr = false;
        _loggedStandalone = false;
    }

    /// <summary>Hub/Bridge wins. Later calls replace the previous module override.</summary>
    public static void NotifyHubLaunch(bool hubIsVr)
    {
        _hubActive = true;
        _hubIsVr = hubIsVr;
        AppMode mode = hubIsVr ? AppMode.XR : AppMode.PC;
        ApplyToLoadedPortingAssets(mode);
        SyncEditorXrLoader(hubIsVr);
        Debug.Log("[StandalonePlatform] " + Describe(true, hubIsVr) + " appMode=" + mode + ".");
    }

    /// <summary>
    /// Selector or Quest when no Hub override has been recorded.
    /// Also pushes <see cref="AppMode"/> onto loaded porting assets so shared input follows the same mode.
    /// </summary>
    public static void ApplyIfNoHubOverride()
    {
        if (_hubActive)
            return;

        AppMode mode = Resolve(false, false);
        ApplyToLoadedPortingAssets(mode);
        SyncEditorXrLoader(mode == AppMode.XR);

        if (_loggedStandalone)
            return;

        _loggedStandalone = true;
        Debug.Log(
            "[StandalonePlatform] " + Describe(false, false) +
            " appMode=" + mode +
            ". Edit StandalonePlatformMode (PC / VR). Hub overrides this.");
    }

    public static void ApplyToLoadedPortingAssets(AppMode mode)
    {
#if UNITY_EDITOR
        const string PortingVariablePath = "Packages/com.woi.module.fire/Runtime/Porting/PortingVariable.asset";
        ScriptableEnumPortingVariable editorPorting =
            UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableEnumPortingVariable>(PortingVariablePath);
        if (editorPorting != null)
            FirePlatformRuntime.TryInitialize(editorPorting);
#endif
        ScriptableEnumPortingVariable[] assets = Resources.FindObjectsOfTypeAll<ScriptableEnumPortingVariable>();
        int updated = 0;
        for (int i = 0; i < assets.Length; i++)
        {
            ScriptableEnumPortingVariable asset = assets[i];
            if (asset == null || asset.CurrentValue == mode)
                continue;

            asset.SetCurrentValue(mode);
            updated++;
        }

        if (updated > 0)
        {
            Debug.Log(
                "[StandalonePlatform] porting appMode=" + mode +
                " assets=" + assets.Length + " updated=" + updated + ".");
        }
    }

    public static void SyncEditorXrLoader(bool vr)
    {
#if UNITY_EDITOR
        EditorXrLoaderSync?.Invoke(vr);
#else
        _ = vr;
#endif
    }

    static StandalonePlatformSettings Settings
    {
        get
        {
            if (_settings == null)
                _settings = Resources.Load<StandalonePlatformSettings>(ResourceName);
            return _settings;
        }
    }
}
