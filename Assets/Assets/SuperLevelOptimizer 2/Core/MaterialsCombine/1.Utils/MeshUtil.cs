using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Object = UnityEngine.Object;

namespace NGS.SLO.MaterialsCombine
{
    public static class MeshUtil
    {
        private const int MAX_CACHED_MESHES_COUNT = 30;

        private static Dictionary<MeshAdjustOperation, Mesh> _adjustedMeshesCache;


        public static Mesh GetUVAdjustedMesh(Mesh mesh, MaterialCombineInstance instance, Rect rect)
        {
            if (_adjustedMeshesCache == null)
                _adjustedMeshesCache = new Dictionary<MeshAdjustOperation, Mesh>();

            Mesh result;
            MeshAdjustOperation operation = new MeshAdjustOperation(mesh, instance, rect);

            if (_adjustedMeshesCache.TryGetValue(operation, out result))
            {
                if (result == null)
                    _adjustedMeshesCache.Remove(operation);

                else
                    return result;
            }

            result = Object.Instantiate(mesh);

            AdjustUV(result, instance, rect);

            _adjustedMeshesCache.Add(operation, result);

            if (_adjustedMeshesCache.Count > MAX_CACHED_MESHES_COUNT)
                ClearCache();

            return result;
        }

        public static void AdjustUV(Mesh mesh, MaterialCombineInstance instance, Rect rect)
        {
            int[] indices = mesh.GetIndices(instance.MaterialIndex)
                .Distinct()
                .ToArray();
 
            Vector2[] uvs = mesh.uv;

            foreach (var index in indices)
            {
                Vector2 uv = uvs[index];

                uv.x -= Mathf.Floor(instance.Data.UVOffset.x);
                uv.y -= Mathf.Floor(instance.Data.UVOffset.y);

                uv.Scale(instance.Data.MainTextureScale);
                uv += instance.Data.MainTextureOffset;

                uv.x /= instance.Data.MainTextureRepeats.x;
                uv.y /= instance.Data.MainTextureRepeats.y;

                uv.x = rect.x + rect.width * uv.x;
                uv.y = rect.y + rect.height * uv.y;

                uvs[index] = uv;
            }

            mesh.uv = uvs;
        }

        public static void ClearCache()
        {
            _adjustedMeshesCache?.Clear();
        }


        private struct MeshAdjustOperation : IEquatable<MeshAdjustOperation>
        {
            public Mesh mesh;
            public MaterialCombineInstance instance;
            public Rect rect;


            public MeshAdjustOperation(Mesh mesh, MaterialCombineInstance instance, Rect rect)
            {
                this.mesh = mesh;
                this.instance = instance;
                this.rect = rect;
            }

            public override bool Equals(object obj)
            {
                return obj is MeshAdjustOperation o && Equals(o);
            }

            public bool Equals(MeshAdjustOperation other)
            {
                if (mesh != other.mesh)
                    return false;

                if (instance.MaterialIndex != other.instance.MaterialIndex)
                    return false;

                if (Mathf.Abs(rect.x - other.rect.x) > 0.001f)
                    return false;

                if (Mathf.Abs(rect.y - other.rect.y) > 0.001f)
                    return false;

                if (Mathf.Abs(rect.width - other.rect.width) > 0.001f)
                    return false;

                if (Mathf.Abs(rect.height - other.rect.height) > 0.001f)
                    return false;

                if (Mathf.Abs(instance.Data.MainTextureOffset.x - other.instance.Data.MainTextureOffset.x) > 0.001f)
                    return false;

                if (Mathf.Abs(instance.Data.MainTextureOffset.y - other.instance.Data.MainTextureOffset.y) > 0.001f)
                    return false;

                if (Mathf.Abs(instance.Data.MainTextureScale.x - other.instance.Data.MainTextureScale.x) > 0.001f)
                    return false;

                if (Mathf.Abs(instance.Data.MainTextureScale.y - other.instance.Data.MainTextureScale.y) > 0.001f)
                    return false;

                return true;
            }

            public override int GetHashCode()
            {
                int h = mesh ? mesh.GetInstanceID() : 0;       

                h = h * 397 ^ Quantize(rect.x);
                h = h * 397 ^ Quantize(rect.y);
                h = h * 397 ^ Quantize(rect.width);
                h = h * 397 ^ Quantize(rect.height);

                h = h * 397 ^ Quantize(instance.Data.MainTextureOffset.x);
                h = h * 397 ^ Quantize(instance.Data.MainTextureOffset.y);
                h = h * 397 ^ Quantize(instance.Data.MainTextureScale.x);
                h = h * 397 ^ Quantize(instance.Data.MainTextureScale.y);

                h = h * 397 ^ instance.MaterialIndex;

                return h;
            }


            private int Quantize(float v)
            {
                return Mathf.RoundToInt(v / 0.001f);
            }
        }
    }
}
