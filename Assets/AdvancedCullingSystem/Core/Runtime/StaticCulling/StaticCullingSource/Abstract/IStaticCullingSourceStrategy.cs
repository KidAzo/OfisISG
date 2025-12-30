using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    public interface IStaticCullingSourceStrategy
    {
        bool Validate(out string errorMessage);

        bool TryGetBounds(out Bounds bounds);

        CullingTarget CreateCullingTarget();

        void PrepareForBaking();

        void ClearAfterBaking();
    }
}
