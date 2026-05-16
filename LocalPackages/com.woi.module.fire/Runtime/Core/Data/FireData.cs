using UnityEngine;

namespace FireExtinguisher.Core
{
    /// <summary>
    /// Defines the properties and extinguisher compatibility rules for a fire type.
    /// Create one asset per fire variant via
    /// <b>Assets → Create → Fire Extinguisher → Fire Data</b>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewFireData",
        menuName = "Fire Extinguisher/Fire Data",
        order = 1)]
    public sealed class FireData : ScriptableObject
    {
        [Header("Classification")]
        [Tooltip("The European fire class (EN 2) that describes this fire's fuel type.")]
        [SerializeField] private FireClass _fireClass = FireClass.A;

        [Header("Intensity")]
        [Tooltip("Maximum intensity value for this fire (arbitrary unit). " +
                 "Used to scale suppression and visual feedback.")]
        [SerializeField, Min(0.1f)] private float _maxIntensity = 1f;

        [Tooltip("Multiplier applied to incoming extinguish power. " +
                 "Values above 1 make the fire harder to suppress; " +
                 "values below 1 make it easier.")]
        [SerializeField, Min(0f)] private float _extinguishResistance = 1f;

        [Header("Compatibility")]
        [Tooltip("Extinguisher types that are effective against this fire. " +
                 "Used by the compatibility evaluator to grant full effectiveness.")]
        [SerializeField] private ExtinguisherType[] _allowedExtinguisherTypes = { ExtinguisherType.DryPowder };

        [Tooltip("Extinguisher types that must never be used on this fire. " +
                 "Used by the compatibility evaluator to block or penalise application. " +
                 "Leave empty if there are no forbidden types.")]
        [SerializeField] private ExtinguisherType[] _forbiddenExtinguisherTypes = { };

        /// <summary>The European fire class that describes this fire's fuel type.</summary>
        public FireClass FireClass => _fireClass;

        /// <summary>
        /// Maximum intensity for this fire.
        /// Fire zones are initialised within this range.
        /// </summary>
        public float MaxIntensity => _maxIntensity;

        /// <summary>
        /// Multiplier applied to incoming extinguish power.
        /// A value of 2 means the fire requires twice the suppression to extinguish.
        /// </summary>
        public float ExtinguishResistance => _extinguishResistance;

        /// <summary>Extinguisher types that are rated effective against this fire.</summary>
        public ExtinguisherType[] AllowedExtinguisherTypes => _allowedExtinguisherTypes;

        /// <summary>
        /// Extinguisher types that are dangerous or ineffective on this fire.
        /// Implementations should check this before applying suppression.
        /// </summary>
        public ExtinguisherType[] ForbiddenExtinguisherTypes => _forbiddenExtinguisherTypes;
    }
}
