using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace FireExtinguisher.Core
{
    /// <summary>
    /// Defines the physical and gameplay properties of a fire extinguisher type.
    /// Create one asset per extinguisher variant via
    /// <b>Assets → Create → Fire Extinguisher → Extinguisher Data</b>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewExtinguisherData",
        menuName = "Fire Extinguisher/Extinguisher Data",
        order = 0)]
    public sealed class ExtinguisherData : ScriptableObject
    {
        public const string LanguageTurkish = "tr";
        public const string LanguageEnglish = "en";

        [Header("Display")]
        [Tooltip("Türkçe HUD / liste adı (ör. ABC Tozlu 6 kg).")]
        [SerializeField] private string _nameTr = string.Empty;

        [Tooltip("İngilizce HUD / liste adı.")]
        [FormerlySerializedAs("_name")]
        [SerializeField] private string _nameEn = string.Empty;

        [Tooltip("Başlık altı ikinci satır — Türkçe (ör. ajan + kapasite).")]
        [SerializeField] private string _subtitleTr = string.Empty;

        [Tooltip("Başlık altı ikinci satır — İngilizce.")]
        [SerializeField] private string _subtitleEn = string.Empty;

        [Header("Agent")]
        [Tooltip("The type of extinguishing agent in this extinguisher.")]
        [SerializeField] private ExtinguisherType _extinguisherType = ExtinguisherType.DryPowder;

        [Tooltip("Fire classes this extinguisher is rated to combat.")]
        [SerializeField] private FireClass[] _supportedFireClasses = { FireClass.A, FireClass.B, FireClass.C };

        [Header("Spray Geometry")]
        [Tooltip("Maximum effective range of the spray in metres.")]
        [SerializeField, Min(0f)] private float _maxRange = 4f;

        [Tooltip("Radius of the spray cone at maximum range, in metres.")]
        [SerializeField, Min(0f)] private float _sprayRadius = 0.6f;

        [Tooltip("Full spread angle of the spray cone in degrees.\n" +
                 "The effective half-angle (centre to edge) = ConeAngleDegrees / 2.\n" +
                 "Example: 24° → hits within 12° of centre register.")]
        [SerializeField, Range(1f, 90f)] private float _coneAngleDegrees = 24f;

        [Header("Effectiveness")]
        [Tooltip("Base suppression applied to a fire zone per second at optimal distance (0–1).")]
        [SerializeField, Range(0f, 1f)] private float _extinguishPower = 0.35f;

        [Tooltip("Agent consumed per second of continuous discharge, in the same units as Max Capacity on ExtinguisherController.\n" +
                 "Duration = MaxCapacity / ConsumptionRate.\n" +
                 "Example: MaxCapacity=6, ConsumptionRate=1 → 6 seconds.")]
        [SerializeField, Min(0.01f)] private float _consumptionRate = 1f;

        [Header("Optimal Distance")]
        [Tooltip("Minimum distance in metres for full effectiveness.")]
        [SerializeField, Min(0f)] private float _optimalDistanceMin = 1f;

        [Tooltip("Maximum distance in metres for full effectiveness. Beyond this the power falls off.")]
        [SerializeField, Min(0f)] private float _optimalDistanceMax = 3f;

        [Tooltip("Controls the steepness of the distance falloff curve on both ends.\n\n" +
                 "Applies a power curve to the normalised distance before lerping:\n" +
                 "   curvedT = Pow(t, 1 / rangeFalloffAmount)\n\n" +
                 "1.0  = linear falloff (default)\n" +
                 "2.0  = steeper — power drops faster away from optimal range\n" +
                 "0.8  = gentler — power stays stronger across a wider distance band")]
        [SerializeField, Range(0.1f, 4f)] private float _rangeFalloffAmount = 1f;

        /// <summary>
        /// <paramref name="languageCode"/> için gösterim adı (ör. <c>tr</c>, <c>en</c>).
        /// Eksik dilde diğer dil yedeği; ikisi de boşsa boş döner (PickupItem yedeği devreye girer).
        /// </summary>
        public string GetDisplayName(string languageCode) =>
            PickLocalizedPair(_nameTr, _nameEn, languageCode);

        /// <summary>Başlık altı ikinci satır; aynı dil / yedek kuralları <see cref="GetDisplayName"/> ile.</summary>
        public string GetSubtitle(string languageCode) =>
            PickLocalizedPair(_subtitleTr, _subtitleEn, languageCode);

        private static string PickLocalizedPair(string rawTr, string rawEn, string languageCode)
        {
            string tr = string.IsNullOrWhiteSpace(rawTr) ? string.Empty : rawTr.Trim();
            string en = string.IsNullOrWhiteSpace(rawEn) ? string.Empty : rawEn.Trim();

            static bool Has(string s) => !string.IsNullOrWhiteSpace(s);

            if (string.IsNullOrWhiteSpace(languageCode))
                return Has(en) ? en : tr;

            string c = languageCode.Trim().ToLowerInvariant();
            bool preferTr = c == LanguageTurkish
                || c.StartsWith("tr-", System.StringComparison.Ordinal)
                || c.StartsWith("tr_", System.StringComparison.Ordinal);

            if (preferTr)
            {
                if (Has(tr))
                    return tr;
                if (Has(en))
                    return en;
                return string.Empty;
            }

            if (Has(en))
                return en;
            if (Has(tr))
                return tr;
            return string.Empty;
        }

        /// <summary>The type of extinguishing agent in this extinguisher.</summary>
        public ExtinguisherType ExtinguisherType => _extinguisherType;

        /// <summary>Fire classes this extinguisher is rated to combat.</summary>
        public FireClass[] SupportedFireClasses => _supportedFireClasses;

        /// <summary>
        /// Returns <c>true</c> when this extinguisher is rated to combat the given fire class.
        /// Returns <c>false</c> and logs a warning when no supported classes are configured.
        /// </summary>
        public bool CanExtinguish(FireClass fireClass)
        {
            if (_supportedFireClasses == null || _supportedFireClasses.Length == 0)
            {
                Debug.LogWarning(
                    $"[ExtinguisherData] '{name}' has no supported fire classes assigned. " +
                    "Suppression blocked until classes are configured.", this);
                return false;
            }

            return _supportedFireClasses.Contains(fireClass);
        }

        /// <summary>Maximum effective range of the spray in metres.</summary>
        public float MaxRange => _maxRange;

        /// <summary>Radius of the spray cone at maximum range, in metres.</summary>
        public float SprayRadius => _sprayRadius;

        /// <summary>
        /// Full spread angle of the spray cone in degrees.
        /// The evaluator uses <c>ConeAngleDegrees × 0.5</c> as the boundary half-angle.
        /// </summary>
        public float ConeAngleDegrees => _coneAngleDegrees;

        /// <summary>
        /// Base suppression applied to a fire zone per second at optimal distance.
        /// Value is in the 0–1 range where 1 extinguishes a full-intensity zone in one second.
        /// </summary>
        public float ExtinguishPower => _extinguishPower;

        /// <summary>
        /// Agent consumed per second of continuous discharge, in the same units as
        /// <c>ExtinguisherController.MaxCapacity</c>.
        /// Duration = MaxCapacity / ConsumptionRate.
        /// </summary>
        public float ConsumptionRate => _consumptionRate;

        /// <summary>Minimum distance in metres for full effectiveness.</summary>
        public float OptimalDistanceMin => _optimalDistanceMin;

        /// <summary>Maximum distance in metres at which full effectiveness applies.</summary>
        public float OptimalDistanceMax => _optimalDistanceMax;

        /// <summary>
        /// Power-curve exponent for the distance falloff on both ends.
        /// Applied as <c>Pow(t, 1 / RangeFalloffAmount)</c> before lerping.
        /// 1 = linear. &gt;1 = steeper drop. &lt;1 = gentler drop.
        /// </summary>
        public float RangeFalloffAmount => _rangeFalloffAmount;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_optimalDistanceMin > _optimalDistanceMax)
                _optimalDistanceMin = _optimalDistanceMax;

            if (_sprayRadius > _maxRange)
                _sprayRadius = _maxRange;
        }
#endif
    }
}
