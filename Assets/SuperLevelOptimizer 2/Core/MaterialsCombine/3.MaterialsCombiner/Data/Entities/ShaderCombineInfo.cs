using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.SLO.MaterialsCombine
{
    [Serializable]
    public class ShaderCombineInfo
    {
        public bool AllowedCombine 
        {
            get
            {
                return _allowedCombine;
            }
            set
            {
                _allowedCombine = value;
            }
        }
        public Shader Shader
        {
            get
            {
                return _shader;
            }
        }
        public IReadOnlyList<ShaderProperty> ShaderProperties
        {
            get
            {
                return _shaderProperties;
            }
        }
        public string MainTextureName
        {
            get
            {
                if (_mainTextureIndex < 0)
                    return "";

                return _shaderProperties[_mainTextureIndex].name;
            }
        }

        [SerializeField]
        private bool _allowedCombine;

        [SerializeField]
        private Shader _shader;

        [SerializeField]
        private List<ShaderProperty> _shaderProperties;

        [SerializeField]
        private int _mainTextureIndex;


        public ShaderCombineInfo(Shader shader)
        {
            _shader = shader;

            ResetProperties();
        }

        public void SetPropertyValues(ShaderProperty property, int propertyIndex)
        {
            if (propertyIndex < 0 || propertyIndex >= _shaderProperties.Count)
                throw new IndexOutOfRangeException("PropertyIndex is out of range");

            if (property.name != _shaderProperties[propertyIndex].name)
                throw new ArgumentException("Properties Name Mismatch");

            if (property.kind != _shaderProperties[propertyIndex].kind)
                throw new ArgumentException("Properties Kind Mismatch");

            _shaderProperties[propertyIndex] = property;
        }

        public void SetMainTexture(string textureName)
        {
            for (int i = 0; i < _shaderProperties.Count; i++)
            {
                ShaderProperty property = _shaderProperties[i];

                if (property.name == textureName)
                {
                    if (property.kind != ShaderPropertyKind.Texture2D)
                        throw new ArgumentException("MainTexture should be Texture2D");

                    _mainTextureIndex = i;

                    return;
                }
            }

            throw new ArgumentException($"Texture with name {textureName} not found");
        }

        public void ResetProperties()
        {
            _allowedCombine = true;

            _mainTextureIndex = -1;

            GatherShaderInfo(_shader);
        }


        private void GatherShaderInfo(Shader shader)
        {
            int count = shader.GetPropertyCount();

            _shaderProperties = new List<ShaderProperty>();

            for (int i = 0; i < count; i++)
            {
                string propName = shader.GetPropertyName(i);

                ShaderPropertyType propType = shader.GetPropertyType(i);
                ShaderPropertyKind propKind = ShaderPropertyKind.Undefined;

                if (propType == ShaderPropertyType.Texture)
                {
                    TextureDimension texDimension = shader.GetPropertyTextureDimension(i);

                    if (texDimension == TextureDimension.Tex2D)
                    {
                        propKind = ShaderPropertyKind.Texture2D;

                        if (_mainTextureIndex < 0)
                            _mainTextureIndex = i;
                    }
                    else
                    {
                        propKind = ShaderPropertyKind.Texture;
                    }

                }
                else if (propType == ShaderPropertyType.Float || propType == ShaderPropertyType.Range)
                {
                    propKind = ShaderPropertyKind.Float;
                }
                else if (propType == ShaderPropertyType.Color)
                {
                    propKind = ShaderPropertyKind.Color;
                }
                else if (propType == ShaderPropertyType.Vector)
                {
                    propKind = ShaderPropertyKind.Vector;
                }

                _shaderProperties.Add(new ShaderProperty(propKind, propName));
            }
        }
    }
}
