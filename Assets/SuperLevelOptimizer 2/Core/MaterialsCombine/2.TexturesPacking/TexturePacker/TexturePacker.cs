using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using NGS.SLO.Shared;

namespace NGS.SLO.MaterialsCombine
{
    public class TexturePacker : ITexturePacker
    {
        public Texture2D PackTextures(Texture2D[] textures, int padding, int maxAtlasSize, out Rect[] rects)
        {
            if (textures == null || textures.Length == 0)
                throw new NullReferenceException("TexturePacker::'textures' is empty");

            Texture2D[] uncompressedTextures = new Texture2D[textures.Length];
            int maxDimension = int.MinValue;

            for (int i = 0; i < textures.Length; i++)
            {
                uncompressedTextures[i] = LoadUncompressedTexture(textures[i]);
                maxDimension = Mathf.Max(maxDimension, textures[i].width, textures[i].height);
            }
            
            maxDimension = Mathf.NextPowerOfTwo(maxDimension); 

            Vector2 atlasSize = new Vector2(
                Mathf.Min(maxDimension, maxAtlasSize),
                Mathf.Min(maxDimension, maxAtlasSize));

            Rect[] placements = null;

            while (atlasSize.x <= maxAtlasSize || atlasSize.y <= maxAtlasSize)
            {
                if (TryPlaceTextures(textures, atlasSize, out placements))
                {
                    Texture2D atlas = CreateAtlasTexture(atlasSize);

                    PlaceTexturesToAtlas(atlas, uncompressedTextures, placements, padding);

                    ConvertPlacementsToUV(ref placements, atlasSize, padding);

                    rects = placements;

                    return atlas;
                }

                if (atlasSize.x <= atlasSize.y)
                    atlasSize.x *= 2;
                else
                    atlasSize.y *= 2;
            }

            throw new ArgumentOutOfRangeException("TexturePacker::It is unable to fit all textures into an atlas of a given size.");
        }


        private Texture2D LoadUncompressedTexture(Texture2D source)
        {
            if (source.isReadable && source.format == TextureFormat.RGBA32)
                return source;

            return TextureUtil.GetTextureUncompressed(source);
        }

        private Texture2D CreateAtlasTexture(Vector2 atlasSize)
        {
            Texture2D atlas = new Texture2D((int)atlasSize.x, (int)atlasSize.y, TextureFormat.RGBA32, true);

            atlas.wrapMode = TextureWrapMode.Clamp;

            return atlas;
        }

        private bool TryPlaceTextures(Texture2D[] textures, Vector2 atlasSize, out Rect[] placements)
        {
            TexturePlacer texturePlacer = new TexturePlacer((int)atlasSize.x, (int)atlasSize.y);

            List<int> sortedIndices = new List<int>();

            for (int i = 0; i < textures.Length; i++)
                sortedIndices.Add(i);

            sortedIndices.Sort((a, b) =>
                (textures[b].width * textures[b].height).CompareTo(textures[a].width * textures[a].height));

            placements = new Rect[textures.Length];

            foreach (int index in sortedIndices)
            {
                Rect placement;

                if (!texturePlacer.TryInsert(textures[index].width, textures[index].height, out placement))
                    return false;

                placements[index] = placement;
            }

            return true;
        }

        private void PlaceTexturesToAtlas(Texture2D atlas, Texture2D[] textures, Rect[] placements, int padding)
        {
            NativeArray<Color32> atlasPixels = atlas.GetPixelData<Color32>(0);

            new FillPixelsBlackColorJob()
            {

                atlasPixels = atlasPixels

            }.Schedule(atlasPixels.Length, 64).Complete();

            JobHandle handles = new JobHandle();

            for (int i = 0; i < textures.Length; i++)
            {
                JobHandle handle = PlaceTextureToAtlas(
                    atlas,
                    atlasPixels,
                    textures[i],
                    placements[i],
                    padding);

                handles = JobHandle.CombineDependencies(handles, handle);
            }

            handles.Complete();

            atlas.Apply(true, false);
        }

        private JobHandle PlaceTextureToAtlas(Texture2D atlas, NativeArray<Color32> atlasPixels, Texture2D texture, Rect placement, int padding)
        {
            Rect innerRect = GetInnerRect(placement, padding);

            NativeArray<Color32> texturePixels = texture.GetPixelData<Color32>(0);

            JobHandle handle = new ApplyTextureToAtlasJob()
            {

                atlasPixels = atlasPixels,
                atlasWidth = atlas.width,

                texturePixels = texturePixels,
                textureWidth = texture.width,
                textureHeight = texture.height,
                innerRect = innerRect

            }.Schedule((int)(innerRect.width * innerRect.height), 64);

            if (innerRect.width == placement.width)
                return handle;

            JobHandle fillHandle = new FillPaddingJob()
            {

                atlasPixels = atlasPixels,
                atlasWidth = atlas.width,
                padding = padding,
                innerRect = innerRect

            }.Schedule(handle);

            return JobHandle.CombineDependencies(handle, fillHandle); 
        }

        private void ConvertPlacementsToUV(ref Rect[] placements, Vector2 atlasSize, int padding)
        {
            for (int i = 0; i < placements.Length; i++)
            {
                Rect placement = placements[i];
                Rect innerRect = GetInnerRect(placement, padding);

                placements[i] = new Rect(
                    innerRect.x / atlasSize.x,
                    innerRect.y / atlasSize.y,
                    innerRect.width / atlasSize.x,
                    innerRect.height / atlasSize.y);
            }
        }

        private Rect GetInnerRect(Rect placement, int padding)
        {
            if (placement.width <= padding * 2 || placement.height <= padding * 2)
                return placement;

            return new Rect(
                placement.x + padding,
                placement.y + padding,
                placement.width - padding * 2,
                placement.height - padding * 2);
        }


        private void FillEmptyPixels(Texture2D atlas, Texture2D[] textures, Rect[] placements)
        {
            for (int x = 0; x < atlas.width; x++)
            {
                for (int y = 0; y < atlas.height; y++)
                {
                    float closestDist = float.MaxValue;
                    Color c = Color.clear;

                    for (int r = 0; r < placements.Length; ++r)
                    {
                        Rect curRect = placements[r];
                        if (curRect.Contains(new Vector2(x, y)))
                        {
                            closestDist = -1;
                            break;
                        }

                        int d = (int)DistanceToRect(curRect, x, y);

                        if (d < closestDist)
                        {
                            closestDist = d;

                            float uvX = (x - curRect.x) / curRect.width;
                            float uvY = (y - curRect.y) / curRect.height;

                            c = textures[r].GetPixelBilinear(uvX, uvY);
                        }
                    }

                    if (closestDist > -1)
                        atlas.SetPixel(x, y, c);
                }
            }
        }

        private float DistanceToRect(Rect r, int x, int y)
        {
            float xDist = Mathf.Max(Mathf.Abs(x - r.center.x) - r.width / 2, 0);
            float yDist = Mathf.Max(Mathf.Abs(y - r.center.y) - r.height / 2, 0);

            return xDist * xDist + yDist * yDist;
        }



        [BurstCompile]
        private struct FillPixelsBlackColorJob : IJobParallelFor
        {
            [WriteOnly]
            public NativeArray<Color32> atlasPixels;

            public void Execute(int index)
            {
                atlasPixels[index] = new Color32(0, 0, 0, 0);
            }
        }

        [BurstCompile]
        private struct ApplyTextureToAtlasJob : IJobParallelFor
        {
            [WriteOnly]
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<Color32> atlasPixels;
            public int atlasWidth;

            [ReadOnly]
            public NativeArray<Color32> texturePixels;
            public int textureWidth;
            public int textureHeight;

            public Rect innerRect;

            public void Execute(int index)
            {
                int width = (int)innerRect.width;

                int xLocal = index % width;
                int yLocal = index / width;

                float u = xLocal / (float)(width - 1);
                float v = yLocal / (float)((int)innerRect.height - 1);

                Color32 color = GetPixelBilinear(texturePixels, textureWidth, textureHeight, u, v);

                int x = (int)innerRect.x + xLocal;
                int y = (int)innerRect.y + yLocal;

                atlasPixels[y * atlasWidth + x] = color;
            }

            private Color32 GetPixelBilinear(NativeArray<Color32> pixels, int width, int height, float u, float v)
            {
                u = Mathf.Clamp01(u);
                v = Mathf.Clamp01(v);

                float x = u * (width - 1);
                float y = v * (height - 1);

                int xMin = Mathf.FloorToInt(x);
                int yMin = Mathf.FloorToInt(y);
                int xMax = Mathf.Min(xMin + 1, width - 1);
                int yMax = Mathf.Min(yMin + 1, height - 1);

                float dx = x - xMin;
                float dy = y - yMin;

                Color32 c00 = pixels[yMin * width + xMin]; // нижний левый
                Color32 c10 = pixels[yMin * width + xMax]; // нижний правый
                Color32 c01 = pixels[yMax * width + xMin]; // верхний левый
                Color32 c11 = pixels[yMax * width + xMax]; // верхний правый

                Color32 c0 = Color32.Lerp(c00, c10, dx);
                Color32 c1 = Color32.Lerp(c01, c11, dx);

                return Color32.Lerp(c0, c1, dy);
            }
        }

        [BurstCompile]
        private struct FillPaddingJob : IJob
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<Color32> atlasPixels;
            public int atlasWidth;

            public int padding;
            public Rect innerRect;

            public void Execute()
            {
                for (int x = (int)innerRect.x; x < innerRect.x + innerRect.width; x++)
                {
                    Color topColor = GetPixel(x, (int)innerRect.y);
                    Color bottomColor = GetPixel(x, (int)(innerRect.y + innerRect.height - 1));

                    int top = (int)innerRect.y;
                    int bottom = (int)(innerRect.y + innerRect.height - 1);

                    for (int py = 1; py <= padding; py++)
                    {
                        SetPixel(x, top - py, topColor);
                        SetPixel(x, bottom + py, bottomColor);
                    }
                }

                for (int y = (int)innerRect.y; y < innerRect.y + innerRect.height; y++)
                {
                    Color leftColor = GetPixel((int)innerRect.x, y);
                    Color rightColor = GetPixel((int)(innerRect.x + innerRect.width - 1), y);

                    for (int px = 1; px <= padding; px++)
                    {
                        SetPixel((int)innerRect.x - px, y, leftColor);
                        SetPixel((int)(innerRect.x + innerRect.width - 1 + px), y, rightColor);
                    }
                }

                Color topLeft = GetPixel((int)innerRect.x, (int)innerRect.y);
                Color topRight = GetPixel((int)(innerRect.x + innerRect.width - 1), (int)innerRect.y);
                Color bottomLeft = GetPixel((int)innerRect.x, (int)(innerRect.y + innerRect.height - 1));
                Color bottomRight = GetPixel((int)(innerRect.x + innerRect.width - 1), (int)(innerRect.y + innerRect.height - 1));

                for (int px = 1; px <= padding; px++)
                {
                    for (int py = 1; py <= padding; py++)
                    {
                        SetPixel((int)innerRect.x - px, (int)innerRect.y - py, topLeft);
                        SetPixel((int)(innerRect.x + innerRect.width - 1 + px), (int)innerRect.y - py, topRight);
                        SetPixel((int)innerRect.x - px, (int)(innerRect.y + innerRect.height - 1 + py), bottomLeft);
                        SetPixel((int)(innerRect.x + innerRect.width - 1 + px), (int)(innerRect.y + innerRect.height - 1 + py), bottomRight);
                    }
                }
            }

            private Color GetPixel(int x, int y)
            {
                return atlasPixels[y * atlasWidth + x];
            }

            private void SetPixel(int x, int y, Color pixel)
            {
                atlasPixels[y * atlasWidth + x] = pixel;
            }
        }
    }
}

//private void FillEmptyPixels(Texture2D atlas, Texture2D[] textures, Rect[] placements, IReadOnlyList<Rect> freeRectangles)
//{
//    foreach (var freeRect in freeRectangles)
//    {
//        int startX = (int)Mathf.Min(freeRect.x, atlas.width);
//        int endX = (int)Mathf.Min(freeRect.x + freeRect.width, atlas.width);

//        int startY = (int)Mathf.Min(freeRect.y, atlas.height);
//        int endY = (int)Mathf.Min(freeRect.y + freeRect.height, atlas.height);

//        for (int x = startX; x < endX; x++)
//        {
//            for (int y = startY; y < endY; y++)
//            {
//                float closestDist = float.MaxValue;
//                Color c = Color.clear;

//                for (int r = 0; r < placements.Length; r++)
//                {
//                    Rect curRect = placements[r];

//                    int d = (int)DistanceToRect(curRect, x, y);

//                    if (d < closestDist)
//                    {
//                        closestDist = d;

//                        float uvX = (x - curRect.x) / curRect.width;
//                        float uvY = (y - curRect.y) / curRect.height;

//                        c = textures[r].GetPixelBilinear(uvX, uvY);
//                    }
//                }

//                atlas.SetPixel(x, y, c);
//            }
//        }
//    }
//}

//private float DistanceToRect(Rect r, int x, int y)
//{
//    float xDist = Mathf.Max(Mathf.Abs(x - r.center.x) - r.width / 2, 0);
//    float yDist = Mathf.Max(Mathf.Abs(y - r.center.y) - r.height / 2, 0);

//    return xDist * xDist + yDist * yDist;
//}
