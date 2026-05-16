namespace FireExtinguisher.Core
{
    /// <summary>
    /// Identifies the physical role of a <see cref="FireTargetZone"/> within its
    /// parent <see cref="FireSource"/>.
    /// </summary>
    /// <remarks>
    /// Used by presentation layers and external consumers to distinguish zones
    /// without hard-coding string names. Add new values here as needed;
    /// existing values must not be renumbered to preserve serialized data.
    /// </remarks>
    public enum FireZoneType
    {
        /// <summary>
        /// The base of the fire, closest to the fuel source.
        /// Typically the primary suppression target.
        /// </summary>
        Base = 0,

        /// <summary>
        /// The upper flame body.
        /// Suppressing only this zone is usually cosmetic; the base must also be addressed.
        /// </summary>
        Upper = 1,

        /// <summary>
        /// A user-defined zone type for project-specific needs.
        /// </summary>
        Custom = 2,
    }
}
