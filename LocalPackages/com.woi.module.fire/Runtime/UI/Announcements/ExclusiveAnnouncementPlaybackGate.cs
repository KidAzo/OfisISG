namespace Woi.UI.Announcements
{
    /// <summary>
    /// Scene intro / non–AnnouncementService playback can push this gate so
    /// <see cref="AnnouncementService"/> ignores new <c>Play</c> calls until the intro audio finishes.
    /// </summary>
    public static class ExclusiveAnnouncementPlaybackGate
    {
        private static int _depth;

        public static bool IsBlocking => _depth > 0;

        public static void Enter()
        {
            _depth++;
        }

        public static void Exit()
        {
            if (_depth > 0)
                _depth--;
        }
    }
}
