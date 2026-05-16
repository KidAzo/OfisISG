using UnityEngine;

namespace FireExtinguisher.Core
{
    /// <summary>
    /// Provides spray physics data and perceptual evaluation references to the extinguisher system.
    /// Implement this interface to supply aiming data from any platform:
    /// an FPS camera, a VR controller nozzle, an AI agent, or a test fixture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface is the primary platform-seam for aiming. The core never queries input
    /// systems or transforms directly; it only consumes the values returned here.
    /// </para>
    /// <para>
    /// The interface distinguishes two conceptually different pairs of origin + direction:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Spray pair</b> (<see cref="SprayOrigin"/> / <see cref="SprayDirection"/>): used
    ///     for physics — the SphereCast that detects fire zones. Always anchored to the nozzle.
    ///   </item>
    ///   <item>
    ///     <b>Evaluation pair</b> (<see cref="EvaluationOrigin"/> / <see cref="EvaluationDirection"/>):
    ///     used to judge "did the player aim at this zone?" — the perceptual reference. For PC
    ///     (screen-centre aiming) this is the camera; for VR (nozzle aiming) this is the nozzle.
    ///   </item>
    /// </list>
    /// <para>
    /// Keeping these pairs separate means the evaluator stays platform-agnostic: it never
    /// branches on platform; it simply uses whichever values the provider supplies.
    /// </para>
    /// </remarks>
    public interface IAimProvider
    {
        // ── Spray pair (physics) ──────────────────────────────────────────────────

        /// <summary>
        /// World-space position where the spray exits the nozzle.
        /// Used as the origin for the SphereCast that detects fire zones.
        /// </summary>
        Vector3 SprayOrigin { get; }

        /// <summary>
        /// Normalized world-space direction of the spray.
        /// Used as the direction for the SphereCast.
        /// <list type="bullet">
        ///   <item>PC: <c>normalize(AimPoint − SprayOrigin)</c> — nozzle points toward the camera aim point.</item>
        ///   <item>VR: <c>nozzle.forward</c> — nozzle points wherever the controller is physically aimed.</item>
        /// </list>
        /// </summary>
        Vector3 SprayDirection { get; }

        /// <summary>
        /// World-space point the aimer is currently targeting.
        /// Returns <c>SprayOrigin + SprayDirection × maxRange</c> when no surface is hit.
        /// Used by crosshair UI, debug overlays, and AimPoint-dependent VFX.
        /// </summary>
        Vector3 AimPoint { get; }

        // ── Evaluation pair (perceptual aim reference) ────────────────────────────

        /// <summary>
        /// World-space position used as the origin for cone angle evaluation and
        /// the crosshair-bias pre-pass ray.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        ///   <item>
        ///     <b>PC</b>: camera position. The cone check asks "did the player aim at this zone?";
        ///     that question is answered from the player's eye. Using the nozzle instead introduces
        ///     close-range parallax (10–20°) that causes valid zones to be rejected.
        ///   </item>
        ///   <item>
        ///     <b>VR</b>: nozzle position. The player physically aims with the controller; the
        ///     nozzle IS the perceptual reference.
        ///   </item>
        /// </list>
        /// Implementations without a separate eye point should return <see cref="SprayOrigin"/>.
        /// </remarks>
        Vector3 EvaluationOrigin { get; }

        /// <summary>
        /// Normalized world-space direction used as the reference axis for cone angle checks
        /// and the crosshair-bias pre-pass ray.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        ///   <item>
        ///     <b>PC</b>: <c>camera.forward</c>. This is the axis the player perceives the spray
        ///     as going. Cone angles are measured against this direction from <see cref="EvaluationOrigin"/>.
        ///   </item>
        ///   <item>
        ///     <b>VR</b>: <c>nozzle.forward</c>. Same as <see cref="SprayDirection"/>; the nozzle
        ///     orientation is simultaneously the physical spray axis and the perceptual aim axis.
        ///   </item>
        /// </list>
        /// <b>Critical rule</b>: the evaluator always computes angles as
        /// <c>Angle(EvaluationDirection, normalize(zoneCenter − EvaluationOrigin))</c>.
        /// Both vectors must be derived from the same spatial reference — never mix origins.
        /// </remarks>
        Vector3 EvaluationDirection { get; }

        // ── Validity ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Whether the current aim data is valid and usable this frame.
        /// Returns <c>false</c> when the provider is uninitialized, tracking is lost,
        /// or the extinguisher is in an unusable pose.
        /// The controller must not evaluate spray when this is <c>false</c>.
        /// </summary>
        bool IsAimValid { get; }
    }
}
