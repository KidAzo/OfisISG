using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    public class LODGroupCombiner
    {
        private RendererCombiner _combiner;


        public LODGroupCombiner(IMeshCombiner meshCombiner, ISkinnedMeshCombiner skinnedMeshCombiner)
        {
            _combiner = new RendererCombiner(meshCombiner, skinnedMeshCombiner);
        }

        public MeshCombineResult[] CreateCombinedObject(IList<LODCombineInstance> instances, MeshCombineOptions options, out LODGroup combinedGroup)
        {
            if (instances == null || instances.Count == 0)
            {
                combinedGroup = null;

                return new MeshCombineResult[] 
                { 
                    new MeshCombineResult(instances.Select(i => i.instance).ToArray(), 
                    "LODGroupCombiner::CreateCombinedObject 'instances' is empty") 
                };
            }

            combinedGroup = CreateLODGroupObject();

            CopyLODGroupSettings(instances[0].lodGroup, combinedGroup);

            List<MeshCombineResult> combineResults = new List<MeshCombineResult>();

            LOD[] lods = CombineLODs(combinedGroup, instances, combineResults, options);

            combinedGroup.SetLODs(lods);

            return combineResults.ToArray();
        }


        private LODGroup CreateLODGroupObject()
        {
            return new GameObject("Combined_LODGroup")
                .AddComponent<LODGroup>();
        }

        private void CopyLODGroupSettings(LODGroup source, LODGroup destination)
        {
            destination.fadeMode = source.fadeMode;
            destination.animateCrossFading = source.animateCrossFading;
        }

        private LOD[] CombineLODs(LODGroup parent, IList<LODCombineInstance> instances, List<MeshCombineResult> outCombineResult, MeshCombineOptions options)
        {
            int lodsCount = instances.Max(i => i.lodGroup.lodCount);

            LOD[] lods = new LOD[lodsCount];

            float div = 1f / (lodsCount + 1);

            for (int i = 0; i < lodsCount; i++)
            {
                LevelOfDetailCombiner lodCombiner = new LevelOfDetailCombiner(this, i);

                MeshCombineInstance[] lodInstances = instances
                    .Where(l => l.lodLevel == i)
                    .Select(l => l.instance)
                    .ToArray();

                GameObject combinedLodGO = null;

                lodCombiner.Combine(lodInstances, outCombineResult, options, out combinedLodGO);

                combinedLodGO.transform.parent = parent.transform;

                lods[i] = new LOD(
                    1f - (div * (i + 1)),
                    combinedLodGO.GetComponentsInChildren<Renderer>());
            }

            return lods;
        }


        private class LevelOfDetailCombiner
        {
            private LODGroupCombiner _parent;
            private int _levelIndex;

            public LevelOfDetailCombiner(LODGroupCombiner parent, int levelIndex)
            {
                _parent = parent;
                _levelIndex = levelIndex;
            }

            public void Combine(MeshCombineInstance[] instances, List<MeshCombineResult> outCombineResult, MeshCombineOptions options, out GameObject combinedLodGO)
            {
                List<MeshCombineGroup> groups = new List<MeshCombineGroup>();
                MeshCombineInstancesGrouper.CreateCombineGroups(instances, options, groups);
                
                combinedLodGO = new GameObject($"LOD{_levelIndex}");

                foreach (var group in groups)
                {
                    GameObject combinedGO;

                    MeshCombineResult result = _parent._combiner.CreateCombinedObject(group.Instances, options, out combinedGO);

                    outCombineResult.Add(result);

                    if (result.success)
                    {
                        combinedGO.transform.parent = combinedLodGO.transform;
                    }
                }
            }
        }
    }
}
