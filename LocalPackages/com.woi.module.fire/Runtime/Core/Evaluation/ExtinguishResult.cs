using UnityEngine;

namespace FireExtinguisher.Core
{
    /// <summary>
    /// Immutable result produced by <see cref="ExtinguishEvaluator.Evaluate"/> for a single
    /// spray tick. Contains all data needed by presentation layers, audio systems,
    /// and external consumer logic without those systems needing to re-query the scene.
    /// </summary>
    /// <remarks>
    /// Use the static factory methods <see cref="Hit"/> and <see cref="Miss"/> to construct
    /// instances. Direct construction via the default constructor yields an uninitialized
    /// miss with no meaningful data.
    /// </remarks>
    public readonly struct ExtinguishResult
    {
        // ── Hit/miss summary ─────────────────────────────────────────────────────

        /// <summary>
        /// <c>true</c> when the spray successfully struck a live <see cref="FireTargetZone"/>
        /// within cone and range. When <c>false</c>, inspect <see cref="MissReason"/>.
        /// </summary>
        public bool DidHitZone { get; }

        /// <summary>
        /// The reason the spray did not connect. <see cref="SprayMissReason.None"/>
        /// when <see cref="DidHitZone"/> is <c>true</c>.
        /// </summary>
        public SprayMissReason MissReason { get; }

        // ── Hit geometry ─────────────────────────────────────────────────────────

        /// <summary>
        /// The zone that was struck. <c>null</c> when <see cref="DidHitZone"/> is <c>false</c>.
        /// </summary>
        public FireTargetZone HitZone { get; }

        /// <summary>
        /// The <see cref="FireSource"/> that owns <see cref="HitZone"/>.
        /// <c>null</c> when <see cref="DidHitZone"/> is <c>false</c>.
        /// </summary>
        public FireSource Source { get; }

        /// <summary>
        /// World-space contact point from the SphereCast.
        /// <see cref="Vector3.zero"/> when <see cref="DidHitZone"/> is <c>false</c>.
        /// </summary>
        public Vector3 HitPoint { get; }

        /// <summary>
        /// Distance in metres from the spray origin to <see cref="HitPoint"/>.
        /// Zero when <see cref="DidHitZone"/> is <c>false</c>.
        /// </summary>
        public float Distance { get; }

        // ── Angular data ──────────────────────────────────────────────────────────

        /// <summary>
        /// Angle in degrees between the spray direction and the direction from the
        /// spray origin to <see cref="HitZone"/>'s transform position.
        /// Zero when <see cref="DidHitZone"/> is <c>false</c>.
        /// </summary>
        public float AngleFromCenter { get; }

        /// <summary>
        /// Normalised score in [0, 1] describing how centred the spray was on the zone.
        /// <c>1</c> means the spray was within the zone's <see cref="FireTargetZone.AngleTolerance"/>.
        /// Falls off linearly to <c>0</c> at the extinguisher's cone boundary.
        /// Zero when <see cref="DidHitZone"/> is <c>false</c>.
        /// </summary>
        public float CoverageScore { get; }

        // ── Distance effectiveness ────────────────────────────────────────────────

        /// <summary>
        /// Normalised score in [0, 1] reflecting how well the spray distance falls
        /// within the extinguisher's optimal range.
        /// <c>1</c> inside the optimal band; falls off linearly outside.
        /// Zero when <see cref="DidHitZone"/> is <c>false</c>.
        /// </summary>
        public float DistanceScore { get; }

        // ── Compatibility and suppression ─────────────────────────────────────────

        /// <summary>
        /// Result of the extinguisher-type-vs-fire-class compatibility check.
        /// <see cref="CompatibilityResult.Neutral"/> when <see cref="DidHitZone"/> is <c>false</c>.
        /// </summary>
        public CompatibilityResult Compatibility { get; }

        /// <summary>
        /// Raw suppression amount calculated by the evaluator for this tick.
        /// Equal to <c>ExtinguishPower × deltaTime × CoverageScore × DistanceScore</c>.
        /// This is the value passed to <see cref="FireSource.ReceiveSpray"/>; the fire's
        /// own resistance scaling is applied inside that method.
        /// Zero when <see cref="DidHitZone"/> is <c>false</c>.
        /// </summary>
        public float ExtinguishAmountCalculated { get; }

        // ── Private constructors ─────────────────────────────────────────────────

        private ExtinguishResult(SprayMissReason reason, float distance)
        {
            DidHitZone               = false;
            MissReason               = reason;
            HitZone                  = null;
            Source                   = null;
            HitPoint                 = Vector3.zero;
            Distance                 = distance;
            AngleFromCenter          = 0f;
            CoverageScore            = 0f;
            DistanceScore            = 0f;
            Compatibility            = CompatibilityResult.Neutral;
            ExtinguishAmountCalculated = 0f;
        }

        private ExtinguishResult(
            FireTargetZone zone,
            FireSource source,
            Vector3 hitPoint,
            float distance,
            float angleFromCenter,
            float coverageScore,
            float distanceScore,
            CompatibilityResult compatibility,
            float extinguishAmountCalculated)
        {
            DidHitZone               = true;
            MissReason               = SprayMissReason.None;
            HitZone                  = zone;
            Source                   = source;
            HitPoint                 = hitPoint;
            Distance                 = distance;
            AngleFromCenter          = angleFromCenter;
            CoverageScore            = coverageScore;
            DistanceScore            = distanceScore;
            Compatibility            = compatibility;
            ExtinguishAmountCalculated = extinguishAmountCalculated;
        }

        // ── Static factories ─────────────────────────────────────────────────────

        /// <summary>Creates a result representing a spray that did not reach a live zone.</summary>
        /// <param name="reason">Why the spray missed.</param>
        /// <param name="distance">Distance travelled before the miss was determined. Zero if unknown.</param>
        public static ExtinguishResult Miss(SprayMissReason reason, float distance = 0f)
            => new ExtinguishResult(reason, distance);

        /// <summary>Creates a result representing a successful spray evaluation.</summary>
        public static ExtinguishResult Hit(
            FireTargetZone zone,
            FireSource source,
            Vector3 hitPoint,
            float distance,
            float angleFromCenter,
            float coverageScore,
            float distanceScore,
            CompatibilityResult compatibility,
            float extinguishAmountCalculated)
            => new ExtinguishResult(
                zone, source, hitPoint, distance,
                angleFromCenter, coverageScore, distanceScore,
                compatibility, extinguishAmountCalculated);

        /// <inheritdoc/>
        public override string ToString()
        {
            if (!DidHitZone)
                return $"ExtinguishResult [Miss: {MissReason}, dist={Distance:F2}m]";

            return $"ExtinguishResult [Hit: zone={HitZone.name}, " +
                   $"dist={Distance:F2}m, angle={AngleFromCenter:F1}°, " +
                   $"coverage={CoverageScore:F2}, compat={Compatibility}, " +
                   $"amount={ExtinguishAmountCalculated:F4}]";
        }
    }
}
