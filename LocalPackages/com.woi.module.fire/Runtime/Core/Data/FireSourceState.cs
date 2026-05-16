namespace FireExtinguisher.Core
{
    /// <summary>
    /// Represents the lifecycle state of a <see cref="FireSource"/>.
    /// </summary>
    /// <remarks>
    /// State transitions are managed exclusively by <see cref="FireSource"/>.
    /// External systems should read this value and react; they must not write it.
    /// </remarks>
    public enum FireSourceState
    {
        /// <summary>
        /// The fire is burning above the suppressed threshold.
        /// </summary>
        Active = 0,

        /// <summary>
        /// The fire's normalised intensity has fallen below the suppressed threshold
        /// but has not yet reached zero. The fire is being actively controlled.
        /// </summary>
        Suppressed = 1,

        /// <summary>
        /// All zones have reached zero intensity. The fire is out.
        /// </summary>
        Extinguished = 2,

        /// <summary>
        /// The fire was extinguished but is growing again because the fuel
        /// source was not eliminated. Reignition must be enabled on <see cref="FireSource"/>.
        /// </summary>
        Reigniting = 3,
    }
}
