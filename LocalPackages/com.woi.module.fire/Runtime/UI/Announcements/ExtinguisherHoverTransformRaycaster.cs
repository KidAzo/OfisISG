using UnityEngine;
using UnityEngine.Rendering;

namespace Woi.UI.Announcements
{
    /// <summary>
    /// Same behaviour as <see cref="ExtinguisherHoverRaycaster"/> but casts from a transform (e.g. XR
    /// <c>Near-Far Interactor</c> or right controller) forward axis. Drives <see cref="ExtinguisherHoverController"/>
    /// with <see cref="HoverPointerMode.CameraCenterRay"/>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Physics.RaycastNonAlloc"/> and skips hits on colliders under <see cref="rayOrigin"/> so the first
    /// hit is not the controller mesh. Starts the ray slightly forward of the origin to reduce self-intersection.
    /// Optional <see cref="LineRenderer"/> on a child object draws the aim in the Game view to the first surface (or max distance).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ExtinguisherHoverTransformRaycaster : MonoBehaviour
    {
        [SerializeField]
        private Transform rayOrigin;

        [Tooltip("Push ray start along forward so controller / hand colliders are less likely to consume the hit.")]
        [SerializeField]
        private float rayStartInsetMeters = 0.08f;

        [SerializeField]
        private float maxDistance = 12f;

        [SerializeField]
        private LayerMask layerMask = Physics.DefaultRaycastLayers;

        [Tooltip("When true, every frame the ray hits no HoverController we clear stale hover state and optionally hide any visible popup.")]
        [SerializeField]
        private bool enforceCleanupWhenRayMissesHover = true;

        [SerializeField]
        private bool hideVisiblePopupWhenRayMissesHover = true;

        [Tooltip("Editor / dev: draw Physics.Raycast in Scene view (cyan).")]
        [SerializeField]
        private bool debugDrawRay;

        [Header("Visual ray (Game view)")]
        [Tooltip("Draw a LineRenderer from the ray start to the first surface along the aim (tubes, walls, floor), or max distance when nothing is hit.")]
        [SerializeField]
        private bool drawWorldRayLine = true;

        [SerializeField]
        private Color rayLineColor = new Color(0.25f, 0.85f, 1f, 0.92f);

        [SerializeField]
        private float rayLineWidth = 0.0045f;

        [Tooltip("Optional; if null, a simple unlit material is created at runtime (Sprites/Default or Unlit/Color).")]
        [SerializeField]
        private Material rayLineMaterial;

        private ExtinguisherHoverController _current;
        private LineRenderer _lineRenderer;
        private Material _runtimeRayLineMaterial;

        const string RayVisualChildName = "HoverRayVisual";

        static readonly RaycastHit[] s_HitBuffer = new RaycastHit[48];

        void Awake()
        {
            if (rayOrigin == null)
                rayOrigin = transform;

            EnsureLineRenderer();
        }

        void OnEnable()
        {
            if (rayOrigin == null)
                rayOrigin = transform;

            FireVrGameplayInteractionRay.Register(this, rayOrigin, rayStartInsetMeters);
        }

        void OnDestroy()
        {
            FireVrGameplayInteractionRay.Unregister(this);

            if (_runtimeRayLineMaterial != null)
            {
                Destroy(_runtimeRayLineMaterial);
                _runtimeRayLineMaterial = null;
            }
        }

        void LateUpdate()
        {
            if (rayOrigin == null)
                return;

            Vector3 dir = rayOrigin.forward;
            if (dir.sqrMagnitude < 1e-8f)
                return;

            dir.Normalize();
            Vector3 origin = rayOrigin.position + dir * Mathf.Max(0f, rayStartInsetMeters);
            if (!IsFiniteVector3(origin) || !IsFiniteVector3(dir))
            {
                if (_lineRenderer != null)
                    _lineRenderer.enabled = false;
                return;
            }

            if (ExtinguisherHoverController.IsPlayerHoldingExtinguisherForTubeHover())
            {
                if (_current != null)
                {
                    _current.NotifyRayHoverEnd();
                    _current = null;
                }
            }

            var ray = new Ray(origin, dir);

            ExtinguisherHoverController target = null;
            RaycastHit chosenHit = default;
            float closestSurfaceAlongRay = maxDistance;

            int count = Physics.RaycastNonAlloc(ray, s_HitBuffer, maxDistance, layerMask, QueryTriggerInteraction.Collide);
            if (count > 0)
            {
                float bestDist = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    ref RaycastHit h = ref s_HitBuffer[i];
                    if (h.collider == null)
                        continue;
                    if (!float.IsFinite(h.distance))
                        continue;
                    Transform t = h.collider.transform;
                    if (IsTransformUnder(t, rayOrigin))
                        continue;

                    if (h.distance < closestSurfaceAlongRay)
                        closestSurfaceAlongRay = h.distance;

                    var ctrl = h.collider.GetComponentInParent<ExtinguisherHoverController>();
                    if (ctrl == null || ctrl.PointerMode != HoverPointerMode.CameraCenterRay)
                        continue;

                    if (h.distance < bestDist)
                    {
                        bestDist = h.distance;
                        target = ctrl;
                        chosenHit = h;
                    }
                }
            }

            UpdateRayLine(origin, dir, closestSurfaceAlongRay);

            if (debugDrawRay)
                Debug.DrawRay(origin, dir * maxDistance, Color.cyan, 0f, false);

            if (target != _current)
            {
                if (_current != null)
                    _current.NotifyRayHoverEnd();

                _current = target;

                if (_current != null && !_current.NotifyRayHoverBegin(in chosenHit))
                    _current = null;
            }

            if (enforceCleanupWhenRayMissesHover && target == null)
                ExtinguisherHoverController.ApplyRayMissCleanup(hideVisiblePopupWhenRayMissesHover);
        }

        void EnsureLineRenderer()
        {
            if (!drawWorldRayLine)
                return;

            Transform holder = transform.Find(RayVisualChildName);
            if (holder == null)
            {
                var go = new GameObject(RayVisualChildName);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                holder = go.transform;
            }

            _lineRenderer = holder.GetComponent<LineRenderer>();
            if (_lineRenderer == null)
                _lineRenderer = holder.gameObject.AddComponent<LineRenderer>();

            _lineRenderer.positionCount = 2;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.loop = false;
            _lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;
            _lineRenderer.widthMultiplier = 1f;
            _lineRenderer.startWidth = rayLineWidth;
            _lineRenderer.endWidth = rayLineWidth * 0.65f;
            _lineRenderer.numCornerVertices = 3;
            _lineRenderer.numCapVertices = 2;

            Material mat = rayLineMaterial;
            if (mat == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");
                if (shader == null)
                    shader = Shader.Find("Hidden/Internal-Colored");

                if (shader != null)
                {
                    _runtimeRayLineMaterial = new Material(shader) { name = "ExtinguisherHoverRayLine (runtime)" };
                    if (_runtimeRayLineMaterial.HasProperty("_Color"))
                        _runtimeRayLineMaterial.color = rayLineColor;
                    else if (_runtimeRayLineMaterial.HasProperty("_BaseColor"))
                        _runtimeRayLineMaterial.SetColor("_BaseColor", rayLineColor);

                    mat = _runtimeRayLineMaterial;
                }
            }

            _lineRenderer.material = mat;
            _lineRenderer.sortingOrder = 80;
            _lineRenderer.startColor = rayLineColor;
            _lineRenderer.endColor = rayLineColor * 0.75f;
        }

        void UpdateRayLine(Vector3 origin, Vector3 dir, float endDistanceAlongRay)
        {
            if (_lineRenderer == null)
            {
                if (drawWorldRayLine)
                    EnsureLineRenderer();
                if (_lineRenderer == null)
                    return;
            }

            if (!drawWorldRayLine)
            {
                _lineRenderer.enabled = false;
                return;
            }

            _lineRenderer.enabled = true;
            if (!IsFiniteVector3(origin) || !IsFiniteVector3(dir) || !float.IsFinite(endDistanceAlongRay))
            {
                _lineRenderer.enabled = false;
                return;
            }

            float len = Mathf.Clamp(endDistanceAlongRay, 0.02f, maxDistance);
            Vector3 end = origin + dir * len;
            if (!IsFiniteVector3(end))
            {
                _lineRenderer.enabled = false;
                return;
            }

            _lineRenderer.SetPosition(0, origin);
            _lineRenderer.SetPosition(1, end);

            if (rayLineMaterial == null && _runtimeRayLineMaterial != null)
            {
                if (_runtimeRayLineMaterial.HasProperty("_Color"))
                    _runtimeRayLineMaterial.color = rayLineColor;
                else if (_runtimeRayLineMaterial.HasProperty("_BaseColor"))
                    _runtimeRayLineMaterial.SetColor("_BaseColor", rayLineColor);
            }

            _lineRenderer.startColor = rayLineColor;
            _lineRenderer.endColor = rayLineColor * 0.75f;
            _lineRenderer.startWidth = rayLineWidth;
            _lineRenderer.endWidth = rayLineWidth * 0.65f;
        }

        static bool IsFiniteVector3(Vector3 v) =>
            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        static bool IsTransformUnder(Transform leaf, Transform ancestor)
        {
            for (Transform t = leaf; t != null; t = t.parent)
            {
                if (t == ancestor)
                    return true;
            }

            return false;
        }

        void OnDisable()
        {
            FireVrGameplayInteractionRay.Unregister(this);

            if (_lineRenderer != null)
                _lineRenderer.enabled = false;

            if (_current != null)
            {
                _current.NotifyRayHoverEnd();
                _current = null;
            }

            if (enforceCleanupWhenRayMissesHover)
                ExtinguisherHoverController.ApplyRayMissCleanup(hideVisiblePopupWhenRayMissesHover);
        }
    }
}
