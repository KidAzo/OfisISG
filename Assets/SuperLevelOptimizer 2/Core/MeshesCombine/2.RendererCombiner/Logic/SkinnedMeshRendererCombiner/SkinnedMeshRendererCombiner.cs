using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NGS.SLO.Shared;

namespace NGS.SLO.MeshesCombine
{
    public class SkinnedMeshRendererCombiner
    {
        private ISkinnedMeshCombiner _skinnedMeshCombiner;

        public SkinnedMeshRendererCombiner(ISkinnedMeshCombiner meshCombiner)
        {
            _skinnedMeshCombiner = meshCombiner;
        }

        public MeshCombineResult CreateCombinedObject(IList<MeshCombineInstance> instances, MeshCombineOptions options, out GameObject combinedGO)
        {
            Transform[] bones = null;

            MeshCombineResult combineResult = _skinnedMeshCombiner.Combine(instances, out bones);

            if (!combineResult.success)
            {
                combinedGO = null;
                return combineResult;
            }

            combinedGO = new GameObject("Combined_SkinnedMeshRenderer");
            combinedGO.gameObject.layer = instances[0].renderer.gameObject.layer;

            SkinnedMeshRenderer combinedRenderer = combinedGO.AddComponent<SkinnedMeshRenderer>();
            combinedRenderer.sharedMesh = combineResult.combinedMesh;

            SkinnedMeshRenderer sourceRenderer = instances[0].renderer as SkinnedMeshRenderer;
            int materialIndex = instances[0].submeshIndex;

            combinedRenderer.bones = bones;
            combinedRenderer.sharedMaterial = sourceRenderer.GetSharedMaterial(materialIndex);
            combinedRenderer.sharedMesh = combineResult.combinedMesh;

            CopySkinnedMeshRendererSettings(sourceRenderer, combinedRenderer);

            return combineResult;
        }


        private void CopySkinnedMeshRendererSettings(SkinnedMeshRenderer source, SkinnedMeshRenderer destination)
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

            destination.quality = source.quality;
            destination.updateWhenOffscreen = source.updateWhenOffscreen;
            destination.skinnedMotionVectors = source.skinnedMotionVectors;
        }
    }
}
