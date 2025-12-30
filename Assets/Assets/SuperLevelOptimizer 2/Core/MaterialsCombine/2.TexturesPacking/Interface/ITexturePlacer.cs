using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
    public interface ITexturePlacer
    {
        public int MaxWidth { get; }
        public int MaxHeight { get; }

        public bool CanInsert(int width, int height);

        public bool TryInsert(int width, int height, out Rect resultRect);
    }
}
