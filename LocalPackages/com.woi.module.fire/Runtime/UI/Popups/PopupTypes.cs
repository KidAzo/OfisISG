using UnityEngine;
using Woi.UI.Popups.Localization;

namespace Woi.UI.Popups
{
    public enum PopupType
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Success = 3,
        Training = 4
    }

    public enum PopupAnchor
    {
        TopRight = 0,
        TopCenter = 1,
        BottomCenter = 2,
        Center = 3
    }

    /// <summary>When a popup is visible and a new one requests no-replace mode.</summary>
    public enum PopupOverflowBehavior
    {
        /// <summary>Drop the new request.</summary>
        IgnoreNew = 0,

        /// <summary>Enqueue and show after the current popup fully closes.</summary>
        QueueNext = 1
    }
}
