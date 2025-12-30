using System;
using System.Collections.Generic;
using UnityEngine;


namespace NGS.SLO.MaterialsCombine
{
    public enum MaterialCombineStatus { NotCombined, Combined, CombineError };

    [Serializable]
    public struct MaterialCombineReport
    {
        public Material material;
        public MaterialCombineStatus combineStatus;
        public string message;
    }
}
