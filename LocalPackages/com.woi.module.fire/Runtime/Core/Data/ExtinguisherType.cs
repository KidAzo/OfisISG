namespace FireExtinguisher.Core
{
    /// <summary>
    /// Identifies the extinguishing agent used by a fire extinguisher.
    /// Used to determine compatibility with a given <see cref="FireClass"/>.
    /// </summary>
    public enum ExtinguisherType
    {
        /// <summary>Water. Effective on Class A. Dangerous on Class B, C, and F.</summary>
        Water = 0,

        /// <summary>Aqueous Film-Forming Foam. Effective on Class A and B.</summary>
        Foam = 1,

        /// <summary>Dry powder (ABC). Effective on Class A, B, and C.</summary>
        DryPowder = 2,

        /// <summary>Carbon dioxide. Effective on Class B and C. Leaves no residue.</summary>
        CO2 = 3,

        /// <summary>Wet chemical. Effective on Class F (cooking oils and fats).</summary>
        WetChemical = 4,

        /// <summary>Metal powder. Effective on Class D.</summary>
        MetalPowder = 5, 
    }
}
