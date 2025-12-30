using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    [Serializable]
    public struct MeshCombineOptions
    {
        public static MeshCombineOptions Default
        {
            get
            {
                return new MeshCombineOptions()
                {
                    limit65kVertices = true,
                    useLightweightBuffers = false
                };
            }
        }

        public bool limit65kVertices;
        public bool useLightweightBuffers;
        public bool rebuildUV2;
    }
}
