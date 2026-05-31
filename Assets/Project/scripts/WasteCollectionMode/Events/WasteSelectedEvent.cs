namespace Woi.Events
{
    /// <summary>
    /// Raised when the player selects a waste item in waste collection mode.
    /// </summary>
    public readonly struct WasteSelectedEvent
    {
        public string Name { get; }

        public WasteSelectedEvent(string name)
        {
            Name = name ?? string.Empty;
        }
    }
}
