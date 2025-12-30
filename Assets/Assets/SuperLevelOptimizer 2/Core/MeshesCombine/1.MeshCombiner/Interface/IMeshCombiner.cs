using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    public interface IMeshCombiner
    {
        MeshCombineResult Combine(IList<MeshCombineInstance> instances);
    }
}
