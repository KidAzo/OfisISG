using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
    public interface ITexturePacker
    {
        public Texture2D PackTextures(Texture2D[] textures, int padding, int maxAtlasSize, out Rect[] rects);
    }
}