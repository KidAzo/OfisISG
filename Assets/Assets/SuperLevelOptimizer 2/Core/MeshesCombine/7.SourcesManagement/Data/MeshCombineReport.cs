using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    public enum MeshCombineStatus { Combined, CombineError }

    [Serializable]
    public struct MeshCombineReport
    {
        public int submeshIndex;
        public MeshCombineStatus status;
        public string combineError;
    }
}
