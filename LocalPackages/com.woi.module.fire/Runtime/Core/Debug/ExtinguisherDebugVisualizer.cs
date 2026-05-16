using UnityEngine;

namespace FireExtinguisher.Core
{
    /// <summary>
    /// Optional debug component that visualises spray evaluation geometry in the
    /// Scene view and at runtime via <c>Debug.DrawLine</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Attach alongside <see cref="ExtinguisherController"/> and assign the same
    /// <see cref="IAimProvider"/> source used by the controller.
    /// All visuals are driven by framework events and public state — no gameplay
    /// logic is introduced here.
    /// </para>
    /// <para>
    /// <b>Scene view:</b> Gizmo shapes are drawn via <c>OnDrawGizmos</c>. In Play mode,
    /// only the extinguisher that is currently held (<see cref="IHoldStateProvider.IsHeld"/>)
    /// draws — loose world pickups stay clean. In Edit mode, gizmos still draw for layout.<br/>
    /// <b>Game view:</b> Enable <i>Gizmos</i> in the Game view toolbar, or tick
    /// <see cref="_drawRuntimeLines"/> to use <c>Debug.DrawLine</c> during Play mode.
    /// </para>
    /// <para>
    /// Safe to disable or remove — nothing in the core system depends on this component.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Fire Extinguisher/Debug/Extinguisher Debug Visualizer")]
    public sealed class ExtinguisherDebugVisualizer : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("The ExtinguisherController whose spray will be visualised.")]
        [SerializeField] private ExtinguisherController _controller;

        [Tooltip("MonoBehaviour that implements IAimProvider — the same one assigned to the controller.")]
        [SerializeField] private MonoBehaviour _aimProviderSource;

        [Header("Runtime Lines (Game View)")]
        [Tooltip("Draw Debug.DrawLine calls each frame so spray is visible in the Game view " +
                 "without opening the Scene view. Has no effect in builds.")]
        [SerializeField] private bool _drawRuntimeLines = true;

        [Tooltip("Duration each Debug line persists, in seconds. Keep at 0 for single-frame lines.")]
        [SerializeField, Min(0f)] private float _lineDuration = 0f;

        [Header("Colours")]
        [SerializeField] private Color _aimLineColor      = new Color(0.0f, 0.8f, 1.0f, 1f); // cyan
        [SerializeField] private Color _hitColor          = new Color(0.2f, 1.0f, 0.2f, 1f); // green
        [SerializeField] private Color _missColor         = new Color(1.0f, 0.3f, 0.3f, 1f); // red
        [SerializeField] private Color _coneColor         = new Color(0.8f, 0.8f, 0.0f, 0.4f); // dim yellow
        [SerializeField] private Color _hitZoneColor      = new Color(1.0f, 0.9f, 0.0f, 1f); // yellow

        // ── Runtime state ─────────────────────────────────────────────────────────

        private IAimProvider        _aimProvider;
        private IHoldStateProvider   _holdProvider;
        private ExtinguishResult     _lastResult;
        private bool                 _hasResult;

        // Cached every frame from the aim provider for gizmo drawing.
        private Vector3 _cachedOrigin;
        private Vector3 _cachedDirection;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _aimProvider = _aimProviderSource as IAimProvider;

            // If the assigned source doesn't implement IAimProvider (or nothing was
            // assigned), fall back to any IAimProvider on this same GameObject.
            if (_aimProvider == null)
                _aimProvider = GetComponent<IAimProvider>();

            if (_aimProvider == null)
                Debug.LogWarning("[ExtinguisherDebugVisualizer] No IAimProvider found. " +
                                 "Assign Aim Provider Source or add an IAimProvider component to this GameObject.", this);

            _holdProvider = GetComponentInChildren<IHoldStateProvider>();
        }

        private void OnEnable()
        {
            if (_controller != null)
                _controller.OnSprayEvaluated += HandleSprayEvaluated;
        }

        private void OnDisable()
        {
            if (_controller != null)
                _controller.OnSprayEvaluated -= HandleSprayEvaluated;
        }

        private void LateUpdate()
        {
            RefreshAimCache();

            if (!_drawRuntimeLines || _controller == null) return;
            if (!ShouldDrawHeldOnlyDebug()) return;

            DrawRuntimeLines();
        }

        private void RefreshAimCache()
        {
            if (_controller != null)
            {
                _cachedOrigin    = _controller.ResolvedSprayWorldOrigin;
                _cachedDirection = _controller.ResolvedSprayWorldDirection;
                return;
            }

            if (_aimProvider != null)
            {
                _cachedOrigin    = _aimProvider.SprayOrigin;
                _cachedDirection = _aimProvider.SprayDirection;
            }
        }

        // ── Gizmos ────────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (_controller == null) return;
            if (!ShouldDrawHeldOnlyDebug()) return;

            ExtinguisherData data = _controller.ExtinguisherData;
            if (data == null) return;

            Vector3 origin;
            Vector3 direction;
            if (Application.isPlaying && _controller != null)
            {
                origin    = _controller.ResolvedSprayWorldOrigin;
                direction = _controller.ResolvedSprayWorldDirection;
            }
            else if (Application.isPlaying)
            {
                origin    = _cachedOrigin;
                direction = _cachedDirection;
            }
            else if (_controller != null)
            {
                _controller.ComputeResolvedSprayPose(out origin, out direction);
            }
            else if (_aimProvider != null)
            {
                origin    = _aimProvider.SprayOrigin;
                direction = _aimProvider.SprayDirection;
            }
            else
            {
                origin    = transform.position;
                direction = transform.forward;
            }

            DrawConeGizmos(origin, direction, data);

            if (_hasResult)
                DrawHitGizmos(origin);
        }

        // ── Private drawing ───────────────────────────────────────────────────────

        /// <summary>
        /// In Play mode, only the equipped extinguisher should spam the Scene/Game view.
        /// Edit mode keeps gizmos visible for prefab authoring.
        /// </summary>
        private bool ShouldDrawHeldOnlyDebug()
        {
            if (!Application.isPlaying)
                return true;

            return _holdProvider != null && _holdProvider.IsHeld;
        }

        private void DrawRuntimeLines()
        {
            ExtinguisherData data = _controller.ExtinguisherData;
            if (data == null) return;

            bool isHit   = _hasResult && _lastResult.DidHitZone;
            Color rayCol = isHit ? _hitColor : _aimLineColor;

            // Main spray ray.
            Vector3 rayEnd = isHit
                ? _lastResult.HitPoint
                : _cachedOrigin + _cachedDirection * data.MaxRange;

            Debug.DrawLine(_cachedOrigin, rayEnd, rayCol, _lineDuration);

            // Cross at hit point.
            if (isHit)
                DrawDebugCross(_lastResult.HitPoint, 0.08f, _hitColor);
        }

        private void DrawConeGizmos(Vector3 origin, Vector3 direction, ExtinguisherData data)
        {
            // ── Origin marker ────────────────────────────────────────────────────
            Gizmos.color = _aimLineColor;
            Gizmos.DrawWireSphere(origin, 0.04f);

            // ── Spray ray to max range ───────────────────────────────────────────
            Vector3 rangeEnd = origin + direction * data.MaxRange;
            Gizmos.color = _hasResult && _lastResult.DidHitZone ? _hitColor : _aimLineColor;
            Gizmos.DrawLine(origin, _hasResult && _lastResult.DidHitZone ? _lastResult.HitPoint : rangeEnd);

            // ── Spray cone edges ─────────────────────────────────────────────────
            // Build a stable basis perpendicular to the spray direction.
            Vector3 perp = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) < 0.99f
                ? Vector3.Cross(direction, Vector3.up).normalized
                : Vector3.Cross(direction, Vector3.forward).normalized;
            Vector3 up = Vector3.Cross(perp, direction).normalized;

            float coneRadius = Mathf.Tan(data.ConeAngleDegrees * Mathf.Deg2Rad) * data.MaxRange;

            Gizmos.color = _coneColor;
            Gizmos.DrawLine(origin, rangeEnd + perp  * coneRadius);
            Gizmos.DrawLine(origin, rangeEnd - perp  * coneRadius);
            Gizmos.DrawLine(origin, rangeEnd + up    * coneRadius);
            Gizmos.DrawLine(origin, rangeEnd - up    * coneRadius);

            // ── SphereCast sphere at max range ───────────────────────────────────
            Gizmos.color = new Color(_coneColor.r, _coneColor.g, _coneColor.b, 0.2f);
            Gizmos.DrawWireSphere(rangeEnd, data.SprayRadius);

            // ── Optimal range markers ────────────────────────────────────────────
            Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.25f);
            Gizmos.DrawWireSphere(origin + direction * data.OptimalDistanceMin, data.SprayRadius * 0.5f);
            Gizmos.DrawWireSphere(origin + direction * data.OptimalDistanceMax, data.SprayRadius * 0.75f);
        }

        private void DrawHitGizmos(Vector3 origin)
        {
            if (!_lastResult.DidHitZone)
            {
                Gizmos.color = _missColor;
                Gizmos.DrawWireSphere(_cachedOrigin + _cachedDirection * Mathf.Max(_lastResult.Distance, 0.2f), 0.06f);
                return;
            }

            // Hit point sphere.
            Gizmos.color = _hitColor;
            Gizmos.DrawWireSphere(_lastResult.HitPoint, 0.06f);

            // SphereCast sphere at hit distance (shows actual detection volume).
            ExtinguisherData data = _controller.ExtinguisherData;
            if (data != null)
            {
                Gizmos.color = new Color(_hitColor.r, _hitColor.g, _hitColor.b, 0.15f);
                Gizmos.DrawWireSphere(_lastResult.HitPoint, data.SprayRadius);
            }

            // Hit zone centre.
            if (_lastResult.HitZone != null)
            {
                Vector3 zoneCenter = _lastResult.HitZone.transform.position;

                Gizmos.color = _hitZoneColor;
                Gizmos.DrawLine(_lastResult.HitPoint, zoneCenter);
                Gizmos.DrawWireSphere(zoneCenter, 0.12f);
            }

            // Line from origin to hit point for context.
            Gizmos.color = new Color(_hitColor.r, _hitColor.g, _hitColor.b, 0.5f);
            Gizmos.DrawLine(origin, _lastResult.HitPoint);
        }

        private static void DrawDebugCross(Vector3 centre, float halfSize, Color color)
        {
            Debug.DrawLine(centre - Vector3.right   * halfSize, centre + Vector3.right   * halfSize, color);
            Debug.DrawLine(centre - Vector3.up      * halfSize, centre + Vector3.up      * halfSize, color);
            Debug.DrawLine(centre - Vector3.forward * halfSize, centre + Vector3.forward * halfSize, color);
        }

        // ── Event handler ─────────────────────────────────────────────────────────

        private void HandleSprayEvaluated(ExtinguishResult result)
        {
            _lastResult = result;
            _hasResult  = true;
        }
    }
}
