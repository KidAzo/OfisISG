using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR

using UnityEditor;

#endif

namespace NGS.SLO.Shared
{
#if UNITY_EDITOR

    public class AssetsExporter
    {
        private static List<int> _checkedObjectIDs;
        private static event Action _delayedCall;


        public static void ExportMissingAssets(IReadOnlyList<GameObject> gos, string relativePath, 
            bool exportMeshes = true, 
            bool exportMaterials = true,
            string meshesFolderName = "meshes",
            string materialsFolderName = "materials",
            string texturesFolderName = "textures")
        {
            try
            {
                if (gos == null || gos.Count == 0)
                {
                    Debug.Log($"AssetsExporter::ExportMissingAssets 'gos' not assigned");
                    return;
                }

                if (!PathUtil.TryGetProjectRelative(relativePath, out relativePath))
                {
                    Debug.Log($"AssetsExporter::unable export assets. {relativePath} is incorrect path");
                    return;
                }

                _checkedObjectIDs ??= new List<int>();
                List<Material> materialsTemp = new List<Material>();

                StartAssetEditing();

                foreach (var go in gos)
                {
                    Renderer renderer = go.GetComponent<Renderer>();

                    if (renderer == null || (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)))
                        continue;

                    if (exportMeshes)
                    {
                        CreateMissingMeshAsset(renderer, $"{relativePath}{meshesFolderName}");
                    }

                    if (exportMaterials)
                    {
                        materialsTemp.Clear();

                        renderer.GetSharedMaterials(materialsTemp);

                        if (materialsTemp != null && materialsTemp.Count > 0)
                            CreateMissingMaterialsAssets(materialsTemp, relativePath, materialsFolderName, texturesFolderName);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"AssetsExporter::error during exporting missing assets: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {

                StopAssetEditing();
            }
        }

        public static void ClearCache()
        {
            _checkedObjectIDs?.Clear();
        }


        private static void StartAssetEditing()
        {
            _delayedCall = null;

            AssetDatabase.StartAssetEditing();
        }

        private static void StopAssetEditing()
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
            // AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            _delayedCall?.Invoke();
            _delayedCall = null;
        }

        private static void LoadAndAssignTexture(Material material, string texturePath, string textureName)
        {
            try
            {
                if (string.IsNullOrEmpty(texturePath))
                {
                    Debug.Log("AssetsExporter::unable to assign texture since 'path' is empty");
                    return;
                }

                if (material == null)
                {
                    Debug.Log("AssetsExporter::unable to assign texture since 'material' is null");
                    return;
                }

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

                if (texture == null)
                {
                    Debug.Log($"AssetsExporter::unable to assign texture. Not found texture at {texturePath}");
                    return;
                }

                material.SetTexture(textureName, texture);
            }
            catch (Exception ex)
            {
                Debug.Log($"AssetsExporter::unable to assign texture. {ex.Message}\n{ex.StackTrace}");
            }
        }


        private static void CreateMissingMeshAsset(Renderer renderer, string relativePath)
        {
            Mesh mesh;

            if (!renderer.TryGetSharedMesh(out mesh))
            {
                Debug.Log($"AssetsCreator::CreateMissingAssets unable to create assets for {renderer.name} cause suitable mesh not found");
                return;
            }

            int instanceID = mesh.GetInstanceID();

            if (_checkedObjectIDs.Contains(instanceID))
                return;

            if (!ResourcesUtil.IsAssetInProject(mesh))
                ResourcesUtil.SaveMeshAsAsset(mesh, relativePath, $"{mesh.name}_{instanceID}");

            _checkedObjectIDs.Add(instanceID);
        }

        private static void CreateMissingMaterialsAssets(IList<Material> materials, 
            string relativePath, string materialsFolderName, string texturesFolderName)
        {
            for (int i = 0; i < materials.Count; i++)
            {
                Material material = materials[i];

                if (material == null)
                    continue;

                if (ResourcesUtil.IsAssetInProject(material))
                    continue;

                int instanceID = material.GetInstanceID();

                if (_checkedObjectIDs.Contains(instanceID))
                    continue;

                CreateMissingTexturesAssets(material, $"{relativePath}{texturesFolderName}/{material.name}/");
                CreateMissingMaterialAsset(material, $"{relativePath}{materialsFolderName}/");

                _checkedObjectIDs.Add(instanceID);
            }
        }

        private static void CreateMissingMaterialAsset(Material material, string relativePath)
        {
            if (ResourcesUtil.IsAssetInProject(material))
                return;

            ResourcesUtil.SaveMaterialAsAsset(material, relativePath, $"{material.name}_{material.GetInstanceID()}");
        }

        private static void CreateMissingTexturesAssets(Material material, string relativePath)
        {
            Shader shader = material.shader;

            if (shader == null)
            {
                Debug.Log($"AssetsCreator::CreateMissingTexturesAsset unable to create asset for {material.name} cause shader is missing");
                return;
            }

            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                ShaderPropertyType propertyType = shader.GetPropertyType(i);

                if (propertyType != ShaderPropertyType.Texture)
                    continue;

                TextureDimension textureDimension = shader.GetPropertyTextureDimension(i);

                if (textureDimension != TextureDimension.Tex2D)
                    continue;

                Texture2D texture = material.GetTexture(shader.GetPropertyName(i)) as Texture2D;

                if (texture == null)
                    continue;

                if (ResourcesUtil.IsAssetInProject(texture))
                    continue;

                string texturePath;
                string textureName = shader.GetPropertyName(i);

                if (ResourcesUtil.SaveTextureAsPNG(texture, relativePath, texture.name + "_" + texture.GetInstanceID(), out texturePath))
                {
                    _delayedCall += () => LoadAndAssignTexture(material, texturePath, textureName);
                }
                else
                    Debug.Log($"AssetsExporter::failed to create texture at {relativePath}");
            }
        }
    }

#endif
}
