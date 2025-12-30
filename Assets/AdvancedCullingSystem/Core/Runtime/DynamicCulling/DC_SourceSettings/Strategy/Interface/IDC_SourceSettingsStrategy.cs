using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    public interface IDC_SourceSettingsStrategy
    {
        bool ReadyForCulling { get; }


        bool CheckCompatibilityAndGetComponents(out string incompatibilityReason);

        void PrepareForCulling();

        void ClearData();


        bool TryGetBounds(ref Bounds bounds);

        ICullingTarget CreateCullingTarget();

        IEnumerable<Collider> GetColliders();
    }
}
