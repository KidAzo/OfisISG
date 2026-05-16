namespace Woi.UI.Announcements
{
    /// <summary>
    /// How <see cref="ExtinguisherHoverController"/> detects “hover”.
    /// </summary>
    public enum HoverPointerMode
    {
        /// <summary>Unity’s OnMouseEnter/Exit — uses <b>mouse position</b> from the main camera (needs a cursor).</summary>
        UnityMouseOverCollider = 0,

        /// <summary>No mouse: add <see cref="ExtinguisherHoverRaycaster"/> on the camera — ray from viewport through screen center (crosshair).</summary>
        CameraCenterRay = 1
    }
}
