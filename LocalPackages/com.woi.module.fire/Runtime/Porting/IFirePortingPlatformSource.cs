/// <summary>
/// Minimal read surface for the existing porting ScriptableObject. Use <see cref="ExistingPortingSettingsPlatformProvider"/> as the default implementation.
/// </summary>
public interface IFirePortingPlatformSource
{
    AppMode CurrentMode { get; }
}
