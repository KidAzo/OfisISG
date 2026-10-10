using UnityEngine;

/// <summary>
/// Standalone and Editor platform for Hazard Hunt and Waste Collector when Hub/Bridge did not set one.
/// <see cref="AppMode.XR"/> is VR. Default is <see cref="AppMode.PC"/>.
/// </summary>
[CreateAssetMenu(fileName = "StandalonePlatformMode", menuName = "Woi/Standalone Platform Mode")]
public sealed class StandalonePlatformSettings : ScriptableObject
{
    [Tooltip("PC or VR (AppMode.XR). Hub/Bridge overrides this. Quest stays VR when Hub did not set a platform.")]
    [SerializeField] AppMode mode = AppMode.PC;

    public AppMode Mode => mode == AppMode.XR ? AppMode.XR : AppMode.PC;
}
