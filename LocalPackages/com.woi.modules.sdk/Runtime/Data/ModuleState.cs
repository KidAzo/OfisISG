namespace WOI.Modules.SDK.Data
{
    /// <summary>
    /// Installation and runtime lifecycle state of a module in the Hub.
    /// </summary>
    public enum ModuleState
    {
        NotInstalled,
        Downloading,
        Installed,
        UpdateAvailable,
        Updating,
        Running,
        Error
    }
}
