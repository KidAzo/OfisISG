using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    public class LODCombineInstancesComparer
    {
        public static bool HasEqualParameters(LODCombineInstance instanceA, LODCombineInstance instanceB)
        {
            if (instanceA.lodGroup.animateCrossFading != instanceB.lodGroup.animateCrossFading)
                return false;

            if (instanceA.lodGroup.fadeMode != instanceB.lodGroup.fadeMode)
                return false;

            return true;
        }
    }
}
