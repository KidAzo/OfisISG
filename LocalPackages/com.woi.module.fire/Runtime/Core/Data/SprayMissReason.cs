namespace FireExtinguisher.Core
{
    /// <summary>
    /// Describes why a spray evaluation did not result in a hit on a
    /// <see cref="FireTargetZone"/>.
    /// </summary>
    /// <remarks>
    /// Set to <see cref="None"/> in a successful <see cref="ExtinguishResult"/>.
    /// External systems can branch on this value for feedback, audio, or debug output
    /// without implementing any detection logic themselves.
    /// </remarks>
    public enum SprayMissReason
    {
        /// <summary>No miss. The spray hit a valid, active zone.</summary>
        None = 0,

        /// <summary>
        /// The SphereCast found no collider within <c>ExtinguisherData.MaxRange</c>.
        /// </summary>
        OutOfRange = 1,

        /// <summary>
        /// A collider was hit within range, but it carries no
        /// <see cref="FireTargetZone"/> component.
        /// </summary>
        NoFireZoneHit = 2,

        /// <summary>
        /// A <see cref="FireTargetZone"/> collider was hit, but the angle between
        /// the spray direction and the direction to the zone centre exceeds
        /// <c>ExtinguisherData.ConeAngleDegrees</c>.
        /// </summary>
        OutsideConeAngle = 3,

        /// <summary>
        /// The zone was hit but its intensity has already reached zero.
        /// </summary>
        ZoneAlreadyExtinguished = 4,

        /// <summary>
        /// The owning <see cref="FireSource"/> is already in the
        /// <see cref="FireSourceState.Extinguished"/> state.
        /// </summary>
        FireAlreadyExtinguished = 5,
    }
}
