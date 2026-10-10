using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Management;

/// <summary>
/// Editor Play boots the Standalone OpenXR loader before gameplay.
/// PC mode stops that loader. VR mode (selector or Hub) keeps or starts it.
/// This assembly is Editor-only, so Android Quest players never run it.
/// </summary>
[InitializeOnLoad]
static class HazardEditorStandalonePc
{
    static HazardEditorStandalonePc()
    {
        StandalonePlatform.EditorXrLoaderSync = Sync;
    }

    static void Sync(bool vr)
    {
        XRManagerSettings manager = XRGeneralSettings.Instance != null
            ? XRGeneralSettings.Instance.Manager
            : null;
        if (manager == null)
            return;

        if (vr)
        {
            if (manager.activeLoader == null)
                manager.InitializeLoaderSync();
            if (manager.activeLoader != null)
                manager.StartSubsystems();

            Debug.Log("[StandalonePlatform] Editor OpenXR loader running.");
            return;
        }

        if (manager.activeLoader == null || !manager.isInitializationComplete)
            return;

        manager.DeinitializeLoader();
        Debug.Log("[StandalonePlatform] Editor OpenXR loader stopped.");
    }
}
