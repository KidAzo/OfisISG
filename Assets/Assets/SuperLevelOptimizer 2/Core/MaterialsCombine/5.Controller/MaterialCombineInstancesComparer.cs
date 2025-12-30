using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
    public static class MaterialCombineInstancesComparer
    {
        public static bool HasEqualParameters(
            ShaderCombineInfo shaderInfo,
            MaterialsCombineOptions combineOptions,
            MaterialCombineInstance instanceA,
            MaterialCombineInstance instanceB)
        {
            if (instanceA.Material.shader != instanceB.Material.shader)
                return false;

            foreach (var property in shaderInfo.ShaderProperties)
            {
                if (property.ignore)
                    continue;

                if (property.kind == ShaderPropertyKind.Texture2D)
                {
                    if (!Compare2DTextureProperty(property, instanceA, instanceB, combineOptions.fillEmptyTextures))
                        return false;
                }
                else if (property.kind == ShaderPropertyKind.Texture)
                {
                    if (!CompareTextureProperty(property, instanceA, instanceB))
                        return false;
                }
                else if (property.kind == ShaderPropertyKind.Float)
                {
                    if (!CompareFloatProperty(property, instanceA, instanceB))
                        return false;
                }
                else if (property.kind == ShaderPropertyKind.Color)
                {
                    if (!CompareColorProperty(property, instanceA, instanceB))
                        return false;
                }
                else if (property.kind == ShaderPropertyKind.Vector)
                {
                    if (!CompareVectorProperty(property, instanceA, instanceB))
                        return false;
                }
            }

            return true;
        }

        private static bool Compare2DTextureProperty(
            ShaderProperty property, 
            MaterialCombineInstance instanceA, 
            MaterialCombineInstance instanceB,
            bool fillEmptyTextures)
        {
            Texture textureA = instanceA.Material.GetTexture(property.name);
            Texture textureB = instanceB.Material.GetTexture(property.name);

            if (textureA == textureB)
                return true;

            if (!property.combine2DTexture)
                return false;

            if (textureA == null || textureB == null)
            {
                if (!fillEmptyTextures)
                    return false;
            }

            return true;
        }

        private static bool CompareTextureProperty(
            ShaderProperty property,
            MaterialCombineInstance instanceA,
            MaterialCombineInstance instanceB)
        {
            Texture textureA = instanceA.Material.GetTexture(property.name);
            Texture textureB = instanceB.Material.GetTexture(property.name);

            return textureA == textureB;
        }

        private static bool CompareFloatProperty(
            ShaderProperty property,
            MaterialCombineInstance instanceA,
            MaterialCombineInstance instanceB)
        {
            float floatA = instanceA.Material.GetFloat(property.name);
            float floatB = instanceB.Material.GetFloat(property.name);

            if (Mathf.Abs(floatA - floatB) > (property.threshold + Mathf.Epsilon))
                return false;

            return true;
        }

        private static bool CompareColorProperty(
            ShaderProperty property,
            MaterialCombineInstance instanceA,
            MaterialCombineInstance instanceB)
        {
            Color colorA = instanceA.Material.GetColor(property.name);
            Color colorB = instanceB.Material.GetColor(property.name);

            if (Mathf.Abs(colorA.r - colorB.r) > (property.threshold + Mathf.Epsilon))
                return false;

            if (Mathf.Abs(colorA.g - colorB.g) > (property.threshold + Mathf.Epsilon))
                return false;

            if (Mathf.Abs(colorA.b - colorB.b) > (property.threshold + Mathf.Epsilon))
                return false;

            if (Mathf.Abs(colorA.a - colorB.a) > (property.threshold + Mathf.Epsilon))
                return false;

            return true;
        }

        private static bool CompareVectorProperty(
          ShaderProperty property,
          MaterialCombineInstance instanceA,
          MaterialCombineInstance instanceB)
        {
            Vector4 vectorA = instanceA.Material.GetVector(property.name);
            Vector4 vectorB = instanceB.Material.GetVector(property.name);

            if (Mathf.Abs(vectorA.x - vectorB.x) > (property.threshold + Mathf.Epsilon))
                return false;

            if (Mathf.Abs(vectorA.y - vectorB.y) > (property.threshold + Mathf.Epsilon))
                return false;

            if (Mathf.Abs(vectorA.z - vectorB.z) > (property.threshold + Mathf.Epsilon))
                return false;

            if (Mathf.Abs(vectorA.w - vectorB.w) > (property.threshold + Mathf.Epsilon))
                return false;

            return true;
        }
    }
}
