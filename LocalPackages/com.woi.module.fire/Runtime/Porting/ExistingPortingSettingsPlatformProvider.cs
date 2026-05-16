using UnityEngine;

/// <summary>
/// Adapter around <see cref="ScriptableEnumPortingVariable"/> for code that prefers an interface over the static runtime.
/// </summary>
public sealed class ExistingPortingSettingsPlatformProvider : IFirePortingPlatformSource
{
    readonly ScriptableEnumPortingVariable _porting;

    public ExistingPortingSettingsPlatformProvider(ScriptableEnumPortingVariable portingVariable)
    {
        _porting = portingVariable;
    }

    public AppMode CurrentMode
    {
        get
        {
            if (_porting != null)
                return _porting.CurrentValue;

            Debug.LogWarning(
                "[ExistingPortingSettingsPlatformProvider] ScriptableEnumPortingVariable is null — returning AppMode.PC.");
            return AppMode.PC;
        }
    }
}
