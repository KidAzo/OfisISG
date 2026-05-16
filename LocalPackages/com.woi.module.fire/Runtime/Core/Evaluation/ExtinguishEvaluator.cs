using UnityEngine;

namespace FireExtinguisher.Core
{
    /// <summary>
    /// Evaluates a single spray tick against the scene and returns a structured
    /// <see cref="ExtinguishResult"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Place this component on the extinguisher GameObject alongside, or as a child of,
    /// <c>ExtinguisherController</c>. The controller calls <see cref="Evaluate"/> each
    /// frame while discharging, forwarding all four values from the active
    /// <see cref="IAimProvider"/>.
    /// </para>
    /// <para>
    /// <b>Two-pair design</b> — the evaluator receives and uses two independent pairs:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Spray pair</b> (<c>origin</c> / <c>direction</c>):
    ///     used only for the <c>SphereCastNonAlloc</c> that detects fire zones.
    ///     Anchored to the nozzle on all platforms.
    ///   </item>
    ///   <item>
    ///     <b>Evaluation pair</b> (<c>evaluationOrigin</c> / <c>evaluationDirection</c>):
    ///     used for the crosshair-bias pre-pass ray and all cone angle checks.
    ///     On PC this is the camera position + camera.forward.
    ///     On VR this is the nozzle position + nozzle.forward (same as the spray pair).
    ///   </item>
    /// </list>
    /// <para>
    /// The evaluator contains no platform-specific branching. All platform differences
    /// live in the <see cref="IAimProvider"/> implementation. Passing
    /// <c>evaluationOrigin == origin</c> and <c>evaluationDirection == direction</c>
    /// (as VR does) produces behaviour identical to a single-origin nozzle-based check.
    /// </para>
    /// <para>
    /// <b>Angle rule</b>: cone angle = <c>Angle(evaluationDirection, normalize(zoneCenter −
    /// evaluationOrigin))</c>. Both vectors in this expression originate from
    /// <c>evaluationOrigin</c> — origins are never mixed.
    /// </para>
    /// <para>
    /// Detection uses <c>Physics.SphereCastNonAlloc</c> against a configurable
    /// <see cref="_fireZoneLayerMask"/>. Place all <see cref="FireTargetZone"/> colliders
    /// on a dedicated layer and assign it here to prevent spurious hits on walls or props.
    /// </para>
    /// <para>
    /// Effectiveness uses an elliptical two-zone angular falloff model. The angular
    /// deviation from the evaluation axis is decomposed into horizontal (camera-right) and
    /// vertical (camera-up) components. The horizontal component is divided by
    /// <c>(1 + horizontalForgiveness)</c> before being Pythagorean-recombined into a single
    /// <em>effectiveAngle</em>, producing a cone that is wider left/right than up/down.
    /// This compensates for the FPS nozzle lateral offset without any platform branching.
    /// <list type="bullet">
    ///   <item><b>Main cone</b> (effectiveAngle ≤ half-angle): sampled from <see cref="_coneEffectivenessCurve"/>;
    ///   runs from 1.0 at centre down to ~0.25 at the cone edge.</item>
    ///   <item><b>Soft fringe</b> (half-angle &lt; effectiveAngle ≤ half-angle + <see cref="_softFringeDegrees"/>):
    ///   a SmoothStep taper from the cone-edge value (~0.25) down to a near-zero floor
    ///   (<see cref="FringeMinEffectiveness"/>). Still registers a suppression tick.</item>
    ///   <item><b>Hard reject</b>: effectiveAngle beyond the fringe → <see cref="SprayMissReason.OutsideConeAngle"/>.</item>
    ///   <item><b>Close-range assist</b>: below <c>closeRangeThreshold</c>, the fringe is
    ///   expanded and a minimum angle factor floor is applied, compensating for the geometric
    ///   amplification of camera–nozzle parallax at very short distances.</item>
    /// </list>
    /// </para>
    /// </remarks>
    [AddComponentMenu("Fire Extinguisher/Extinguish Evaluator")]
    public sealed class ExtinguishEvaluator : MonoBehaviour
    {
        // ── Constants ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Effectiveness floor at the outer edge of the soft fringe.
        /// Ensures no sudden cliff at the fringe boundary.
        /// </summary>
        private const float FringeMinEffectiveness = 0.05f;

        // ── Inspector ────────────────────────────────────────────────────────────

        [Header("Detection")]
        [Tooltip("Layer mask for FireTargetZone colliders. Assign a dedicated fire zone layer " +
                 "to prevent the SphereCast from hitting walls, props, or the player.")]
        [SerializeField] private LayerMask _fireZoneLayerMask = Physics.AllLayers;

        [Tooltip("Maximum number of colliders the SphereCast buffer can hold. " +
                 "Increase only if many zones overlap in a single scene.")]
        [SerializeField, Range(1, 32)] private int _hitBufferSize = 8;

        [Header("Behaviour")]
        [Tooltip("When true, the evaluator calls FireSource.ReceiveSpray automatically " +
                 "and the caller only needs to read the returned result. " +
                 "Set false to intercept the application step (e.g., networked play).")]
        [SerializeField] private bool _applySuppressionOnEvaluate = true;

        [Header("Cone Effectiveness")]
        [Tooltip("Maps normalised position within the MAIN CONE → effectiveness multiplier.\n" +
                 "X axis: 0 = cone centre, 1 = cone edge (half-angle boundary).\n" +
                 "Y axis: effectiveness in [0, 1].\n\n" +
                 "The soft fringe zone beyond the cone edge uses its own SmoothStep taper.\n\n" +
                 "Leave empty to use the built-in cosine falloff (centre=1.0, mid≈0.75, edge=0.25).")]
        [SerializeField] private AnimationCurve _coneEffectivenessCurve;

        [Tooltip("Width of the soft fringe region beyond the main cone half-angle, in degrees.\n\n" +
                 "Inside this band, effectiveness eases smoothly (SmoothStep) from the cone-edge\n" +
                 "value (~0.25) down toward a near-zero floor (~0.05). Hits beyond cone + fringe\n" +
                 "are hard-rejected with OutsideConeAngle.\n\n" +
                 "Typical values: 5–12 degrees.")]
        [SerializeField, Min(0f)] private float _softFringeDegrees = 8f;

        [Header("Close-Range Assist")]
        [Tooltip("Nozzle distance in metres at which the close-range assist starts.\n\n" +
                 "Assist strength = 0 at this distance and rises to full at 0 m (nozzle touching the zone).\n" +
                 "Has no effect beyond this distance — normal mid-range behaviour is fully preserved.\n\n" +
                 "Set to 0 to disable the assist entirely.\n" +
                 "Typical value: 1.5 m (matches SprayRadius so the assist covers exactly the zone\n" +
                 "where the spray sphere already encloses the fire).")]
        [SerializeField, Min(0f)] private float _closeRangeThreshold = 1.5f;

        [Tooltip("Extra degrees added to the soft fringe at full close-range assist (distance = 0 m).\n\n" +
                 "Expands the hard-reject boundary proportionally as the nozzle closes in, preventing\n" +
                 "OutsideConeAngle misses caused by camera-nozzle parallax amplification:\n" +
                 "a 0.3 m lateral offset that is only ~8° at 2 m becomes 20°+ at 0.3 m.\n\n" +
                 "Applied proportionally: 0 at threshold, full value at 0 m.\n" +
                 "Typical values: 10–20 degrees.")]
        [SerializeField, Min(0f)] private float _closeRangeExtraFringeDegrees = 15f;

        [Tooltip("Minimum angleFactor at full close-range assist (distance = 0 m).\n\n" +
                 "Clamps the computed angle effectiveness so a zone near the expanded fringe edge\n" +
                 "still produces meaningful suppression at close range rather than falling to the\n" +
                 "near-zero FringeMinEffectiveness floor.\n\n" +
                 "Applied proportionally: 0 at threshold, full value at 0 m.\n" +
                 "Typical values: 0.15–0.35.")]
        [SerializeField, Range(0f, 1f)] private float _closeRangeMinAngleFactor = 0.25f;

        [Header("Horizontal Forgiveness")]
        [Tooltip("Extra leniency applied to left/right angular deviation relative to up/down.\n\n" +
                 "The effective horizontal angle is divided by (1 + value) before being recombined\n" +
                 "with the vertical angle into the single effective angle used for all cone and fringe\n" +
                 "checks. The result is an elliptical effective cone that is wider left/right than\n" +
                 "up/down, without any platform-specific branching.\n\n" +
                 "0 = circular cone, identical to previous behaviour.\n" +
                 "0.5 = horizontal deviation costs 33% fewer degrees (divisor = 1.5).\n" +
                 "1.0 = horizontal deviation costs 50% fewer degrees (divisor = 2.0).\n\n" +
                 "Use this to compensate for the FPS nozzle lateral offset: targets near the\n" +
                 "horizontal cone edge that appear visually covered will no longer be rejected\n" +
                 "prematurely. Vertical behaviour is unchanged.\n" +
                 "Typical values: 0.3–0.7.")]
        [SerializeField, Range(0f, 2f)] private float _horizontalForgiveness = 0.5f;

        [Header("Crosshair / Nozzle Aim Assist")]
        [Tooltip("How strongly the evaluation-ray–aligned zone is preferred during target selection.\n\n" +
                 "A thin pre-pass ray is fired from EvaluationOrigin along EvaluationDirection to\n" +
                 "identify the zone directly under the crosshair (PC) or nozzle forward (VR).\n" +
                 "That zone's nozzle distance is multiplied by (1 - bias) for selection scoring,\n" +
                 "making it win over geometrically closer but aim-misaligned zones.\n" +
                 "Effectiveness calculations always use the actual nozzle distance.\n\n" +
                 "0 = pure nozzle-distance selection (original behaviour).\n" +
                 "1 = aim-aligned zone always wins if the nozzle spray reaches it.\n" +
                 "PC typical: 0.6–0.8.  VR typical: 0.3–0.5 (less parallax to compensate).")]
        [SerializeField, Range(0f, 1f)] private float _crosshairBias = 0.7f;

        [Header("Distance Factor")]
        [Tooltip("Minimum distanceFactor applied when the nozzle is at distance 0 (point-blank).\n\n" +
                 "distanceFactor ramps smoothly from this floor at 0 m up to 1.0 at OptimalDistanceMin.\n" +
                 "When OptimalDistanceMin = 0 the ramp is skipped and this floor has no effect.\n\n" +
                 "finalEffect = extinguishPower × angleFactor × distanceFactor\n\n" +
                 "0 = point-blank gives zero distance contribution (old behaviour).\n" +
                 "0.4 = point-blank gives 40% of optimal distance contribution.\n" +
                 "1 = distance never penalises close range.")]
        [SerializeField, Range(0f, 1f)] private float _closeDistanceFloor = 0.4f;

        [Tooltip("Minimum distanceFactor applied when the nozzle is at MaxRange.\n\n" +
                 "distanceFactor ramps smoothly from 1.0 at OptimalDistanceMax down to this floor\n" +
                 "at MaxRange. Beyond MaxRange the SphereCast returns no hits, so this is the\n" +
                 "weakest suppression the system can produce while still registering a hit.\n\n" +
                 "0 = at MaxRange the distance factor is zero.\n" +
                 "0.15 = at MaxRange still delivers 15% of optimal distance contribution.")]
        [SerializeField, Range(0f, 1f)] private float _farDistanceFloor = 0.15f;

        [Header("Debug")]
        [Tooltip("When enabled, logs angleFactor, distanceFactor, and finalEffect to the Console\n" +
                 "each tick a suppression hit is registered.\n\n" +
                 "Disable in production — this generates one log entry per spray frame.")]
        [SerializeField] private bool _debugLog;

        // ── Private state ─────────────────────────────────────────────────────────

        private RaycastHit[] _hitBuffer;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _hitBuffer = new RaycastHit[_hitBufferSize];

            // Default main-cone curve (covers 0° → half-angle only):
            //   t = 0.0 → 1.00  (centre, full power)
            //   t = 0.5 → 0.75  (mid-cone, smooth drop)
            //   t = 1.0 → 0.25  (cone edge, where the soft fringe picks up)
            if (_coneEffectivenessCurve == null || _coneEffectivenessCurve.length == 0)
            {
                _coneEffectivenessCurve = new AnimationCurve(
                    new Keyframe(0.0f, 1.00f),
                    new Keyframe(0.5f, 0.75f),
                    new Keyframe(1.0f, 0.25f));

                for (int i = 0; i < _coneEffectivenessCurve.length; i++)
                    _coneEffectivenessCurve.SmoothTangents(i, 0f);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates a single spray tick and returns a complete <see cref="ExtinguishResult"/>.
        /// </summary>
        /// <param name="origin">
        /// World-space spray origin (nozzle tip). Used exclusively for the SphereCast.
        /// Maps to <see cref="IAimProvider.SprayOrigin"/>.
        /// </param>
        /// <param name="direction">
        /// Normalized world-space spray direction. Used exclusively for the SphereCast.
        /// Maps to <see cref="IAimProvider.SprayDirection"/>.
        /// </param>
        /// <param name="evaluationOrigin">
        /// World-space position used as the origin for the aim-assist pre-pass ray and all
        /// cone angle calculations. Maps to <see cref="IAimProvider.EvaluationOrigin"/>.
        /// PC: camera position. VR: nozzle position.
        /// </param>
        /// <param name="evaluationDirection">
        /// Normalized world-space direction used as the reference axis for the aim-assist
        /// pre-pass ray and all cone angle calculations.
        /// Maps to <see cref="IAimProvider.EvaluationDirection"/>.
        /// PC: camera.forward. VR: nozzle.forward.
        /// </param>
        /// <param name="data">Configuration asset for the active extinguisher. Must not be null.</param>
        /// <param name="deltaTime">Elapsed seconds for this tick. Pass <c>Time.deltaTime</c>.</param>
        public ExtinguishResult Evaluate(
            Vector3        origin,
            Vector3        direction,
            Vector3        evaluationOrigin,
            Vector3        evaluationDirection,
            ExtinguisherData data,
            float          deltaTime)
        {
            if (data == null)
            {
                Debug.LogWarning("[ExtinguishEvaluator] Evaluate called with null ExtinguisherData.", this);
                return ExtinguishResult.Miss(SprayMissReason.OutOfRange);
            }

            // ── 1. SphereCast (spray pair) ────────────────────────────────────────
            //
            // Uses origin (nozzle) and direction (spray direction) only.
            // The evaluation pair is never involved in physics detection.
            //
            // CLOSE-RANGE FIX — start the cast one SprayRadius BEHIND the nozzle:
            //   Physics.SphereCastNonAlloc does not detect colliders that the sphere
            //   already overlaps at t=0. When the player is within SprayRadius of a
            //   fire zone (the most common "standing right next to the fire" case),
            //   hitCount would be 0 and the evaluator would return OutOfRange.
            //   Pulling the origin back by SprayRadius and extending MaxRange by the
            //   same amount means the sphere travels through the nozzle position,
            //   registering any already-overlapping zone as a hit at distance ≈ 0.
            //   All reported hit.distances are corrected in the selection loop below.

            Vector3 castOrigin   = origin - direction * data.SprayRadius;
            float   castMaxRange = data.MaxRange + data.SprayRadius;

            int hitCount = Physics.SphereCastNonAlloc(
                castOrigin,
                data.SprayRadius,
                direction,
                _hitBuffer,
                castMaxRange,
                _fireZoneLayerMask,
                QueryTriggerInteraction.Collide);

            if (hitCount == 0)
                return ExtinguishResult.Miss(SprayMissReason.OutOfRange);

            // ── 2. Target selection — aim-assist biased (evaluation pair) ─────────
            //
            // Step 2a — Aim pre-pass ray:
            //   Fired from evaluationOrigin along evaluationDirection (camera→forward on PC,
            //   nozzle→forward on VR). Identifies the zone that is directly under the
            //   crosshair / nozzle forward without the lateral offset of the SphereCast.
            //   Without this, nozzle-distance ordering would force the player to aim slightly
            //   toward the nozzle side to win the correct zone in the selection ranking.
            //
            // Step 2b — Biased scoring:
            //   score = nozzle_distance × (1 - bias)  for the aim-aligned zone
            //   score = nozzle_distance × 1.0          for all others
            //   Lowest score wins. bestDist always holds the REAL nozzle distance and is
            //   used unchanged for all effectiveness calculations — the bias is selection-only.

            FireTargetZone aimAlignedZone = null;
            if (_crosshairBias > 0f)
            {
                if (Physics.Raycast(
                        new Ray(evaluationOrigin, evaluationDirection),
                        out RaycastHit evalHit,
                        data.MaxRange,
                        _fireZoneLayerMask,
                        QueryTriggerInteraction.Collide))
                {
                    evalHit.collider.TryGetComponent(out aimAlignedZone);
                }
            }

            FireTargetZone bestZone   = null;
            FireSource     bestSource = null;
            RaycastHit     bestHit    = default;
            float          bestScore  = float.MaxValue;
            float          bestDist   = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hitBuffer[i];

                if (!hit.collider.TryGetComponent(out FireTargetZone zone))
                    continue;

                FireSource source = zone.GetComponentInParent<FireSource>();
                if (source == null) continue;

                // Correct for the SprayRadius pullback applied to castOrigin.
                // hit.distance is measured from castOrigin (SprayRadius behind the nozzle),
                // so subtract SprayRadius to get the real nozzle → zone-surface distance.
                // Clamp to 0 for zones the sphere already enclosed at the nozzle position.
                float rawDist = Mathf.Max(0f, hit.distance - data.SprayRadius);

                float distanceMult = (zone == aimAlignedZone) ? (1f - _crosshairBias) : 1f;
                float score        = rawDist * distanceMult;

                if (score < bestScore)
                {
                    bestZone   = zone;
                    bestSource = source;
                    bestHit    = hit;
                    bestScore  = score;
                    bestDist   = rawDist;
                }
            }

            if (bestZone == null)
                return ExtinguishResult.Miss(SprayMissReason.NoFireZoneHit);

            // ── 3. Early-out: already-extinguished states ──────────────────────────

            if (bestSource.IsExtinguished)
                return ExtinguishResult.Miss(SprayMissReason.FireAlreadyExtinguished, bestDist);

            if (bestZone.IsExtinguished)
                return ExtinguishResult.Miss(SprayMissReason.ZoneAlreadyExtinguished, bestDist);

            // ── 4. Angle decomposition (evaluation pair only) ────────────────────
            //
            // RULE: all vectors are rooted at evaluationOrigin — origins are never mixed.
            //
            // PC:  evaluationOrigin = camera position, evaluationDirection = camera.forward
            //      → measures exactly what the player perceives; no nozzle-offset parallax.
            // VR:  evaluationOrigin = nozzle position, evaluationDirection = nozzle.forward
            //      → physical nozzle aim; horizontal forgiveness still applies but has no
            //        net effect when _horizontalForgiveness = 0 (VR prefers 0).
            //
            // WHY zone centre, not hit.point:
            //   SphereCast hit.point is a surface-contact tangent that can sit far off-axis
            //   when the sphere barely clips a collider edge. Zone centre is stable.
            //
            // WHY decompose into H/V:
            //   The spray volume is physically shifted sideways from the camera by the nozzle
            //   lateral offset. A circular cone treats left/right deviation identically to
            //   up/down, but the real spray footprint is wider left/right relative to what the
            //   player perceives. Decomposing and scaling the horizontal component by
            //   1/(1 + _horizontalForgiveness) produces an elliptical effective cone that is
            //   wider left/right without changing vertical behaviour.
            //   When _horizontalForgiveness = 0, effectiveAngle == angleFromCenter exactly.

            Vector3 toZoneCenter    = (bestZone.transform.position - evaluationOrigin).normalized;
            float   angleFromCenter = Vector3.Angle(evaluationDirection, toZoneCenter);

            // Reconstruct camera-space basis from evaluationDirection.
            // cross(worldUp, forward) = camera right for any non-vertical forward.
            // Gimbal-lock guard: if forward is near-vertical, fall back to world right.
            Vector3 worldUp  = Vector3.up;
            Vector3 camRight = Vector3.Cross(worldUp, evaluationDirection);
            camRight = camRight.sqrMagnitude > 0.001f ? camRight.normalized : Vector3.right;
            Vector3 camUp = Vector3.Cross(evaluationDirection, camRight); // already unit

            // Project zone deviation onto camera axes.
            // h = signed horizontal offset  (positive → right of centre)
            // v = signed vertical offset    (positive → above centre)
            float h = Vector3.Dot(toZoneCenter, camRight);
            float v = Vector3.Dot(toZoneCenter, camUp);

            // Convert to approximate deflection angles in each axis plane.
            // asin(|h|) is exact for horizontal-only deviation; approximation is excellent
            // for the moderate angles that matter in gameplay (< 45°).
            float horizDeg = Mathf.Asin(Mathf.Clamp(Mathf.Abs(h), 0f, 1f)) * Mathf.Rad2Deg;
            float vertDeg  = Mathf.Asin(Mathf.Clamp(Mathf.Abs(v), 0f, 1f)) * Mathf.Rad2Deg;

            // Apply horizontal forgiveness: divide horizontal degrees by (1 + factor).
            // factor=0 → no change; factor=0.5 → horiz costs 33% fewer degrees; factor=1 → 50%.
            float horizEffective = horizDeg / (1f + _horizontalForgiveness);
            float vertEffective  = vertDeg;

            // Recombine into a single effective angle used for all cone/fringe checks.
            // Pythagorean combination is exact when h and v are orthogonal (they always are).
            float effectiveAngle = Mathf.Sqrt(horizEffective * horizEffective +
                                              vertEffective  * vertEffective);

            // ── 5. Two-zone effectiveness model with close-range assist ───────────
            //
            // All threshold comparisons use effectiveAngle (elliptical).
            // angleFromCenter (spherical, unmodified) is preserved for result reporting so
            // that training / analytics systems see the true aim deviation.
            //
            // Close-range assist (closeRangeT):
            //   Camera-nozzle parallax amplifies the measured angle non-linearly at close
            //   range. A 0.3 m lateral offset that adds ~8° at 2 m adds ~20°+ at 0.3 m.
            //   With a 11.25° half-angle and 8° fringe, a zone the nozzle touches can still
            //   be hard-rejected. Two compensations scale smoothly with proximity:
            //     (a) Expanded fringe — widens the hard-reject boundary so zones inside the
            //         physical spray volume are not rejected due to parallax.
            //     (b) Minimum angle factor floor — ensures close-range fringe-edge hits still
            //         produce meaningful suppression instead of the near-zero FringeMin floor.
            //   Both effects are 0 at _closeRangeThreshold and at full strength at 0 m.
            //   They have zero effect beyond the threshold — mid-range behaviour is unchanged.
            //
            // Zone A — Main cone  (effectiveAngle ≤ coneHalfAngle):
            //   t ∈ [0,1] normalised within the cone. Sampled from _coneEffectivenessCurve.
            //
            // Zone B — Soft fringe  (coneHalfAngle < effectiveAngle ≤ fringeBoundary):
            //   SmoothStep taper from cone-edge value (~0.25) → FringeMinEffectiveness.
            //   Fringe width is expanded by close-range assist when applicable.
            //
            // Zone C — Hard reject  (effectiveAngle > fringeBoundary):
            //   OutsideConeAngle.

            // closeRangeT: 0 at threshold, 1 at distance 0. Zero when assist is disabled.
            float closeRangeT = _closeRangeThreshold > 0f
                ? 1f - Mathf.Clamp01(bestDist / _closeRangeThreshold)
                : 0f;

            float coneHalfAngle   = data.ConeAngleDegrees * 0.5f;
            float activeFringe    = _softFringeDegrees + _closeRangeExtraFringeDegrees * closeRangeT;
            float fringeBoundary  = coneHalfAngle + activeFringe;

            float angleFactor;

            if (effectiveAngle > fringeBoundary)
                return ExtinguishResult.Miss(SprayMissReason.OutsideConeAngle, bestDist);

            if (effectiveAngle <= coneHalfAngle)
            {
                // Zone A: sample the main-cone curve.
                float t = coneHalfAngle > 0f ? effectiveAngle / coneHalfAngle : 0f;
                angleFactor = EvaluateConeFalloff(Mathf.Clamp01(t));
            }
            else
            {
                // Zone B: SmoothStep taper through the (possibly expanded) soft fringe.
                float coneEdgeValue = EvaluateConeFalloff(1f);
                float fringeT       = activeFringe > 0f
                    ? (effectiveAngle - coneHalfAngle) / activeFringe
                    : 1f;
                float smoothFringeT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fringeT));
                angleFactor         = Mathf.Lerp(coneEdgeValue, FringeMinEffectiveness, smoothFringeT);
            }

            // Close-range minimum angle factor floor.
            // Prevents near-zero effectiveness on zones the nozzle spray physically encloses
            // but which measure near the fringe edge due to camera parallax amplification.
            if (closeRangeT > 0f)
                angleFactor = Mathf.Max(angleFactor, _closeRangeMinAngleFactor * closeRangeT);

            // ── 6. Distance factor ────────────────────────────────────────────────
            //
            // distanceFactor ∈ [0, 1], fully driven by the data asset + floor fields.
            //
            // Too close  (bestDist < OptimalDistanceMin):
            //   Lerp from _closeDistanceFloor at 0 m → 1.0 at OptimalDistanceMin.
            //   When OptimalDistanceMin = 0, this branch is skipped (factor = 1.0).
            //
            // Optimal    (OptimalDistanceMin ≤ bestDist ≤ OptimalDistanceMax):
            //   factor = 1.0 — full distance contribution.
            //
            // Too far    (bestDist > OptimalDistanceMax):
            //   Lerp from 1.0 at OptimalDistanceMax → _farDistanceFloor at MaxRange.
            //   Never reaches zero unless _farDistanceFloor is set to 0.

            float distanceFactor = CalculateDistanceFactor(bestDist, data,
                                                           _closeDistanceFloor, _farDistanceFloor,
                                                           data.RangeFalloffAmount);

            // ── 7. Final suppression ──────────────────────────────────────────────
            //
            // finalEffect = extinguishPower × angleFactor × distanceFactor
            //
            // Both factors are independent [0, 1] values:
            //   angleFactor  — how well-aimed the spray is (cone + fringe + close-range assist)
            //   distanceFactor — how appropriate the distance is (with configurable floors)
            //
            // This is the only place suppression magnitude is calculated.
            // No other factor bypasses or overrides this product.

            // ── Gate 1: electrical / prerequisite gate ────────────────────────────
            FireExtinguishPrerequisiteGate gate = bestSource.GetComponent<FireExtinguishPrerequisiteGate>();
            bool gateAllows = gate == null || gate.CanExtinguish;

            // ── Gate 2: fire-class vs extinguisher compatibility ──────────────────
            // Primary check: does this extinguisher's supported-fire-class list include
            // the fire's class? Logged once per tick when debug is on.
            FireClass fireClass = bestSource.Data != null ? bestSource.Data.FireClass : default;
            bool typeMatches = data.CanExtinguish(fireClass);

            bool canApplySuppression = gateAllows && typeMatches;
            float rawSuppression = canApplySuppression
                ? data.ExtinguishPower * deltaTime * angleFactor * distanceFactor
                : 0f;

            // ── 8. Debug log ──────────────────────────────────────────────────────

            if (_debugLog)
            {
                if (!typeMatches)
                {
                    Debug.Log(
                        $"[Extinguisher] WRONG TYPE — fire class={fireClass}, " +
                        $"extinguisher={data.ExtinguisherType} not in supported classes. Suppression blocked.",
                        this);
                }
                else
                {
                    Debug.Log(
                        $"[Extinguisher] angleFactor={angleFactor:F3}  " +
                        $"distanceFactor={distanceFactor:F3}  " +
                        $"finalEffect={rawSuppression:F4}  " +
                        $"(dist={bestDist:F2}m  angle={angleFromCenter:F1}°  " +
                        $"closeRangeT={1f - Mathf.Clamp01(bestDist / Mathf.Max(_closeRangeThreshold, 0.001f)):F2})",
                        this);
                }
            }

            // ── 9. Compatibility ──────────────────────────────────────────────────

            CompatibilityResult compatibility = bestSource.CheckCompatibility(data.ExtinguisherType);

            if (gate != null && !gateAllows && typeMatches && compatibility == CompatibilityResult.Effective)
            {
                float hypotheticalSuppression = data.ExtinguishPower * deltaTime * angleFactor * distanceFactor;
                if (hypotheticalSuppression > 0.00001f)
                    gate.NotifyPrerequisiteBlockedSprayAttempt();
            }

            // ── 10. Build result ──────────────────────────────────────────────────

            ExtinguishResult result = ExtinguishResult.Hit(
                zone:                       bestZone,
                source:                     bestSource,
                hitPoint:                   bestHit.point,
                distance:                   bestDist,
                angleFromCenter:            angleFromCenter,
                coverageScore:              angleFactor,
                distanceScore:              distanceFactor,
                compatibility:              compatibility,
                extinguishAmountCalculated: rawSuppression);

            // ── 11. Optional suppression application ──────────────────────────────

            if (_applySuppressionOnEvaluate && rawSuppression > 0f)
                bestSource.ReceiveSpray(data.ExtinguisherType, bestZone, rawSuppression);

            return result;
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates the main-cone effectiveness at normalised position <paramref name="t"/>,
        /// where 0 is the cone centre and 1 is the cone edge (half-angle boundary).
        /// Uses the assigned <see cref="_coneEffectivenessCurve"/> when available,
        /// otherwise falls back to: <c>0.25 + 0.75 × cos(t × π/2)</c>
        /// → centre = 1.0, mid ≈ 0.78, edge = 0.25.
        /// </summary>
        private float EvaluateConeFalloff(float t)
        {
            if (_coneEffectivenessCurve != null && _coneEffectivenessCurve.length > 0)
                return Mathf.Clamp01(_coneEffectivenessCurve.Evaluate(t));

            return 0.25f + 0.75f * Mathf.Cos(t * Mathf.PI * 0.5f);
        }

        /// <summary>
        /// Returns a [0, 1] distance factor for use in
        /// <c>finalEffect = extinguishPower × angleFactor × distanceFactor</c>.
        /// </summary>
        /// <param name="distance">Real nozzle → zone-surface distance (metres).</param>
        /// <param name="data">Extinguisher configuration asset.</param>
        /// <param name="closeFloor">
        /// Minimum factor at distance 0. Lerped up to 1.0 at <c>OptimalDistanceMin</c>.
        /// Has no effect when <c>OptimalDistanceMin = 0</c>.
        /// </param>
        /// <param name="farFloor">
        /// Minimum factor at <c>MaxRange</c>. Lerped down from 1.0 at <c>OptimalDistanceMax</c>.
        /// </param>
        /// <param name="falloffAmount">
        /// Curve exponent. Applied as <c>Pow(t, 1 / falloffAmount)</c> before lerping.
        /// 1 = linear. &gt;1 = steeper drop-off. &lt;1 = gentler drop-off.
        /// </param>
        private static float CalculateDistanceFactor(float distance, ExtinguisherData data,
                                                     float closeFloor, float farFloor,
                                                     float falloffAmount)
        {
            // Safeguard: exponent must be positive.
            float exp = 1f / Mathf.Max(falloffAmount, 0.01f);

            if (distance < data.OptimalDistanceMin)
            {
                // Too close: lerp from closeFloor at 0 m → 1.0 at OptimalDistanceMin.
                float t = data.OptimalDistanceMin > 0f ? distance / data.OptimalDistanceMin : 1f;
                return Mathf.Lerp(closeFloor, 1f, Mathf.Pow(Mathf.Clamp01(t), exp));
            }

            if (distance <= data.OptimalDistanceMax)
                return 1f;

            // Too far: lerp from 1.0 at OptimalDistanceMax → farFloor at MaxRange.
            float falloffRange = data.MaxRange - data.OptimalDistanceMax;
            float farT = falloffRange > 0f
                ? (distance - data.OptimalDistanceMax) / falloffRange
                : 1f;
            return Mathf.Lerp(1f, farFloor, Mathf.Pow(Mathf.Clamp01(farT), exp));
        }
    }
}
