namespace FireExtinguisher.Core
{
    /// <summary>
    /// Describes how well a given <see cref="ExtinguisherType"/> matches a fire's
    /// <see cref="FireClass"/>, as declared in <see cref="FireData"/>.
    /// </summary>
    /// <remarks>
    /// Returned by <see cref="FireSource.CheckCompatibility"/>.
    /// External systems (VFX, audio, training) can branch on this value
    /// without implementing any compatibility logic themselves.
    /// </remarks>
    public enum CompatibilityResult
    {
        /// <summary>
        /// The extinguisher type is listed in <see cref="FireData.AllowedExtinguisherTypes"/>.
        /// Full suppression power applies.
        /// </summary>
        Effective = 0,

        /// <summary>
        /// The extinguisher type is neither allowed nor forbidden.
        /// Suppression is applied at reduced or zero effectiveness (caller's choice).
        /// </summary>
        Neutral = 1,

        /// <summary>
        /// The extinguisher type is listed in <see cref="FireData.ForbiddenExtinguisherTypes"/>.
        /// Suppression must not be applied; external systems may treat this as a hazard.
        /// </summary>
        Forbidden = 2,
    }
}
