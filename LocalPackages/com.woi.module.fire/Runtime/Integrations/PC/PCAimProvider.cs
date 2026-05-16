using FireExtinguisher.Core;
using UnityEngine;
using Woi.Player;
using WOI.Modules.SDK;

namespace FireExtinguisher.PC
{
    /// <summary>
    /// PC (screen-centre crosshair) implementation of <see cref="IAimProvider"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In a PC first-person setup the player aims with the screen centre, but the spray
    /// physically originates from the nozzle tip which is laterally offset from the camera.
    /// This provider separates that into two distinct pairs:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Spray pair</b>: origin = nozzle tip, direction = camera.forward.
    ///     The SphereCast travels from the nozzle in the same direction the player is looking.
    ///     This keeps the spray direction consistent with the evaluation direction so that VFX,
    ///     gizmos, and cone checks all share the same axis.
    ///   </item>
    ///   <item>
    ///     <b>Evaluation pair</b>: origin = camera position, direction = camera forward.
    ///     Cone angle checks and the crosshair-bias ray are measured from the player's eye.
    ///     This eliminates the close-range parallax (10–20°) that nozzle-based angle checks
    ///     produce when the nozzle is 0.3–0.5 m off-centre from the camera.
    ///   </item>
    /// </list>
    /// <para>
    /// Attach this component to the extinguisher GameObject and assign the nozzle tip transform.
    /// The view camera is resolved from <see cref="IPlayerService.playerCamera"/> via <see cref="ServiceLocator"/> unless <c>_camera</c> is assigned in the inspector.
    /// Do not rely on <c>Camera.main</c>.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Fire Extinguisher/PC/PC Aim Provider")]
    public sealed class PCAimProvider : MonoBehaviour, IAimProvider
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("Optional override. Leave empty to use the camera from IPlayerService. Camera.main is never used.")]
        [SerializeField] private Camera _camera;

        private IPlayerService _playerService;

        [Tooltip("World-space origin of the spray (the nozzle tip transform).")]
        [SerializeField] private Transform _nozzleTransform;

        [Header("Aim Raycast")]
        [Tooltip("Maximum distance of the centre-screen ray used to resolve the world-space aim point, " +
                 "in metres. This range also caps the spray distance fed to the evaluator.")]
        [SerializeField, Min(0.1f)] private float _aimRange = 15f;

        [Tooltip("Layers the centre-screen aim ray collides with. Should include world geometry " +
                 "and fire zones, but typically exclude the player and the extinguisher model.")]
        [SerializeField] private LayerMask _aimLayerMask = Physics.DefaultRaycastLayers;

        // ── Cached per-frame values ───────────────────────────────────────────────

        private Vector3 _cachedAimPoint;
        private Vector3 _cachedSprayDirection;
        private bool    _isValid;

        private Camera ViewCamera => _camera != null ? _camera : _playerService != null ? _playerService.playerCamera : null;

        // ── IAimProvider — spray pair ─────────────────────────────────────────────

        /// <inheritdoc/>
        public Vector3 SprayOrigin => _nozzleTransform != null
            ? _nozzleTransform.position
            : Vector3.zero;

        /// <inheritdoc/>
        /// <remarks>
        /// Always equals <c>camera.forward</c> — the direction the player is looking.
        /// The SphereCast travels from the nozzle in this direction, parallel to the camera ray.
        /// Using camera.forward here keeps SprayDirection, EvaluationDirection, and the
        /// crosshair-bias ray on the same axis, so VFX, gizmos, and cone checks are aligned.
        /// The nozzle lateral offset is handled by the SphereCast sphere radius and crosshair bias.
        /// </remarks>
        public Vector3 SprayDirection => _cachedSprayDirection;

        /// <inheritdoc/>
        /// <remarks>
        /// World-space point the centre-screen ray struck, or
        /// <c>camera.position + camera.forward × aimRange</c> when nothing is hit.
        /// </remarks>
        public Vector3 AimPoint => _cachedAimPoint;

        // ── IAimProvider — evaluation pair ───────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// Returns the camera position. Cone angle checks measured from here match exactly
        /// what the player sees from their eye — no nozzle-offset parallax.
        /// Falls back to <see cref="SprayOrigin"/> when the camera reference is unassigned.
        /// </remarks>
        public Vector3 EvaluationOrigin => ViewCamera != null
            ? ViewCamera.transform.position
            : SprayOrigin;

        /// <inheritdoc/>
        /// <remarks>
        /// Returns <c>camera.forward</c> — the axis the player perceives as "straight ahead".
        /// Cone angles and the crosshair-bias ray are always measured along this axis from
        /// <see cref="EvaluationOrigin"/>. Falls back to <see cref="SprayDirection"/> when
        /// the camera reference is unassigned.
        /// </remarks>
        public Vector3 EvaluationDirection => ViewCamera != null
            ? ViewCamera.transform.forward
            : SprayDirection;

        // ── IAimProvider — validity ───────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks><c>false</c> when either the camera or nozzle transform is unassigned.</remarks>
        public bool IsAimValid => _isValid;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _playerService = ServiceLocator.Get<IPlayerService>();
        }

        private void Start()
        {
            ValidateReferences();
        }

        private void OnEnable()
        {
            if (_playerService != null)
                _playerService.OnPlayerRegistered += HandlePlayerRegistered;
        }

        private void OnDisable()
        {
            if (_playerService != null)
                _playerService.OnPlayerRegistered -= HandlePlayerRegistered;
        }

        private void HandlePlayerRegistered()
        {
            ValidateReferences();
        }

        private void Update()
        {
            UpdateAim();
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private void UpdateAim()
        {
            Camera cam = ViewCamera;
            if (cam == null || _nozzleTransform == null)
            {
                _isValid = false;
                return;
            }

            _isValid = true;

            // SprayDirection = camera.forward. Both the SphereCast (spray pair) and the
            // cone check / bias ray (evaluation pair) use this same axis, so VFX, debug
            // gizmos, and angle evaluation all stay aligned. The small lateral offset between
            // the nozzle and the camera is absorbed by the SphereCast sphere radius and the
            // crosshair-bias selection mechanism.

            _cachedSprayDirection = cam.transform.forward;

            // AimPoint: world-space point the centre-screen ray struck, or the far end of
            // the range when nothing is hit. Used by crosshair UI and downstream VFX.

            Ray cameraRay = new Ray(cam.transform.position, cam.transform.forward);
            _cachedAimPoint = Physics.Raycast(cameraRay, out RaycastHit hit, _aimRange, _aimLayerMask)
                ? hit.point
                : cameraRay.GetPoint(_aimRange);
        }

        private void ValidateReferences()
        {
            if (ViewCamera == null)
                Debug.LogWarning(
                    "[PCAimProvider] No view camera: assign Camera in inspector or ensure IPlayerService is registered and PlayerController registers with playerCamera set.",
                    this);

            if (_nozzleTransform == null)
                Debug.LogWarning("[PCAimProvider] No NozzleTransform assigned.", this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateReferences();
        }
#endif
    }
}
