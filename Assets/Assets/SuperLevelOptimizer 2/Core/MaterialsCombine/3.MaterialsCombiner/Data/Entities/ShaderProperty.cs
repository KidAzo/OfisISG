using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
    public enum ShaderPropertyKind
    {
        Texture2D,
        Texture,
        Float,
        Color,
        Vector,
        Undefined
    }

    [Serializable]
    public struct ShaderProperty 
    {
        public ShaderPropertyKind kind;
        public string name;
        public float threshold;

        public bool combine2DTexture;
        public bool ignore;

        public ShaderProperty(ShaderPropertyKind kind, string name) 
            : this(kind, name, 0)
        {

        }

        public ShaderProperty(ShaderPropertyKind kind, string name, float threshold) 
            : this(kind, name, threshold, true, false)
        {

        }

        public ShaderProperty(ShaderPropertyKind kind, string name, float threshold, bool combine2DTexture, bool ignore)
        {
            this.kind = kind;
            this.name = name;
            this.threshold = threshold;

            this.combine2DTexture = combine2DTexture;
            this.ignore = ignore;
        }
    }
}
