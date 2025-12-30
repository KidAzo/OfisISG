using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    public struct MeshCombineResult
    {
        public bool success;
        public string errorMessage;
        public Mesh combinedMesh;
        public MeshCombineInstance[] instances;

        public MeshCombineResult(MeshCombineInstance[] instances, Mesh combinedMesh)
        {
            success = true;
            errorMessage = "";

            this.combinedMesh = combinedMesh;
            this.instances = instances;
        }

        public MeshCombineResult(MeshCombineInstance[] instances, string errorMessage)
        {
            success = false;
            combinedMesh = null;

            this.instances = instances;
            this.errorMessage = errorMessage;
        }
    }
}
