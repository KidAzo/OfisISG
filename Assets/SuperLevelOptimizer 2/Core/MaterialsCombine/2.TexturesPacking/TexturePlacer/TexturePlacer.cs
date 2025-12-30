using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
    public class TexturePlacer : ITexturePlacer
    {
        public int MaxWidth { get; private set; }
        public int MaxHeight { get; private set; }

        public IReadOnlyList<Rect> FreeRectangles
        {
            get
            {
                return _freeRectangles;
            }
        }

        private List<Rect> _freeRectangles;


        public TexturePlacer(int maxWidth, int maxHeight)
        {
            MaxWidth = maxWidth;
            MaxHeight = maxHeight;
            
            _freeRectangles = new List<Rect>
            {
                new Rect(0, 0, maxWidth, maxHeight)
            };
        }

        public bool CanInsert(int width, int height)
        {
            Rect? bestCandidate = TryFindBestCandidate(width, height);

            return bestCandidate.HasValue;
        }

        public bool TryInsert(int width, int height, out Rect resultRect)
        {
            Rect? bestCandidate = TryFindBestCandidate(width, height);

            if (!bestCandidate.HasValue)
            {
                resultRect = default;
                return false;
            }

            resultRect = bestCandidate.Value;

            UpdateFreeRectangles(resultRect);

            return true;
        }


        private Rect? TryFindBestCandidate(int width, int height)
        {
            Rect? bestCandidate = null;

            int bestShortSideFit = int.MaxValue;
            int bestLongSideFit = int.MaxValue;

            foreach (var freeRect in _freeRectangles)
            {
                if (width <= freeRect.width && height <= freeRect.height)
                {
                    int remainingWidth = (int)freeRect.width - width;
                    int remainingHeight = (int)freeRect.height - height;
                    int shortSideFit = Mathf.Min(remainingWidth, remainingHeight);
                    int longSideFit = Mathf.Max(remainingWidth, remainingHeight);

                    if (shortSideFit < bestShortSideFit ||
                        (shortSideFit == bestShortSideFit && longSideFit < bestLongSideFit))
                    {
                        bestCandidate = new Rect(freeRect.x, freeRect.y, width, height);
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }
            }

            return bestCandidate;
        }

        private void UpdateFreeRectangles(Rect placedRect)
        {
            List<Rect> newFreeRectangles = new List<Rect>();

            foreach (var freeRect in _freeRectangles)
            {
                if (freeRect.Overlaps(placedRect))
                {
                    float rightWidth = (freeRect.x + freeRect.width) - (placedRect.x + placedRect.width);

                    if (rightWidth > 0)
                    {
                        Rect rightRect = new Rect(placedRect.x + placedRect.width, freeRect.y, rightWidth, freeRect.height);
                        newFreeRectangles.Add(rightRect);
                    }

                    float bottomHeight = (freeRect.y + freeRect.height) - (placedRect.y + placedRect.height);

                    if (bottomHeight > 0)
                    {
                        Rect bottomRect = new Rect(freeRect.x, placedRect.y + placedRect.height, freeRect.width, bottomHeight);
                        newFreeRectangles.Add(bottomRect);
                    }
                }
                else
                {
                    newFreeRectangles.Add(freeRect);
                }
            }

            _freeRectangles = RemoveRedundantRectangles(newFreeRectangles);
        }

        private List<Rect> RemoveRedundantRectangles(List<Rect> rectangles)
        {
            for (int i = 0; i < rectangles.Count; i++)
            {
                Rect a = rectangles[i];

                for (int j = i + 1; j < rectangles.Count; j++)
                {
                    Rect b = rectangles[j];

                    if (IsContainedIn(a, b))
                    {
                        rectangles.RemoveAt(i);
                        i--;
                        break;
                    }

                    if (IsContainedIn(b, a))
                    {
                        rectangles.RemoveAt(j);
                        j--;
                    }
                }
            }

            return rectangles;
        }

        private bool IsContainedIn(Rect a, Rect b)
        {
            return a.x >= b.x && a.y >= b.y &&
                   a.x + a.width <= b.x + b.width &&
                   a.y + a.height <= b.y + b.height;
        }
    }
}
