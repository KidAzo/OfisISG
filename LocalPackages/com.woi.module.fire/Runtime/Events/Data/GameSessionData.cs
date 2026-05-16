using System.Collections.Generic;
using FireExtinguisher.Core;

namespace Woi.Events.Data
{
    /// <summary>
    /// Static, scene-independent session context written once by <c>GameInitializer</c>
    /// immediately before the training scene is loaded.
    /// <para>
    /// Unlike <see cref="SessionDataSO"/>, this class requires no Inspector assignment and
    /// carries no Unity event subscriptions — it is always readable by any script at any point
    /// after <see cref="Set"/> is called, regardless of scene-load order.
    /// </para>
    /// </summary>
    public static class GameSessionData
    {
        /// <summary>Fire classes selected by the user on the login screen.</summary>
        public static List<FireClass> SelectedClasses { get; private set; } = new List<FireClass>();

        /// <summary>Display name entered by the user.</summary>
        public static string UserName { get; private set; } = string.Empty;

        /// <summary>User ID entered by the user.</summary>
        public static string UserId { get; private set; } = string.Empty;

        /// <summary>True once <see cref="Set"/> has been called at least once this play session.</summary>
        public static bool IsSet { get; private set; }

        // ── Write ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Stores session data. Call this from <c>GameInitializer</c> before loading the
        /// training scene so that all training-scene scripts can read it in <c>Start()</c>.
        /// </summary>
        public static void Set(List<FireClass> selectedClasses, string userName, string userId)
        {
            SelectedClasses = selectedClasses != null
                ? new List<FireClass>(selectedClasses)
                : new List<FireClass>();

            UserName = userName  ?? string.Empty;
            UserId   = userId    ?? string.Empty;
            IsSet    = true;
        }

        /// <summary>Resets all fields. Call when returning to the main menu / starting a fresh session.</summary>
        public static void Clear()
        {
            SelectedClasses = new List<FireClass>();
            UserName = string.Empty;
            UserId   = string.Empty;
            IsSet    = false;
        }
    }
}
