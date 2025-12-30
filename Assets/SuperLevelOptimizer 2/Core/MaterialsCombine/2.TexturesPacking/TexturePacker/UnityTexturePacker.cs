using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
    public class UnityTexturePacker : ITexturePacker
    {
        public Texture2D PackTextures(Texture2D[] textures, int padding, int maxAtlasSize, out Rect[] rects)
        {
            Texture2D result = new Texture2D(0, 0);

            rects = result.PackTextures(textures, padding, maxAtlasSize);

            result.Apply();

            return result;
        }
    }
}
