using System.Collections.Generic;
using UnityEngine;
using NGS.SLO.Shared;

namespace NGS.SLO.MeshesCombine
{
    public class MeshRendererCombiner
    {
        private IMeshCombiner _meshCombiner;

        public MeshRendererCombiner(IMeshCombiner meshCombiner)
        {
            _meshCombiner = meshCombiner;
        }

        public MeshCombineResult CreateCombinedObject(IList<MeshCombineInstance> instances, MeshCombineOptions options, out GameObject combinedGO)
        {
            MeshCombineResult combineResult = _meshCombiner.Combine(instances);

            if (!combineResult.success)
            {
                combinedGO = null;
                return combineResult;
            }

#if UNITY_EDITOR

            if (options.rebuildUV2)
                UnityEditor.Unwrapping.GenerateSecondaryUVSet(combineResult.combinedMesh);

#endif

            if (options.useLightweightBuffers)
                VertexBufferUtil.ToLightweightBuffer(combineResult.combinedMesh);

            combinedGO = new GameObject("Combined_MeshRenderer");
            combinedGO.isStatic = instances[0].renderer.gameObject.isStatic;
            combinedGO.gameObject.layer = instances[0].renderer.gameObject.layer;

            MeshRenderer combinedRenderer = combinedGO.AddComponent<MeshRenderer>();
            MeshFilter combinedFilter = combinedGO.AddComponent<MeshFilter>();

            MeshRenderer sourceRenderer = instances[0].renderer as MeshRenderer;
            int materialIndex = instances[0].submeshIndex;

            combinedRenderer.sharedMaterial = sourceRenderer.GetSharedMaterial(materialIndex);
            combinedFilter.sharedMesh = combineResult.combinedMesh;

            CopyMeshRendererSettings(sourceRenderer, combinedRenderer);

            if (options.rebuildUV2)
                combinedRenderer.lightmapIndex = -1;

            return combineResult;
        }


        private void CopyMeshRendererSettings(MeshRenderer source, MeshRenderer destination)
        {
            destination.enabled = source.enabled;
            destination.shadowCastingMode = source.shadowCastingMode;
            destination.receiveShadows = source.receiveShadows;
            destination.motionVectorGenerationMode = source.motionVectorGenerationMode;
            destination.lightProbeUsage = source.lightProbeUsage;
            
            destination.reflectionProbeUsage = source.reflectionProbeUsage;
            destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            destination.renderingLayerMask = source.renderingLayerMask;

            destination.lightmapIndex = source.lightmapIndex;
            destination.lightmapScaleOffset = new Vector4(1, 1, 0, 0);
        }
    }
}
