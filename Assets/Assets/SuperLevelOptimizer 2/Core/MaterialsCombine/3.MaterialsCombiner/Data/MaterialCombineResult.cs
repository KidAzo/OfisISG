using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
    [Serializable]
    public struct MaterialCombineResult
    {
        public bool success;
        public string errorMessage;
        public Material combinedMaterial;
        public MaterialCombineInstance[] instances;
        public Rect[] rects;

        public MaterialCombineResult(string errorMessage)
        {
            this.errorMessage = errorMessage;

            success = false;
            combinedMaterial = null;
            instances = null;
            rects = null;
        }

        public MaterialCombineResult(Material combinedMaterial, MaterialCombineInstance[] instances, Rect[] rects)
        {
            this.combinedMaterial = combinedMaterial;
            this.instances = instances;
            this.rects = rects;

            success = true;
            errorMessage = "";
        }
    }
}
