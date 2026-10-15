using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

/// <summary>
/// Editor-only bridge for StandalonePlatform PC/VR selector.
/// VR: observe XR state only — Unity XR Management owns OpenXR startup.
/// PC: disable automatic XR init in Play mode.
/// </summary>
[InitializeOnLoad]
static class HazardEditorStandalonePc
{
    const float PlayEnterLogDelaySeconds = 2f;

    static bool _playEnterLogScheduled;
    static double _playEnterLogAfterTime;

    static HazardEditorStandalonePc()
    {
        StandalonePlatform.EditorXrLoaderSync = Sync;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ConfigureEditorOpenXrLifecycle()
    {
        XRGeneralSettings settings = XRGeneralSettings.Instance;
        if (settings == null)
        {
            Debug.LogWarning("[StandalonePlatform] XRGeneralSettings was not ready at SubsystemRegistration.");
            return;
        }

        bool vr = StandalonePlatform.Resolve(false, false) == AppMode.XR;
        if (vr)
        {
            Debug.Log(
                "[StandalonePlatform] Editor VR using Unity XR Management auto-start " +
                "(InitManagerOnStart left enabled; no manual loader calls from Sync).");
            return;
        }

        settings.InitManagerOnStart = false;
        Debug.Log("[StandalonePlatform] Editor PC disabled XR InitManagerOnStart for this Play session.");
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _playEnterLogScheduled = false;
            if (StandalonePlatform.Resolve(false, false) == AppMode.XR)
            {
                _playEnterLogScheduled = true;
                _playEnterLogAfterTime = EditorApplication.timeSinceStartup + PlayEnterLogDelaySeconds;
                EditorApplication.update += OneShotPlayEnterXrLog;
            }

            return;
        }

        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.update -= OneShotPlayEnterXrLog;
    }

    static void OneShotPlayEnterXrLog()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= OneShotPlayEnterXrLog;
            return;
        }

        if (EditorApplication.timeSinceStartup < _playEnterLogAfterTime)
            return;

        EditorApplication.update -= OneShotPlayEnterXrLog;
        if (!_playEnterLogScheduled)
            return;

        _playEnterLogScheduled = false;
        LogEditorXrState("EditorXrPlayEnter");
    }

    static void GetSubsystemCounts(
        out int displayCount,
        out int runningDisplayCount,
        out int inputCount,
        out int runningInputCount)
    {
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        displayCount = displays.Count;
        runningDisplayCount = 0;
        for (int i = 0; i < displays.Count; i++)
        {
            if (displays[i] != null && displays[i].running)
                runningDisplayCount++;
        }

        var inputs = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(inputs);
        inputCount = inputs.Count;
        runningInputCount = 0;
        for (int i = 0; i < inputs.Count; i++)
        {
            if (inputs[i] != null && inputs[i].running)
                runningInputCount++;
        }
    }

    static void LogEditorXrState(string tag)
    {
        XRManagerSettings manager = XRGeneralSettings.Instance != null
            ? XRGeneralSettings.Instance.Manager
            : null;

        string activeLoader = manager != null && manager.activeLoader != null
            ? manager.activeLoader.name
            : "<null>";
        bool initComplete = manager != null && manager.isInitializationComplete;
        GetSubsystemCounts(
            out int displayCount,
            out int runningDisplayCount,
            out int inputCount,
            out int runningInputCount);

        Debug.Log(
            "[StandalonePlatform] " + tag + "\n" +
            "activeLoader=" + activeLoader + "\n" +
            "isInitializationComplete=" + initComplete + "\n" +
            "displayCount=" + displayCount + "\n" +
            "runningDisplayCount=" + runningDisplayCount + "\n" +
            "inputCount=" + inputCount + "\n" +
            "runningInputCount=" + runningInputCount + "\n" +
            "xrDeviceActive=" + XRSettings.isDeviceActive);
    }

    static void Sync(bool vr)
    {
        if (vr)
        {
            LogEditorXrState("EditorXrSyncObserve");
            return;
        }

        LogEditorXrState("EditorPcSyncObserve");
    }
}
