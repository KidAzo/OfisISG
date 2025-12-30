using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    public abstract class DC_LODGroupTargetBase : ICullingTarget
    {
        public GameObject GameObject 
        { 
            get 
            {
                return Group.gameObject;
            } 
        }
        public Bounds Bounds { get; private set; }

        protected LODGroup Group { get; private set; }
        protected Renderer[] Renderers { get; private set; }

        public DC_LODGroupTargetBase(LODGroup group, Renderer[] renderers, Bounds bounds)
        {
            Group = group;
            Renderers = renderers;
            Bounds = bounds;
        }

        public abstract void MakeVisible();

        public abstract void MakeInvisible();
    }
}
