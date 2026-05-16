namespace FireExtinguisher.Core
{
    /// <summary>
    /// European fire classification standard (EN 2).
    /// Identifies the type of fuel involved in a fire.
    /// Used to validate extinguisher compatibility via <see cref="FireData"/>.
    /// </summary>
    public enum FireClass
    {
        /// <summary>Solid combustible materials: wood, paper, textiles, plastics.</summary>
        A = 0,

        /// <summary>Flammable liquids and liquefiable solids: petrol, oil, paint.</summary>
        B = 1,

        /// <summary>Flammable gases: propane, butane, methane.</summary>
        C = 2,

        /// <summary>Combustible metals: magnesium, sodium, potassium.</summary>
        D = 3,

        /// <summary>Cooking oils and fats in commercial fryers.</summary>
        F = 4,

        /// <summary>Electrical equipment fires (Class E where applicable).</summary>
        E = 5,
    }
}
