namespace Woi.Events
{
    /// <summary>
    /// Raised when a waste item collection feedback finishes (scale tween complete).
    /// Use to block input and show the result UI.
    /// </summary>
    public readonly struct WasteCollectedEvent
    {
        public string WasteName { get; }

        public int TotalCollected { get; }

        public WasteCollectedEvent(string wasteName, int totalCollected)
        {
            WasteName = wasteName ?? string.Empty;
            TotalCollected = totalCollected;
        }
    }
}
