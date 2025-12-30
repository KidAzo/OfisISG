using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.SLO.MeshesCombine
{
    public class UnityMeshCombiner : IMeshCombiner
    {
        public MeshCombineResult Combine(IList<MeshCombineInstance> instances)
        {
            try
            {
                if (instances == null || instances.Count == 0)
                    return new MeshCombineResult(instances.ToArray(), "UnityMeshCombiner::instances is empty");

                foreach (var instance in instances)
                {
                    if (!instance.ReadyForCombine)
                        return new MeshCombineResult(instances.ToArray(), "Not all instances ready for combine");
                }

                CombineInstance[] unityCombineInstances = new CombineInstance[instances.Count];

                for (int i = 0; i < instances.Count; i++)
                {
                    MeshCombineInstance instance = instances[i];

                    if (instance.mesh == null || instance.renderer == null)
                        return new MeshCombineResult(instances.ToArray(), $"UnityMeshCombiner::MeshCombineInstance at index {i} with null mesh or renderer");

                    unityCombineInstances[i] = new CombineInstance()
                    {
                        mesh = instance.mesh,
                        subMeshIndex = instance.submeshIndex,
                        transform = instance.transform,
                        lightmapScaleOffset = instance.lightmapScaleOffset
                    };
                }

                int vertexCount = instances.Sum(i => i.vertexCount);

                Mesh result = new Mesh();
                result.name = CreateCombinedMeshName(instances);

                if (vertexCount >= UInt16.MaxValue)
                    result.indexFormat = IndexFormat.UInt32;

                result.CombineMeshes(unityCombineInstances, true, true, true);

                return new MeshCombineResult(instances.ToArray(), result);
            }
            catch (Exception ex)
            {
                return new MeshCombineResult(instances.ToArray(), $"{ex.Message} \n {ex.StackTrace}");
            }
        }

        private string CreateCombinedMeshName(IList<MeshCombineInstance> instances)
        {
            string name = "";

            int count = Mathf.Min(3, instances.Count);

            for (int i = 0; i < count; i++)
            {
                string rendererName = instances[i].renderer.name;

                if (rendererName.Length > 5)
                    rendererName = rendererName.Remove(5);

                name += $"{rendererName}_sub_{instances[i].submeshIndex}_";
            }

            name = name.Remove(name.Length - 1);

            return name;
        }
    }
}
