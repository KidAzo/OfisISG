using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Woi.SelectionSystem
{
    /// <summary>
    /// VR: registers the right-controller forward ray with <see cref="FireVrGameplayInteractionRay"/>
    /// for <see cref="SelectionSystemManager"/> and other gameplay raycasts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectionVrInteractionRay : MonoBehaviour
    {
        private const string RightControllerObjectName = "Right Controller";
        private const string RayVisualChildName = "SelectionRayVisual";

        [SerializeField] private Transform rayOrigin;
        [SerializeField] private float rayStartInsetMeters = 0.08f;
        [SerializeField] private float maxDistance = 12f;
        [SerializeField] private LayerMask layerMask = Physics.DefaultRaycastLayers;

        [Header("Auto-find")]
        [SerializeField] private bool autoFindRightController = true;
        [SerializeField] private string preferControllerNameContains = "Right";

        [Header("Visual ray")]
        [SerializeField] private bool drawWorldRayLine = true;
        [SerializeField] private Color rayLineColor = new(0.25f, 0.85f, 1f, 0.92f);
        [SerializeField] private float rayLineWidth = 0.0045f;

        private LineRenderer lineRenderer;
        private Material runtimeRayLineMaterial;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[48];

        public Transform RayOrigin => rayOrigin;

        public void SetGameplayRayEnabled(bool gameplayRayEnabled)
        {
            if (!gameplayRayEnabled)
            {
                if (lineRenderer != null)
                    lineRenderer.enabled = false;

                FireVrGameplayInteractionRay.Unregister(this);
            }

            enabled = gameplayRayEnabled;

            if (gameplayRayEnabled && IsVrActive() && rayOrigin != null)
                FireVrGameplayInteractionRay.Register(this, rayOrigin, rayStartInsetMeters);
        }

        private void Awake()
        {
            if (rayOrigin == null && autoFindRightController)
                rayOrigin = FindRightControllerTransform(preferControllerNameContains);

            if (rayOrigin == null)
                rayOrigin = transform;

            EnsureLineRenderer();
        }

        private void OnEnable()
        {
            if (!IsVrActive())
            {
                if (lineRenderer != null)
                    lineRenderer.enabled = false;
                return;
            }

            if (rayOrigin == null)
                rayOrigin = transform;

            FireVrGameplayInteractionRay.Register(this, rayOrigin, rayStartInsetMeters);
        }

        private static bool IsVrActive()
        {
            return FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR;
        }

        private void OnDisable()
        {
            FireVrGameplayInteractionRay.Unregister(this);
        }

        private void OnDestroy()
        {
            if (runtimeRayLineMaterial != null)
            {
                Destroy(runtimeRayLineMaterial);
                runtimeRayLineMaterial = null;
            }
        }

        private void LateUpdate()
        {
            if (!IsVrActive())
                return;

            if (rayOrigin == null)
                return;

            Vector3 dir = rayOrigin.forward;
            if (dir.sqrMagnitude < 1e-8f)
                return;

            dir.Normalize();
            Vector3 origin = rayOrigin.position + dir * Mathf.Max(0f, rayStartInsetMeters);
            UpdateLineRenderer(origin, dir);
        }

        private void UpdateLineRenderer(Vector3 origin, Vector3 direction)
        {
            if (!drawWorldRayLine || lineRenderer == null)
                return;

            Ray ray = new(origin, direction);
            int hitCount = Physics.RaycastNonAlloc(ray, HitBuffer, maxDistance, layerMask, QueryTriggerInteraction.Collide);

            float endDistance = maxDistance;
            Transform skipRoot = rayOrigin;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = HitBuffer[i];
                if (hit.collider == null)
                    continue;

                if (skipRoot != null && hit.collider.transform.IsChildOf(skipRoot))
                    continue;

                if (hit.distance < endDistance)
                    endDistance = hit.distance;
            }

            lineRenderer.enabled = true;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, origin + direction * endDistance);
        }

        private void EnsureLineRenderer()
        {
            if (!drawWorldRayLine)
                return;

            Transform visual = transform.Find(RayVisualChildName);
            if (visual == null)
            {
                var visualObject = new GameObject(RayVisualChildName);
                visualObject.transform.SetParent(transform, false);
                visual = visualObject.transform;
            }

            lineRenderer = visual.GetComponent<LineRenderer>();
            if (lineRenderer == null)
                lineRenderer = visual.gameObject.AddComponent<LineRenderer>();

            lineRenderer.useWorldSpace = true;
            lineRenderer.widthMultiplier = 1f;
            lineRenderer.startWidth = rayLineWidth;
            lineRenderer.endWidth = rayLineWidth;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.positionCount = 2;

            if (runtimeRayLineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    runtimeRayLineMaterial = new Material(shader);
                    runtimeRayLineMaterial.color = rayLineColor;
                }
            }

            if (runtimeRayLineMaterial != null)
                lineRenderer.material = runtimeRayLineMaterial;

            lineRenderer.startColor = rayLineColor;
            lineRenderer.endColor = rayLineColor;
        }

        public static Transform FindRightControllerTransform(string preferNameContains = "Right")
        {
            Transform best = null;
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go == null || !go.scene.IsValid())
                    continue;

                bool nameMatches = go.name.Contains(RightControllerObjectName, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrEmpty(preferNameContains)
                        && go.name.Contains(preferNameContains, StringComparison.OrdinalIgnoreCase));

                if (!nameMatches)
                    continue;

                if (go.name.Equals(RightControllerObjectName, StringComparison.Ordinal))
                    return go.transform;

                best ??= go.transform;
            }

            return best;
        }
    }
}
