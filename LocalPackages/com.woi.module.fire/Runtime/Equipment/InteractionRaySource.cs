using UnityEngine;

namespace Woi.Equipment
{
    /// <summary>
    /// PC: <paramref name="fallbackRayOrigin"/> (kamera / el). VR: FireVrGameplayInteractionRay ile sahnedeki
    /// ExtinguisherHoverTransformRaycaster’ın kaydettiği kontrolcü ışını (tüp hover ile aynı).
    /// </summary>
    public static class InteractionRaySource
    {
        public static bool TryGetWorldRay(Transform fallbackRayOrigin, out Vector3 origin, out Vector3 directionNormalized)
        {
            if (FireVrGameplayInteractionRay.TryGetRay(out origin, out directionNormalized))
            {
                if (!IsFiniteVector3(origin) || !IsFiniteVector3(directionNormalized) || directionNormalized.sqrMagnitude < 1e-10f)
                {
                    origin = default;
                    directionNormalized = default;
                    return false;
                }

                return true;
            }

            if (fallbackRayOrigin == null)
            {
                origin = default;
                directionNormalized = default;
                return false;
            }

            Vector3 o = fallbackRayOrigin.position;
            if (!IsFiniteVector3(o))
            {
                origin = default;
                directionNormalized = default;
                return false;
            }

            Vector3 d = fallbackRayOrigin.forward;
            if (d.sqrMagnitude < 1e-8f || !IsFiniteVector3(d))
            {
                origin = default;
                directionNormalized = default;
                return false;
            }

            directionNormalized = d.normalized;
            origin = o;
            if (!IsFiniteVector3(directionNormalized))
            {
                origin = default;
                directionNormalized = default;
                return false;
            }

            return true;
        }

        static bool IsFiniteVector3(Vector3 v) =>
            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
