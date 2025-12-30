using System;
using System.Collections.Generic;
using UnityEngine;
using NGS.SLO.Shared;

namespace NGS.SLO.MeshesCombine
{
    public class MeshCombineInstance
    {
        public bool ReadyForCombine { get; private set; }
        public string Reason { get; private set; }

        public readonly Mesh mesh;
        public readonly Renderer renderer;

        public readonly Matrix4x4 transform;
        public readonly Vector4 lightmapScaleOffset;

        public readonly int submeshIndex;

        public readonly int indicesCount;
        public readonly int vertexCount;


        public MeshCombineInstance(Renderer renderer, int submeshIndex = 0)
        {
            if (renderer == null)
            {
                SetNotReadyForCombine("Renderer is null");
                return;
            }

            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
            {
                SetNotReadyForCombine("Incompatible renderer");
                return;
            }

            this.renderer = renderer;

            if (!renderer.TryGetSharedMesh(out mesh))
            {
                SetNotReadyForCombine("Mesh is missing");
                return;
            }

            if (submeshIndex >= mesh.subMeshCount)
            {
                SetNotReadyForCombine("SubmeshIndex {submeshIndex} greater or equal then submeshCount");
                return;
            }

            transform = renderer.localToWorldMatrix;
            lightmapScaleOffset = renderer.lightmapScaleOffset;

            this.submeshIndex = submeshIndex;

            indicesCount = mesh.GetSubMesh(submeshIndex).indexCount;
            vertexCount = mesh.vertexCount;

            SetReadyForCombine();
        }

        private void SetNotReadyForCombine(string reason)
        {
            ReadyForCombine = false;
            Reason = reason;
        }

        private void SetReadyForCombine()
        {
            ReadyForCombine = true;
            Reason = "";
        }
    }
}
