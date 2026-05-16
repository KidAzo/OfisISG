using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Activates PC-only or XR-only object roots based on <see cref="FirePlatformRuntime"/> / existing porting SO.
/// Does not destroy objects and does not touch Addressables or scene loading.
/// </summary>
[DisallowMultipleComponent]
public sealed class FirePlatformSwitcher : MonoBehaviour
{
    [SerializeField] List<GameObject> pcOnlyObjects = new List<GameObject>();
    [SerializeField] List<GameObject> vrOnlyObjects = new List<GameObject>();
    [SerializeField] List<GameObject> sharedObjects = new List<GameObject>();
    [SerializeField] bool applyOnAwake = true;
    [SerializeField] bool logAppliedObjects;
    [SerializeField] AppMode fallbackMode = AppMode.PC;

    void Awake()
    {
        if (applyOnAwake)
            ApplyPlatformActiveState();
    }

    [ContextMenu("Apply Now")]
    public void ApplyNow()
    {
        ApplyPlatformActiveState();
    }

    public void ApplyPlatformActiveState()
    {
        AppMode mode = ResolveMode();

        bool pcActive = mode == AppMode.PC;
        bool vrActive = mode == AppMode.XR;

        SetListActive(pcOnlyObjects, pcActive);
        SetListActive(vrOnlyObjects, vrActive);
        SetListActive(sharedObjects, true);

        if (logAppliedObjects)
        {
            Debug.Log(
                $"[FirePlatformSwitcher] Applied AppMode={mode} (PC objects active={pcActive}, VR objects active={vrActive}).",
                this);
        }
    }

    AppMode ResolveMode()
    {
        if (FirePlatformRuntime.IsSourceInitialized)
            return FirePlatformRuntime.CurrentMode;

        Debug.LogWarning(
            "[FirePlatformSwitcher] FirePlatformRuntime has no porting source yet — using serialized fallback AppMode. " +
            "Ensure FirePlatformRuntime.TryInitialize runs from bootstrap / LoadingScreenController with the same PortingVariable asset.",
            this);
        return fallbackMode;
    }

    static void SetListActive(List<GameObject> list, bool active)
    {
        if (list == null)
            return;

        for (int i = 0; i < list.Count; i++)
        {
            GameObject go = list[i];
            if (go != null)
                go.SetActive(active);
        }
    }
}
