using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.Training
{
    /// <summary>
    /// VR world kart anchor: varsa <see cref="FireSourceTrainingWorldPopupAnchor"/> (konum + isteğe bağlı ölçek çarpanı), yoksa
    /// <see cref="FireSource"/> collider/kökü veya oyuncu rig yanı (yerel offset).
    /// </summary>
    public static class TrainingVrFireWorldCardPlacement
    {
        public readonly struct Layout
        {
            public readonly bool UseColliderCenter;
            public readonly float LiftFromColliderCenter;
            public readonly float HeightAboveRoot;
            public readonly float ExtraWorldYOffset;

            /// <summary>Collider/kök + Y offset’ten sonra dünya uzayında ek X/Y/Z (m).</summary>
            public readonly Vector3 AdditionalWorldOffset;

            /// <summary>
            /// Açıksa yangın yerine kart <see cref="BesidePlayerLocalOffsetMeters"/> ile rig yerel uzayında konur
            /// (X = rig sağı, Y yukarı, Z = rig ileri).
            /// </summary>
            public readonly bool PlaceBesidePlayerRig;

            public readonly Vector3 BesidePlayerLocalOffsetMeters;

            public Layout(
                bool useColliderCenter,
                float liftFromColliderCenter,
                float heightAboveRoot,
                float extraWorldYOffset,
                Vector3 additionalWorldOffset = default,
                bool placeBesidePlayerRig = false,
                Vector3 besidePlayerLocalOffsetMeters = default)
            {
                UseColliderCenter = useColliderCenter;
                LiftFromColliderCenter = liftFromColliderCenter;
                HeightAboveRoot = heightAboveRoot;
                ExtraWorldYOffset = extraWorldYOffset;
                AdditionalWorldOffset = additionalWorldOffset;
                PlaceBesidePlayerRig = placeBesidePlayerRig;
                BesidePlayerLocalOffsetMeters = besidePlayerLocalOffsetMeters;
            }
        }

        /// <param name="worldDocumentScaleMultiplier">
        /// Yalnızca <see cref="FireSourceTrainingWorldPopupAnchor"/> kullanıldığında dolu: host <c>worldDocumentScale</c> ile çarpılır.
        /// </param>
        public static bool TryComputeAnchor(
            FireSource fire,
            in Layout layout,
            out Vector3 anchor,
            out float? worldDocumentScaleMultiplier)
        {
            anchor = default;
            worldDocumentScaleMultiplier = null;

            if (fire != null
                && TryGetPerFireSourcePopupPlacement(fire, out Vector3 explicitPoint, out float? scaleMul))
            {
                anchor = explicitPoint + layout.AdditionalWorldOffset;
                worldDocumentScaleMultiplier = scaleMul;
                return true;
            }

            if (layout.PlaceBesidePlayerRig)
            {
                if (!TrainingPlayerAnchorResolver.TryGetAnchorWorldTransform(out Transform rig))
                    return false;

                anchor = rig.TransformPoint(layout.BesidePlayerLocalOffsetMeters) + layout.AdditionalWorldOffset;
                return true;
            }

            if (fire == null || !fire.isActiveAndEnabled)
                return false;

            Vector3 baseAnchor = default;
            bool haveAnchor = false;

            if (layout.UseColliderCenter)
            {
                Collider[] cols = fire.GetComponentsInChildren<Collider>(true);
                Collider best = null;
                float bestVol = 0f;
                for (int i = 0; i < cols.Length; i++)
                {
                    Collider c = cols[i];
                    if (c == null || !c.enabled || c.isTrigger)
                        continue;

                    Vector3 s = c.bounds.size;
                    float v = Mathf.Abs(s.x * s.y * s.z);
                    if (v <= bestVol)
                        continue;

                    bestVol = v;
                    best = c;
                }

                if (best != null)
                {
                    baseAnchor = best.bounds.center + Vector3.up * layout.LiftFromColliderCenter;
                    baseAnchor += Vector3.up * layout.ExtraWorldYOffset;
                    haveAnchor = true;
                }
            }

            if (!haveAnchor)
            {
                baseAnchor = fire.transform.position + Vector3.up * layout.HeightAboveRoot;
                baseAnchor += Vector3.up * layout.ExtraWorldYOffset;
            }

            anchor = baseAnchor + layout.AdditionalWorldOffset;
            return true;
        }

        static bool TryGetPerFireSourcePopupPlacement(
            FireSource fire,
            out Vector3 worldPosition,
            out float? worldDocumentScaleMultiplier)
        {
            worldPosition = default;
            worldDocumentScaleMultiplier = null;
            if (fire == null)
                return false;

            FireSourceTrainingWorldPopupAnchor[] anchors =
                fire.GetComponentsInChildren<FireSourceTrainingWorldPopupAnchor>(true);

            for (int i = 0; i < anchors.Length; i++)
            {
                FireSourceTrainingWorldPopupAnchor a = anchors[i];
                if (a == null || !a.isActiveAndEnabled)
                    continue;

                if (!a.TryGetWorldPopupPlacement(out worldPosition, out float? mul))
                    continue;

                worldDocumentScaleMultiplier = mul;
                return true;
            }

            return false;
        }
    }
}
