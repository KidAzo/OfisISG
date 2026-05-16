using FireExtinguisher.Core;
using UnityEngine;

namespace FireExtinguisher.VR
{
    /// <summary>
    /// VR (physical nozzle) implementation of <see cref="IAimProvider"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In VR the player physically moves and tilts the extinguisher. The nozzle transform
    /// IS the aiming reference — there is no separate screen centre or camera crosshair.
    /// Both the spray pair and the evaluation pair therefore collapse onto the nozzle:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Spray pair</b>: origin = nozzle tip, direction = nozzle.forward.
    ///     The SphereCast travels from the nozzle in the direction it physically points.
    ///   </item>
    ///   <item>
    ///     <b>Evaluation pair</b>: origin = nozzle tip, direction = nozzle.forward.
    ///     Cone angle checks are measured from the nozzle along nozzle.forward — exactly
    ///     where the physical spray is going. No parallax correction is needed because
    ///     there is no separate eye/camera involved in the aiming decision.
    ///   </item>
    /// </list>
    /// <para>
    /// <b>XR SDK integration</b>: this component reads from a <see cref="Transform"/> that
    /// you drive from your XR rig (e.g., assigned as a child of an XR controller, updated
    /// by XR Interaction Toolkit, OpenXR, or any other SDK). This class contains no SDK
    /// references and will compile without any XR package installed.
    /// </para>
    /// <para>
    /// Attach this component to the extinguisher GameObject and assign the nozzle tip transform
    /// that is parented to (and tracked by) the VR controller.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Fire Extinguisher/VR/VR Aim Provider")]
    public sealed class VRAimProvider : MonoBehaviour, IAimProvider
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("The nozzle tip transform. Must be a child of (or driven by) the VR controller " +
                 "so that its position and forward direction track the physical extinguisher nozzle.")]
        [SerializeField] private Transform _nozzleTransform;

        [Header("Aim")]
        [Tooltip("Maximum effective range of the spray in metres. Used to compute AimPoint " +
                 "when no surface is hit by the nozzle forward ray. Should match " +
                 "ExtinguisherData.MaxRange on the associated data asset.")]
        [SerializeField, Min(0.1f)] private float _aimRange = 10f;

        [Header("Spray axis (mesh vs logical spray)")]
        [Tooltip("Püskürtme yönü nozul yerel uzayında (normalize). Jet mavi +Z (forward) ile aynıysa (0,0,1); " +
                 "ters uç -Z ise (0,0,-1). Aşağı/yanlış eksen = transform/mesh ekseni uyuşmuyor demektir.")]
        [SerializeField] private Vector3 _sprayAxisLocal = new Vector3(0f, 0f, 1f);

        [Tooltip("İşaretliyse yön dünya uzayında çarpılır: -1 (SphereCast / mavi eksen hâlâ zıt ise deneyin).")]
        [SerializeField] private bool _invertSprayDirectionWorld;

        [Tooltip("Nozul yerel yeşil (Y) ekseni etrafında, mesh ekseninden çıkan yöne ek yaw (derece). " +
                 "SphereCast nozul ağzına göre kayıksa ±90 deneyin.")]
        [SerializeField] private float _sprayLocalYawOffsetDegrees;

        [Tooltip("Nozul yerel kırmızı (X / right) ekseni etrafında ek pitch (derece); örn. 180 tam ters.")]
        [SerializeField] private float _sprayLocalPitchOffsetDegrees;

        [Tooltip("Nozul yerel mavi (Z / forward) ekseni etrafında ek roll (derece); örn. 180.")]
        [SerializeField] private float _sprayLocalRollOffsetDegrees;

        // ── IAimProvider — spray pair ─────────────────────────────────────────────

        /// <inheritdoc/>
        public Vector3 SprayOrigin => _nozzleTransform != null
            ? _nozzleTransform.position
            : Vector3.zero;

        /// <inheritdoc/>
        /// <remarks>
        /// Yerel eksen + isteğe bağlı dünya ters çevirme; mesh ile SphereCast hizası için.
        /// </remarks>
        public Vector3 SprayDirection => GetSprayDirectionWorld();

        private Vector3 GetSprayDirectionWorld()
        {
            if (_nozzleTransform == null)
                return Vector3.forward;

            Vector3 local = _sprayAxisLocal.sqrMagnitude > 1e-10f
                ? _sprayAxisLocal.normalized
                : Vector3.forward;

            Vector3 baseWorld = _nozzleTransform.TransformDirection(local).normalized;
            Vector3 world = Quaternion.AngleAxis(_sprayLocalYawOffsetDegrees, _nozzleTransform.up) * baseWorld;
            world.Normalize();
            if (Mathf.Abs(_sprayLocalPitchOffsetDegrees) > 1e-5f)
            {
                world = Quaternion.AngleAxis(_sprayLocalPitchOffsetDegrees, _nozzleTransform.right) * world;
                world.Normalize();
            }

            if (Mathf.Abs(_sprayLocalRollOffsetDegrees) > 1e-5f)
            {
                world = Quaternion.AngleAxis(_sprayLocalRollOffsetDegrees, _nozzleTransform.forward) * world;
                world.Normalize();
            }

            if (_invertSprayDirectionWorld)
                world = -world;

            return world;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The world-space point along the nozzle forward ray. No geometry raycast is
        /// performed here — the point is purely the nozzle forward ray at <c>_aimRange</c>.
        /// Downstream systems (VFX, audio) can perform their own raycast if a surface-
        /// accurate AimPoint is needed for a specific visual effect.
        /// </remarks>
        public Vector3 AimPoint => SprayOrigin + SprayDirection * _aimRange;

        // ── IAimProvider — evaluation pair ────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// Equal to <see cref="SprayOrigin"/>. In VR the nozzle IS the perceptual aiming
        /// reference — there is no separate camera or eye point to measure from.
        /// </remarks>
        public Vector3 EvaluationOrigin => SprayOrigin;

        /// <inheritdoc/>
        /// <remarks>
        /// Equal to <see cref="SprayDirection"/>. The cone angle check and the crosshair-bias
        /// ray both use the nozzle forward as their reference axis, which is the physically
        /// correct reference for VR aiming.
        /// </remarks>
        public Vector3 EvaluationDirection => SprayDirection;

        // ── IAimProvider — validity ───────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks><c>false</c> when the nozzle transform is unassigned.</remarks>
        public bool IsAimValid => _nozzleTransform != null;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (_nozzleTransform == null)
                Debug.LogWarning("[VRAimProvider] No NozzleTransform assigned. " +
                                 "Assign the nozzle tip transform tracked by your VR controller.", this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_nozzleTransform == null)
                Debug.LogWarning("[VRAimProvider] No NozzleTransform assigned.", this);
        }
#endif
    }
}
