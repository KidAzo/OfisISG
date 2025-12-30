using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    public class RendererCombiner
    {
        private MeshRendererCombiner _meshRendererCombiner;
        private SkinnedMeshRendererCombiner _skinnedMeshRendererCombiner;

        public RendererCombiner(IMeshCombiner meshCombiner, ISkinnedMeshCombiner skinnedMeshCombiner)
        {
            _meshRendererCombiner = new MeshRendererCombiner(meshCombiner);
            _skinnedMeshRendererCombiner = new SkinnedMeshRendererCombiner(skinnedMeshCombiner);
        }

        public MeshCombineResult CreateCombinedObject(IList<MeshCombineInstance> instances, MeshCombineOptions options, out GameObject combinedGO)
        {
            if (instances[0].renderer is MeshRenderer)
            {
                return _meshRendererCombiner.CreateCombinedObject(instances, options, out combinedGO);
            }
            else
            {
                return _skinnedMeshRendererCombiner.CreateCombinedObject(instances, options, out combinedGO);
            }
        }
    }
}
