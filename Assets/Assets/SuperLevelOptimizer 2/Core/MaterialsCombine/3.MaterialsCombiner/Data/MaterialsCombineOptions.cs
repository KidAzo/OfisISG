using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
    [Serializable]
    public struct MaterialsCombineOptions
    {
        public static MaterialsCombineOptions Default
        {
            get
            {
                return new MaterialsCombineOptions()
                {
                    maxAtlasSize = 2048,
                    maxTileTextureUpscaledSize = 1024,
                    maxTileTextureDownscale = 4,
                    padding = 4,
                    fillEmptyTextures = true
                };
            }
        }

        public int maxAtlasSize;
        public int maxTileTextureUpscaledSize;
        public int maxTileTextureDownscale;
        public int padding;
        public bool fillEmptyTextures;
    }
}
