using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    public interface ISkinnedMeshCombiner 
    {
        MeshCombineResult Combine(IList<MeshCombineInstance> instances, out Transform[] bones);
    }
}
