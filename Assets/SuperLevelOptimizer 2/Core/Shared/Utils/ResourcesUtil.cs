using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR

using UnityEditor;

#endif

using Object = UnityEngine.Object;

namespace NGS.SLO.Shared
{
#if UNITY_EDITOR

    public static class ResourcesUtil
    {
        public static bool IsAssetInProject(Object obj)
        {
            if (obj == null)
            {
                Debug.Log("ResourcesUtil::IsAssetInProject 'obj' is null");
                return false;
            }

            string path = AssetDatabase.GetAssetPath(obj);

            return !string.IsNullOrEmpty(path);
        }


        public static bool SaveTextureAsPNG(Texture2D texture, string relativePath, string textureName, out string assetPath)
        {
            assetPath = "";

            try
            {
                if (texture == null)
                {
                    Debug.Log("ResourcesUtil::SaveTextureAsPNG 'texture' is null");
                    return false;
                }

                if (!PathUtil.TryGetProjectRelative(relativePath, out relativePath))
                {
                    Debug.Log($"ResourcesUtil::unable to create texture at path: {relativePath}");
                    return false;
                }

                if (!(texture.isReadable) || !(texture.format == TextureFormat.RGBA32))
                    texture = TextureUtil.GetTextureUncompressed(texture);

                byte[] pngData = texture.EncodeToPNG();

                if (pngData == null)
                {
                    Debug.Log($"ResourcesUtil::SaveTextureAsPNG unable encode texture {texture.name} to .png");
                    return false;
                }

                if (!CreateFolderIfNotExists(relativePath))
                {
                    Debug.Log($"ResourcesUtil::unable to create folder at path {relativePath}");
                    return false;
                }

                assetPath = BuildAssetPath(relativePath, textureName, ".png");

                File.WriteAllBytes(assetPath, pngData);

                // AssetDatabase.ImportAsset(assetPath);

                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"ResourcesUtil::error during save texture {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public static bool SaveMaterialAsAsset(Material material, string relativePath, string materialName)
        {
            try
            {
                if (material == null)
                {
                    Debug.Log("ResourcesUtil::SaveMaterialAsAsset 'material' is null");
                    return false;
                }

                if (!PathUtil.TryGetProjectRelative(relativePath, out relativePath))
                {
                    Debug.Log($"ResourcesUtil::SaveMaterialAsAsset unable to create material at path: {relativePath}");
                    return false;
                }

                if (!CreateFolderIfNotExists(relativePath))
                {
                    Debug.Log($"ResourcesUtil::SaveMaterialAsAsset unable to create folder at path: {relativePath}");
                    return false;
                }

                string assetPath = BuildAssetPath(relativePath, materialName, ".mat");

                AssetDatabase.CreateAsset(material, assetPath);

                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"ResourcesUtil::error during save material {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public static bool SaveMeshAsAsset(Mesh mesh, string relativePath, string meshName)
        {
            try
            {
                if (mesh == null)
                {
                    Debug.Log("ResourcesUtil::SaveMeshAsAsset 'mesh' is null");
                    return false;
                }

                if (!PathUtil.TryGetProjectRelative(relativePath, out relativePath))
                {
                    Debug.Log($"ResourcesUtil::SaveMeshAsAsset unable to create mesh at path: {relativePath}");
                    return false;
                }

                if (!CreateFolderIfNotExists(relativePath))
                {
                    Debug.Log($"ResourcesUtil::unable to create folder at path: {relativePath}");
                    return false;
                }

                string assetPath = BuildAssetPath(relativePath, meshName, ".asset");

                AssetDatabase.CreateAsset(mesh, assetPath);

                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"ResourcesUtil::error during save mesh {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }


        private static string BuildAssetPath(string relativePath, string name, string format)
        {
            return $"{relativePath}{name}{format}";
        }

        private static bool CreateFolderIfNotExists(string relativePath)
        {
            relativePath = relativePath.Remove(0, "Assets".Length);

            string folderPath = $"{Application.dataPath}{relativePath}";

            if (!Directory.Exists(folderPath))
                return Directory.CreateDirectory(folderPath).Exists;

            return true;
        }
    }

#endif
}
