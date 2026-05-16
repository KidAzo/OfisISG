using FireExtinguisher.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Woi.Equipment;

namespace Woi.Viewmodel
{
    /// <summary>
    /// Visual-only hose: drives a <see cref="SplineContainer"/> from <see cref="hoseStart"/> to <see cref="nozzleRoot"/>
    /// with two interior knots so the curve sags naturally in first-person viewmodel space.
    /// Pair with <c>SplineExtrude</c> (Splines package) on the same object for mesh thickness.
    /// To bend an existing hose mesh instead, add <see cref="ViewmodelHoseMeshSplineDeformer"/> on the mesh object.
    /// </summary>
    /// <remarks>
    /// Place on the extinguisher pickup prefab (child of <see cref="PlayerExtinguisherEquipment"/>'s equip anchor).
    /// If you toggle the <b>Spline</b> GameObject off for the pre-pin state, put this driver on an <b>always-active</b> parent
    /// and assign <see cref="targetSplineContainer"/> (or leave unassigned so <c>GetComponentInChildren(..., true)</c> finds it);
    /// otherwise <see cref="MonoBehaviour.LateUpdate"/> will not run while the Spline object is inactive and the curve will not update in Play Mode.
    /// Optional <see cref="equipment"/> lets sag/side use the same camera as equipment pickup when <see cref="sideReference"/> is unset.
    /// When endpoints are missing, writes a minimal valid spline so <c>Spline Instantiate</c> does not hit empty-spline null refs.
    /// Optional spray-driven jitter on interior knots when <see cref="ExtinguisherController.IsDischarging"/> is true.
    /// Interior knot offsets are built in the plane perpendicular to the hose chord so near-parallel <c>forward</c>
    /// vectors do not throw knots off the curve (runtime kink / fold).
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Viewmodel/Hose Spline Driver")]
    public sealed class ViewmodelHoseSplineDriver : MonoBehaviour
    {
        [Header("Spline target")]
        [Tooltip("Spline to drive. Leave empty if this component sits on the same GameObject as SplineContainer. " +
                 "If the Spline object is disabled before the pin, assign the child's SplineContainer here or place this script on an always-active parent so LateUpdate keeps running.")]
        [SerializeField]
        SplineContainer targetSplineContainer;

        [Header("Endpoints (world positions → spline local space)")]
        [Tooltip("World-space anchor on the extinguisher body where the hose leaves.")]
        [SerializeField]
        Transform hoseStart;

        [Tooltip("World-space anchor at the nozzle base; spray VFX parent under NozzleTip.")]
        [SerializeField]
        Transform nozzleRoot;

        [Header("Shape")]
        [Tooltip("How much the hose droops between endpoints (world units, applied along -up of the basis).")]
        [SerializeField]
        float sagAmount = 0.08f;

        [Tooltip("Lateral offset for the mid knots (world units, along basis right).")]
        [SerializeField]
        float sideOffset = 0.03f;

        [Tooltip("Pushes the first mid knot along HoseStart.forward (helps clear the body mesh).")]
        [SerializeField]
        float startForwardOffset = 0.04f;

        [Tooltip("Pulls the second mid knot back along NozzleRoot.forward toward the body.")]
        [SerializeField]
        float endBackwardOffset = 0.04f;

        [Header("Author spline shape")]
        [Tooltip(
            "When true, uses the 5 knot positions authored in SplineContainer local space (below), warped so the first knot matches hoseStart and the last matches nozzleRoot. " +
            "When false, uses the procedural 4-knot sag curve.")]
        [SerializeField]
        bool useAuthorSplineShape = true;

        [Header("Update")]
        [Tooltip("When true, rebuilds the spline every frame in Play Mode and in the Editor (ExecuteAlways). When false, call RefreshHose() or tweak fields to trigger OnValidate.")]
        [SerializeField]
        bool updateEveryFrame = true;

        [Header("Optional basis")]
        [Tooltip("If set, sag uses -up and side uses right of this transform. Otherwise the SplineContainer transform is used.")]
        [SerializeField]
        Transform sideReference;

        [Header("Equipment (optional)")]
        [Tooltip("When Side Reference is empty, sag/side use this equipment's player camera. Leave empty to resolve from parent (equipped hierarchy under the player).")]
        [SerializeField]
        PlayerExtinguisherEquipment equipment;

        [Header("Spray wobble (optional)")]
        [Tooltip("When assigned (or found on parents), interior spline knots pick up a subtle jitter while IsDischarging is true.")]
        [SerializeField]
        ExtinguisherController extinguisherController;

        [Tooltip("Max world-space jitter at the first interior knot when fully spraying (second knot uses ~60%). 0 = off.")]
        [SerializeField]
        float sprayWobbleAmplitude;

        [Tooltip("Perlin noise scroll speed along time (higher = faster micro-motion).")]
        [SerializeField]
        float sprayWobbleNoiseSpeed = 3.2f;

        [Tooltip("Extra sine flutter on the chord axis (Hz).")]
        [SerializeField]
        float sprayWobbleFlutterHz = 19f;

        [Tooltip("How fast spray influence fades in/out when discharge starts/stops.")]
        [SerializeField]
        float sprayWobbleBlendSpeed = 7f;

        SplineContainer _container;
        bool _loggedMissingRefs;
        float _sprayWobbleBlend;

        void Awake()
        {
            // Run before default-order components (e.g. Spline Instantiate) so the spline is never empty on first Awake.
            CacheContainer();
            ResolveEquipmentIfNeeded();
            ResolveExtinguisherIfNeeded();
            RefreshHose();
        }

        void OnEnable()
        {
            CacheContainer();
            ResolveEquipmentIfNeeded();
            ResolveExtinguisherIfNeeded();
            RefreshHose();
        }

        void LateUpdate()
        {
            if (!updateEveryFrame)
                return;

            RefreshHose();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Inspector tweaks: preview spline without entering Play Mode.
            CacheContainer();
            ResolveExtinguisherIfNeeded();
            RefreshHose();
        }
#endif

        void CacheContainer()
        {
            if (_container != null)
                return;

            if (targetSplineContainer != null)
            {
                _container = targetSplineContainer;
                return;
            }

            _container = GetComponent<SplineContainer>();
            if (_container == null)
                _container = GetComponentInChildren<SplineContainer>(true);
        }

        void ResolveEquipmentIfNeeded()
        {
            if (equipment != null)
                return;
            equipment = GetComponentInParent<PlayerExtinguisherEquipment>();
        }

        void ResolveExtinguisherIfNeeded()
        {
            if (extinguisherController != null)
                return;
            extinguisherController = GetComponentInParent<ExtinguisherController>();
        }

        Transform ResolveBasisTransform()
        {
            if (sideReference != null)
                return sideReference;

            if (equipment == null)
                ResolveEquipmentIfNeeded();

            if (equipment != null && equipment.PlayerCamera != null)
                return equipment.PlayerCamera.transform;

            return _container != null ? _container.transform : transform;
        }

        void UpdateSprayWobbleBlend()
        {
            if (!Application.isPlaying)
            {
                _sprayWobbleBlend = 0f;
                return;
            }

            ResolveExtinguisherIfNeeded();
            bool discharging = extinguisherController != null && extinguisherController.IsDischarging;
            float target = discharging ? 1f : 0f;
            float speed = Mathf.Max(0.01f, sprayWobbleBlendSpeed);
            _sprayWobbleBlend = Mathf.MoveTowards(_sprayWobbleBlend, target, Time.deltaTime * speed);
        }

        Vector3 ComputeSprayWobbleOffset(Vector3 up, Vector3 right, Vector3 chord, float spanBlend)
        {
            if (_sprayWobbleBlend <= 0.0001f || sprayWobbleAmplitude <= 0f)
                return Vector3.zero;

            float t = Time.time;
            float amp = sprayWobbleAmplitude * _sprayWobbleBlend * spanBlend;
            float n1 = (Mathf.PerlinNoise(t * sprayWobbleNoiseSpeed, 0.31f) - 0.5f) * 2f;
            float n2 = (Mathf.PerlinNoise(0.71f, t * sprayWobbleNoiseSpeed) - 0.5f) * 2f;
            Vector3 along = chord.sqrMagnitude > 1e-10f ? chord.normalized : hoseStart.forward;
            float flutter = Mathf.Sin(t * (Mathf.PI * 2f) * sprayWobbleFlutterHz);
            return (right * n1 + up * n2 + along * (flutter * 0.22f)) * amp;
        }

        static Vector3 StableChordPlaneNormal(Vector3 chordDir, Vector3 refUp, Vector3 refForward)
        {
            Vector3 n = Vector3.ProjectOnPlane(refUp, chordDir);
            if (n.sqrMagnitude < 1e-8f)
                n = Vector3.ProjectOnPlane(refForward, chordDir);
            if (n.sqrMagnitude < 1e-8f)
                n = Vector3.ProjectOnPlane(Vector3.up, chordDir);
            if (n.sqrMagnitude < 1e-8f)
                return Vector3.right;
            return n.normalized;
        }

        static Vector3 ProjectOnChordPerpPlane(Vector3 direction, Vector3 chordDir, Vector3 fallbackUnit)
        {
            Vector3 p = Vector3.ProjectOnPlane(direction, chordDir);
            return p.sqrMagnitude >= 1e-8f ? p.normalized : fallbackUnit;
        }

        /// <summary>
        /// Five knots in <b>SplineContainer local space</b> as authored in the prefab (Spline 0).
        /// Warped at runtime so index 0 → hose start and index 4 → nozzle, preserving bend shape.
        /// </summary>
        static readonly float3[] AuthorSplineLocalKnots =
        {
            new float3(-0.6783334f, 0.2883339f, -0.01666667f),
            new float3(-1.616667f, 0.25f, 0.1f),
            new float3(-4.116667f, 1.1f, 1.466667f),
            new float3(-3.295f, 4.051667f, 4.116667f),
            new float3(-3.25f, 4.663334f, 4.985001f),
        };

        static bool TryWarpAuthorKnotsToEnds(float3 A, float3 B, out float3[] warped)
        {
            warped = null;
            float3 R0 = AuthorSplineLocalKnots[0];
            float3 R4 = AuthorSplineLocalKnots[4];
            float3 rChord = R4 - R0;
            float lenR = math.length(rChord);
            if (lenR < 1e-5f)
                return false;

            float3 rDir = rChord / lenR;
            float3 cChord = B - A;
            float lenC = math.length(cChord);
            if (lenC < 1e-5f)
                return false;

            float3 cDir = cChord / lenC;
            quaternion q = UnitVectorToUnitVectorRotation(rDir, cDir);
            float scale = lenC / lenR;

            int n = AuthorSplineLocalKnots.Length;
            warped = new float3[n];
            for (int i = 0; i < n; i++)
            {
                float3 v = AuthorSplineLocalKnots[i] - R0;
                float along = math.dot(v, rDir);
                float3 parallel = rDir * along;
                float3 perp = v - parallel;
                float3 perpRot = math.rotate(q, perp);
                warped[i] = A + cDir * (along * scale) + perpRot;
            }

            warped[0] = A;
            warped[n - 1] = B;
            return true;
        }

        /// <summary>Rotation taking unit <paramref name="from"/> to unit <paramref name="to"/> (Unity.Mathematics has no stable FromUnitVectors on all package versions).</summary>
        static quaternion UnitVectorToUnitVectorRotation(float3 from, float3 to)
        {
            Quaternion uq = Quaternion.FromToRotation(
                new Vector3(from.x, from.y, from.z),
                new Vector3(to.x, to.y, to.z));
            return new quaternion(uq.x, uq.y, uq.z, uq.w);
        }

        /// <summary>Rebuilds spline knots from current transforms (5-knot authored warp or 4-knot procedural sag).</summary>
        public void RefreshHose()
        {
            CacheContainer();
            if (_container == null)
            {
                LogMissingOnce("SplineContainer missing on this GameObject.");
                return;
            }

            if (hoseStart == null || nozzleRoot == null)
            {
                LogMissingOnce("Assign hoseStart and nozzleRoot for the hose path.");
                ApplyMinimalValidSpline();
                return;
            }

            _loggedMissingRefs = false;

            Transform basis = ResolveBasisTransform();
            Transform splineTm = _container.transform;

            Vector3 w0 = hoseStart.position;
            Vector3 w3 = nozzleRoot.position;
            Vector3 chord = w3 - w0;
            float chordLen = chord.magnitude;
            if (chordLen < 1e-5f)
            {
                ApplyMinimalValidSpline();
                return;
            }

            float3 l0 = (float3)splineTm.InverseTransformPoint(w0);
            float3 l3 = (float3)splineTm.InverseTransformPoint(w3);

            if (useAuthorSplineShape && TryWarpAuthorKnotsToEnds(l0, l3, out float3[] warped))
            {
                Vector3 chordDir = chord / chordLen;
                Vector3 authPlaneN = StableChordPlaneNormal(chordDir, basis.up, basis.forward);
                Vector3 authPlaneB = Vector3.Cross(chordDir, authPlaneN).normalized;

                UpdateSprayWobbleBlend();
                float spanBlend = Mathf.Clamp01(chordLen / 0.35f);
                Vector3 wobble = ComputeSprayWobbleOffset(authPlaneN, authPlaneB, chord, spanBlend);
                for (int i = 1; i <= 2 && i < warped.Length - 1; i++)
                {
                    Vector3 ww = splineTm.TransformPoint((Vector3)warped[i]);
                    ww += i == 1 ? wobble : wobble * 0.62f;
                    warped[i] = (float3)splineTm.InverseTransformPoint(ww);
                }

                Spline splineAuth = _container.Spline;
                splineAuth.Clear();
                for (int i = 0; i < warped.Length; i++)
                    splineAuth.Add(warped[i], TangentMode.AutoSmooth);

                _container.Spline = splineAuth;
                return;
            }

            Vector3 basisUp = basis.up;
            Vector3 basisRight = basis.right;
            Vector3 basisFwd = basis.forward;

            Vector3 chordDirProc = chord / chordLen;

            // Orthonormal "ribbon" frame in the plane ⊥ chord — avoids huge knot jumps when
            // hose/nozzle forward is almost parallel to the chord or camera axes line up badly.
            Vector3 planeN = StableChordPlaneNormal(chordDirProc, basisUp, basisFwd);
            Vector3 planeB = Vector3.Cross(chordDirProc, planeN).normalized;

            Vector3 sideAlong = Vector3.ProjectOnPlane(basisRight, chordDirProc);
            if (sideAlong.sqrMagnitude < 1e-8f)
                sideAlong = planeB;
            else
                sideAlong.Normalize();

            float sagScale = Mathf.Max(0.15f, chordLen);
            float sagMag = sagAmount * Mathf.Min(sagScale, chordLen * 0.45f);
            Vector3 sag = -planeN * sagMag;
            Vector3 side = sideAlong * sideOffset;

            Vector3 startFold = ProjectOnChordPerpPlane(hoseStart.forward, chordDirProc, planeN);
            Vector3 endFold = ProjectOnChordPerpPlane(-nozzleRoot.forward, chordDirProc, -startFold);

            Vector3 pThird = Vector3.Lerp(w0, w3, 1f / 3f);
            Vector3 pTwoThirds = Vector3.Lerp(w0, w3, 2f / 3f);

            Vector3 w1 = pThird + sag + side + startFold * startForwardOffset;
            Vector3 w2 = pTwoThirds + sag + side + endFold * endBackwardOffset;

            UpdateSprayWobbleBlend();
            float spanBlendProc = Mathf.Clamp01(chordLen / 0.35f);
            Vector3 wobbleProc = ComputeSprayWobbleOffset(planeN, planeB, chord, spanBlendProc);
            w1 += wobbleProc;
            w2 += wobbleProc * 0.62f;

            float3 l1 = (float3)splineTm.InverseTransformPoint(w1);
            float3 l2 = (float3)splineTm.InverseTransformPoint(w2);

            // Copy out, mutate, assign back — Spline is a struct in the Splines package.
            Spline spline = _container.Spline;
            spline.Clear();

            // Four knots with AutoSmooth tangents → smooth hose without manual tangent editing.
            spline.Add(l0, TangentMode.AutoSmooth);
            spline.Add(l1, TangentMode.AutoSmooth);
            spline.Add(l2, TangentMode.AutoSmooth);
            spline.Add(l3, TangentMode.AutoSmooth);

            _container.Spline = spline;
        }

        /// <summary>Nozzle anchor used for the hose path.</summary>
        public Transform NozzleRootTransform => nozzleRoot;

        /// <summary>Snap the nozzle transform to a world pose (e.g. from a post-pin layout empty).</summary>
        public void SnapNozzleToWorldPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (nozzleRoot == null)
                return;
            nozzleRoot.SetPositionAndRotation(worldPosition, worldRotation);
        }

        /// <summary>
        /// Short valid spline in container local space so consumers (e.g. <c>Spline Instantiate</c>) never see zero knots.
        /// </summary>
        void ApplyMinimalValidSpline()
        {
            if (_container == null)
                return;

            const float Eps = 0.001f;
            Spline spline = _container.Spline;
            spline.Clear();
            float3 a = float3.zero;
            float3 b = new float3(0f, 0f, Eps);
            spline.Add(a, TangentMode.AutoSmooth);
            spline.Add(math.lerp(a, b, 1f / 3f), TangentMode.AutoSmooth);
            spline.Add(math.lerp(a, b, 2f / 3f), TangentMode.AutoSmooth);
            spline.Add(b, TangentMode.AutoSmooth);
            _container.Spline = spline;
        }

        void LogMissingOnce(string message)
        {
            if (_loggedMissingRefs)
                return;
            _loggedMissingRefs = true;
            Debug.LogWarning("[ViewmodelHoseSplineDriver] " + message, this);
        }
    }
}
