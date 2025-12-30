using System;
using System.Collections.Generic;
using UnityEngine;


namespace NGS.SLO.MeshesCombine
{
    [Serializable]
    public struct BinaryTreeData
    {
        public Vector3 position;
        public Renderer renderer;
        public LODGroup lodGroup;

        public BinaryTreeData(Renderer renderer)
        {
            this.renderer = renderer;

            position = renderer.transform.position;
            lodGroup = null;
        }

        public BinaryTreeData(LODGroup lodGroup)
        {
            this.lodGroup = lodGroup;

            position = lodGroup.transform.position;
            renderer = null;
        }
    }
}
