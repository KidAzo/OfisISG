using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.Shared
{
    public static class RendererHelper
    {
        private static List<Material> _materials;

        public static Material GetSharedMaterial(this Renderer renderer, int materialIndex)
        {
            if (_materials == null)
                _materials = new List<Material>();

            _materials.Clear();

            renderer.GetSharedMaterials(_materials);

            if (materialIndex < 0 || materialIndex >= _materials.Count)
            {
                Debug.Log("RendererHelper::GetSharedMaterial try to get material with out-of-bounds index");
                return null;
            }

            return _materials[materialIndex];
        }

        public static void SetSharedMaterial(this Renderer renderer, Material material, int materialIndex)
        {
            if (_materials == null)
                _materials = new List<Material>();

            _materials.Clear();

            renderer.GetSharedMaterials(_materials);

            if (materialIndex < 0 || materialIndex >= _materials.Count)
            {
                Debug.Log("RendererHelper::SetSharedMaterial try to set material with out-of-bounds index");
                return;
            }

            _materials[materialIndex] = material;

            renderer.sharedMaterials = _materials.ToArray();
        }

        public static bool TryGetSharedMesh(this Renderer renderer, out Mesh mesh)
        {
            if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();

                if (filter == null)
                {
                    mesh = null;
                    return false;
                }

                mesh = filter.sharedMesh;
                return mesh != null;
            }
            else if (renderer is SkinnedMeshRenderer)
            {
                mesh = (renderer as SkinnedMeshRenderer).sharedMesh;
                return mesh != null;
            }
            else
            {
                mesh = null;
                return false;
            }
        }

        public static bool TrySetSharedMesh(this Renderer renderer, Mesh mesh)
        {
            if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();

                if (filter == null)
                    return false;

                filter.sharedMesh = mesh;
                return true;
            }
            else if (renderer is SkinnedMeshRenderer)
            {
                (renderer as SkinnedMeshRenderer).sharedMesh = mesh;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
