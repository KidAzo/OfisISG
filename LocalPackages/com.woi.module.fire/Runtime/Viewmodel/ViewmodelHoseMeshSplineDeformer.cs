using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Woi.Viewmodel
{
    /// <summary>
    /// Bends an existing <see cref="Mesh"/> to follow a <see cref="SplineContainer"/> (same spline
    /// that <see cref="ViewmodelHoseSplineDriver"/> updates). Use your authored hose mesh instead of
    /// <c>Spline Extrude</c> procedural geometry.
    /// </summary>
    /// <remarks>
    /// The mesh must be <b>Read/Write Enabled</b> in the import settings. Rest pose is assumed to run
    /// roughly along one local axis (default +Z); pick the axis that matches your modeler export.
    /// Runs in <see cref="LateUpdate"/> after <see cref="ViewmodelHoseSplineDriver"/> (higher default order).
    /// </remarks>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Viewmodel/Hose Mesh Spline Deformer")]
    public sealed class ViewmodelHoseMeshSplineDeformer : MonoBehaviour
    {
        public enum HoseAxis
        {
            X,
            Y,
            Z
        }

        [Header("References")]
        [Tooltip("Mesh to deform. Usually on the same GameObject as this component.")]
        [SerializeField]
        MeshFilter meshFilter;

        [Tooltip("Spline that drives the hose centerline. Often on a parent with ViewmodelHoseSplineDriver.")]
        [SerializeField]
        SplineContainer splineContainer;

        [Header("Rest pose")]
        [Tooltip("Dominant direction of the hose in the mesh's local space before deformation.")]
        [SerializeField]
        HoseAxis restHoseAxis = HoseAxis.Z;

        [Header("Output")]
        [Tooltip("If true, calls RecalculateNormals() each frame (better lighting, more CPU).")]
        [SerializeField]
        bool recalculateNormals = true;

        Mesh _runtimeMesh;
        Vector3[] _baseVertices;
        Vector3[] _deformed;
        float[] _tParam;
        float3[] _offsetFrenet;

        static float GetAxis(Vector3 v, HoseAxis a) =>
            a switch { HoseAxis.X => v.x, HoseAxis.Y => v.y, _ => v.z };

        static Vector3 SetAxis(Vector3 v, HoseAxis a, float value) =>
            a switch
            {
                HoseAxis.X => new Vector3(value, v.y, v.z),
                HoseAxis.Y => new Vector3(v.x, value, v.z),
                _ => new Vector3(v.x, v.y, value)
            };

        static Vector3 AxisTangent(HoseAxis a) =>
            a switch { HoseAxis.X => Vector3.right, HoseAxis.Y => Vector3.up, _ => Vector3.forward };

        static Vector3 AxisRight(HoseAxis a) =>
            a switch { HoseAxis.X => Vector3.up, HoseAxis.Y => Vector3.right, _ => Vector3.right };

        static Vector3 AxisUp(HoseAxis a) =>
            a switch { HoseAxis.X => Vector3.forward, HoseAxis.Y => Vector3.forward, _ => Vector3.up };

        void Awake()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();
            if (splineContainer == null)
                splineContainer = GetComponentInParent<SplineContainer>();

            if (meshFilter == null || splineContainer == null)
            {
                Debug.LogWarning(
                    "[ViewmodelHoseMeshSplineDeformer] Assign MeshFilter and SplineContainer (or parent a SplineContainer).",
                    this);
                enabled = false;
                return;
            }

            Mesh source = meshFilter.sharedMesh;
            if (source == null)
            {
                Debug.LogWarning("[ViewmodelHoseMeshSplineDeformer] MeshFilter has no sharedMesh.", this);
                enabled = false;
                return;
            }

            if (!source.isReadable)
            {
                Debug.LogError(
                    "[ViewmodelHoseMeshSplineDeformer] Mesh is not readable. Enable Read/Write on the mesh import settings.",
                    meshFilter);
                enabled = false;
                return;
            }

            _runtimeMesh = Instantiate(source);
            _runtimeMesh.name = source.name + " (Runtime Deformed)";
            meshFilter.mesh = _runtimeMesh;

            Vector3[] baseVerts = _runtimeMesh.vertices;
            int n = baseVerts.Length;
            _baseVertices = new Vector3[n];
            System.Array.Copy(baseVerts, _baseVertices, n);
            _deformed = new Vector3[n];
            _tParam = new float[n];
            _offsetFrenet = new float3[n];

            Bounds b = _runtimeMesh.bounds;
            float minA = GetAxis(b.min, restHoseAxis);
            float maxA = GetAxis(b.max, restHoseAxis);
            float axisSpan = Mathf.Max(1e-6f, maxA - minA);

            Vector3 t0 = AxisTangent(restHoseAxis);
            Vector3 r0 = AxisRight(restHoseAxis);
            Vector3 u0 = AxisUp(restHoseAxis);

            for (int i = 0; i < n; i++)
            {
                Vector3 v = baseVerts[i];
                float coord = GetAxis(v, restHoseAxis);
                float t = Mathf.Clamp01((coord - minA) / axisSpan);
                _tParam[i] = t;

                Vector3 restSpine = SetAxis(b.center, restHoseAxis, Mathf.Lerp(minA, maxA, t));
                Vector3 offset = v - restSpine;
                _offsetFrenet[i] = new float3(
                    Vector3.Dot(offset, r0),
                    Vector3.Dot(offset, u0),
                    Vector3.Dot(offset, t0));
            }
        }

        void LateUpdate()
        {
            if (_runtimeMesh == null || splineContainer == null || meshFilter == null)
                return;

            Spline spline = splineContainer.Spline;
            Transform meshTm = meshFilter.transform;
            Transform splineTm = splineContainer.transform;

            for (int i = 0; i < _deformed.Length; i++)
            {
                float t = _tParam[i];
                float3 off = _offsetFrenet[i];

                if (!SplineUtility.Evaluate(spline, t, out float3 pLocal, out float3 tanLocal, out float3 upLocal))
                {
                    _deformed[i] = _baseVertices[i];
                    continue;
                }

                Vector3 pW = splineTm.TransformPoint(pLocal);
                Vector3 tanW = splineTm.TransformDirection((Vector3)tanLocal);
                Vector3 upW = splineTm.TransformDirection((Vector3)upLocal);

                if (tanW.sqrMagnitude < 1e-10f)
                    tanW = meshTm.TransformDirection(AxisTangent(restHoseAxis));
                else
                    tanW.Normalize();

                if (upW.sqrMagnitude < 1e-10f)
                    upW = Vector3.up;

                Vector3 rightW = Vector3.Cross(tanW, upW);
                if (rightW.sqrMagnitude < 1e-10f)
                    rightW = Vector3.Cross(tanW, Vector3.up);
                if (rightW.sqrMagnitude < 1e-10f)
                    rightW = Vector3.Cross(tanW, Vector3.right);
                rightW.Normalize();

                upW = Vector3.Cross(rightW, tanW).normalized;

                Vector3 posM = meshTm.InverseTransformPoint(pW);
                Vector3 rightM = meshTm.InverseTransformDirection(rightW).normalized;
                Vector3 upM = meshTm.InverseTransformDirection(upW).normalized;
                Vector3 tanM = meshTm.InverseTransformDirection(tanW).normalized;

                _deformed[i] = posM + off.x * rightM + off.y * upM + off.z * tanM;
            }

            _runtimeMesh.vertices = _deformed;
            if (recalculateNormals)
                _runtimeMesh.RecalculateNormals();
            _runtimeMesh.RecalculateBounds();
        }
    }
}
