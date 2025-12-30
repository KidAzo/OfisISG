using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    public static class MeshCombineInstancesComparer
    {
        public static bool HasEqualParameters(MeshCombineInstance instanceA, MeshCombineInstance instanceB)
        {
            if (instanceA.renderer.GetType() != instanceB.renderer.GetType())
                return false;

            if (instanceA.renderer is MeshRenderer)
                return HasMeshRenderersEqualParameters(instanceA.renderer as MeshRenderer, instanceB.renderer as MeshRenderer);

            if (instanceA.renderer is SkinnedMeshRenderer)
                return HasSkinnedMeshRenderersEqualParameters(instanceA.renderer as SkinnedMeshRenderer, instanceB.renderer as SkinnedMeshRenderer);

            throw new ArgumentException($"MeshCombineInstancesComparer::unknown renderer type {instanceA.renderer.name} : {instanceA.renderer.GetType()}");
        }

        private static bool HasMeshRenderersEqualParameters(MeshRenderer rendererA, MeshRenderer rendererB)
        {
            if (rendererA.shadowCastingMode != rendererB.shadowCastingMode)
                return false;

            if (rendererA.receiveShadows != rendererB.receiveShadows)
                return false;

            if (rendererA.motionVectorGenerationMode != rendererB.motionVectorGenerationMode)
                return false;

            if (rendererA.lightProbeUsage != rendererB.lightProbeUsage)
                return false;

            if (rendererA.reflectionProbeUsage != rendererB.reflectionProbeUsage)
                return false;

            if (rendererA.allowOcclusionWhenDynamic != rendererB.allowOcclusionWhenDynamic)
                return false;

            if (rendererA.renderingLayerMask != rendererB.renderingLayerMask)
                return false;

            if (rendererA.lightmapIndex != rendererB.lightmapIndex)
                return false;

            return true;
        }

        private static bool HasSkinnedMeshRenderersEqualParameters(SkinnedMeshRenderer rendererA, SkinnedMeshRenderer rendererB)
        {
            if (rendererA.shadowCastingMode != rendererB.shadowCastingMode)
                return false;

            if (rendererA.receiveShadows != rendererB.receiveShadows)
                return false;

            if (rendererA.motionVectorGenerationMode != rendererB.motionVectorGenerationMode)
                return false;

            if (rendererA.lightProbeUsage != rendererB.lightProbeUsage)
                return false;

            if (rendererA.reflectionProbeUsage != rendererB.reflectionProbeUsage)
                return false;

            if (rendererA.allowOcclusionWhenDynamic != rendererB.allowOcclusionWhenDynamic)
                return false;

            if (rendererA.renderingLayerMask != rendererB.renderingLayerMask)
                return false;

            if (rendererA.lightmapIndex != rendererB.lightmapIndex)
                return false;

            if (rendererA.quality != rendererB.quality)
                return false;

            if (rendererA.updateWhenOffscreen != rendererB.updateWhenOffscreen)
                return false;

            if (rendererA.skinnedMotionVectors != rendererB.skinnedMotionVectors)
                return false;

            return true;
        }
    }
}
