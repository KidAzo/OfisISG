using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Designer-authored evacuation route using a <see cref="SplineContainer"/>.
    /// Draw knots in the Scene view; NPCs follow via <see cref="SplineNpcController"/>.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public sealed class EvacuationPath : MonoBehaviour
    {
        [SerializeField]
        private int splineIndex;

        [SerializeField]
        [Min(0.1f)]
        private float defaultMoveSpeed = 2.5f;

        private SplineContainer _container;

        public int SplineIndex => splineIndex;

        public float DefaultMoveSpeed => defaultMoveSpeed;

        private void Awake()
        {
            _container = GetComponent<SplineContainer>();
        }

        private void OnValidate()
        {
            _container = GetComponent<SplineContainer>();
        }

        public float GetLength()
        {
            SplineContainer container = Container;
            if (container == null || container.Splines == null || container.Splines.Count <= splineIndex)
            {
                return 0f;
            }

            return container.CalculateLength(splineIndex);
        }

        public bool TrySample(float normalizedTime, out Vector3 worldPosition, out Vector3 worldTangent)
        {
            worldPosition = default;
            worldTangent = Vector3.forward;

            SplineContainer container = Container;
            if (container == null || container.Splines == null || container.Splines.Count <= splineIndex)
            {
                return false;
            }

            float t = Mathf.Clamp01(normalizedTime);
            if (!container.Evaluate(splineIndex, t, out float3 position, out float3 tangent, out float3 _))
            {
                return false;
            }

            worldPosition = position;

            if (math.lengthsq(tangent) < 1e-6f)
            {
                worldTangent = transform.forward;
            }
            else
            {
                worldTangent = math.normalize(tangent);
            }

            return true;
        }

        private SplineContainer Container
        {
            get
            {
                if (_container == null)
                {
                    _container = GetComponent<SplineContainer>();
                }

                return _container;
            }
        }
    }
}
