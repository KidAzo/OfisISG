using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NGS.SLO.Shared;

namespace NGS.SLO.MaterialsCombine
{
    public class MaterialsCombiner
    {
        private ITexturePacker _texturePacker;


        public MaterialsCombiner(ITexturePacker texturePacker)
        {
            _texturePacker = texturePacker;
        }

        public MaterialCombineResult Combine(IReadOnlyList<MaterialCombineInstance> instances, MaterialsCombineOptions combineOptions)
        {
            try
            {
                if (instances == null || instances.Count == 0)
                    return new MaterialCombineResult("Tried to combine empty arguments");

                ShaderCombineInfo shaderInfo = instances[0].ShaderInfo;

                if (shaderInfo == null)
                    return new MaterialCombineResult("Empty ShaderInfo in first instance");

                for (int i = 0; i < instances.Count; i++)
                {
                    if (!instances[i].ReadyForCombine)
                        return new MaterialCombineResult(instances[i].Material.name + " not ready for combine");
                }

                Material combinedMaterial = new Material(instances[0].Material);
                combinedMaterial.name = CreateCombinedMaterialName(instances);

                Rect[] uvs = null;
                Texture2D[] layerTextures = new Texture2D[instances.Count];

                foreach (var property in shaderInfo.ShaderProperties)
                {
                    if (property.kind != ShaderPropertyKind.Texture2D)
                        continue;

                    if (property.ignore || !property.combine2DTexture)
                        continue;

                    if (!AnyInstanceHasTexture(property, instances))
                        continue;

                    GatherAndPrepareLayerTextures(property, instances, layerTextures, combineOptions.fillEmptyTextures);

                    Texture2D atlas = _texturePacker.PackTextures(layerTextures, combineOptions.padding, combineOptions.maxAtlasSize, out uvs);
                    atlas.name = CreateAtlasName(layerTextures);

                    combinedMaterial.SetTexture(property.name, atlas);
                    combinedMaterial.SetTextureScale(property.name, Vector2.one);
                    combinedMaterial.SetTextureOffset(property.name, Vector2.zero);
                }

                return new MaterialCombineResult(combinedMaterial, instances.ToArray(), uvs);
            }
            catch (Exception ex)
            {
                Debug.LogError($"MaterialCombiner::Error occured while combine materials : {ex.Message}\n{ex.StackTrace}");
                return new MaterialCombineResult($"{ex.Message}\n{ex.StackTrace}");
            }
        }


        private string CreateCombinedMaterialName(IReadOnlyList<MaterialCombineInstance> instances)
        {
            string name = "";

            int count = Mathf.Min(3, instances.Count);

            for (int i = 0; i < count; i++)
            {
                string materialName = instances[i].Material.name;

                if (string.IsNullOrEmpty(materialName))
                    materialName = Mathf.Abs(instances[i].Material.GetHashCode()).ToString();

                if (materialName.Length > 5)
                    materialName = materialName.Remove(5);

                name += $"{materialName}_";
            }

            name = name.Remove(name.Length - 1);

            return name;
        }

        private string CreateAtlasName(Texture2D[] textures)
        {
            string name = "";

            int count = Mathf.Min(3, textures.Length);

            for (int i = 0; i < count; i++)
            {
                string textureName = textures[i].name;

                if (string.IsNullOrEmpty(textureName))
                    textureName = Mathf.Abs(textures[i].GetHashCode()).ToString();

                if (textureName.Length > 5)
                    textureName = textureName.Remove(5);

                name += $"{textureName}_";
            }

            name = name.Remove(name.Length - 1);

            return name;
        }

        private bool AnyInstanceHasTexture(ShaderProperty property, IReadOnlyList<MaterialCombineInstance> instances)
        {
            string propertyName = property.name;

            foreach (var instance in instances)
            {
                if (instance.Material.GetTexture(propertyName) != null)
                    return true;
            }

            return false;
        }

        private void GatherAndPrepareLayerTextures(ShaderProperty property, IReadOnlyList<MaterialCombineInstance> instances, Texture2D[] result, bool fillEmptyTextures)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                MaterialCombineInstance instance = instances[i];
                Texture2D texture = instance.Material.GetTexture(property.name) as Texture2D;

                if (texture == null)
                {
                    if (!fillEmptyTextures)
                        throw new ArgumentException($"MaterialCombiner::{instance.Material.name} missed texture {property.name} and 'fillEmptyTextures' disabled");

                    texture = CreateEmptyTexture(property);
                }

                Vector2 textureScale = MaterialUtil.GetTextureScale(instance.Material, property.name);
                Vector2 textureOffset = MaterialUtil.GetTextureOffset(instance.Material, property.name);

                Vector2Int textureRepeats = instance.CalculateTextureRepeats(textureScale, textureOffset);
                Vector2Int textureSize = new Vector2Int((int)instance.Data.MainTextureTargetSize.x, (int)instance.Data.MainTextureTargetSize.y);

                if (textureRepeats.x < 1 || textureRepeats.y < 1 || textureRepeats.x > instance.Data.MainTextureRepeats.x || textureRepeats.y > instance.Data.MainTextureRepeats.y)
                    textureRepeats = instance.Data.MainTextureRepeats;

                result[i] = PrepareTexture(texture, textureRepeats, textureSize);
            }
        }

        private Texture2D PrepareTexture(Texture2D texture, Vector2Int repeats, Vector2Int size)
        {
            return TextureUtil.UncompressRepeatResize(texture, size.x, size.y, repeats.x, repeats.y);

            //if (repeats.x != 1 || repeats.y != 1 || texture.width != size.x || texture.height != size.y)
            //{
            //    if (repeats.x == 1 && repeats.y == 1)
            //    {
            //        texture = TextureUtil.Resize(texture, size.x, size.y);
            //    }
            //    else
            //    {                    
            //        texture = TextureUtil.Resize(texture, size.x / repeats.x, size.y / repeats.y);
            //        texture = TextureUtil.Repeat(texture, repeats.x, repeats.y);
            //    }
            //}

            //return texture;
        }

        private Texture2D CreateEmptyTexture(ShaderProperty property)
        {
            string nameLower = property.name.ToLower();

            Color color = Color.black;

            if (nameLower.Contains("normal"))
            {
                color = new Color(0.5f, 0.5f, 1f, 1f);
            }
            else if (nameLower.Contains("occlusion") || nameLower.Contains("ao"))
            {
                color = Color.white;
            }
            else if (nameLower.Contains("albedo") || nameLower.Contains("basecolor")
                     || nameLower.Contains("maintex") || nameLower.Contains("diffuse"))
            {
                color = Color.white;
            }
            else if (nameLower.Contains("roughness") || nameLower.Contains("rgh"))
            {
                color = Color.white;
            }
            else if (nameLower.Contains("height") || nameLower.Contains("displacement"))
            {
                color = Color.black;
            }
            else if (nameLower.Contains("detail") || nameLower.Contains("detailmask"))
            {
                color = Color.white;
            }
            else if (nameLower.Contains("opacity") || nameLower.Contains("alpha"))
            {
                color = Color.white;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();

            return texture;
        }
    }
}
