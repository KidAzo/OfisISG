using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

namespace NGS.SLO.Shared
{
    public static class TextureUtil 
    {
        private const int MAX_CACHE_SIZE_MB = 300;

        private static Dictionary<TextureOperation, Texture2D> _texturesCache;
        private static float _cacheSizeMb;


        public static Texture2D GetTextureUncompressed(Texture2D source)
        {
            if (source == null)
                throw new NullReferenceException("TexturesUtil::GetTextureUncompressed source texture is null");

            TextureOperation operation = new TextureOperation(source, OperationType.Uncompress, 0, 0);

            if (TryGetTextureFromCache(operation, out Texture2D cached))
                return cached;

            return SaveTextureInCache(operation, CopyTextureByBlit(source));
        }

        public static Texture2D Repeat(Texture2D source, int repeatX, int repeatY)
        {
            if (source == null)
                throw new NullReferenceException("TexturesUtil::Repeat source texture is null");

            if (!source.isReadable || source.format != TextureFormat.RGBA32)
                source = GetTextureUncompressed(source);

            TextureOperation operation = new TextureOperation(source, OperationType.Repeat, repeatX, repeatY);

            if (TryGetTextureFromCache(operation, out Texture2D cached))
                return cached;

            int rawWidth = source.width * repeatX;
            int rawHeight = source.height * repeatY;

            Texture2D repeatedTex = new Texture2D(rawWidth, rawHeight, TextureFormat.RGBA32, false);

            NativeArray<Color32> srcPixels = source.GetPixelData<Color32>(0);
            NativeArray<Color32> dstPixels = repeatedTex.GetPixelData<Color32>(0);

            var job = new TextureRepeatJob
            {
                sourcePixels = srcPixels,
                resultPixels = dstPixels,

                sourceWidth = source.width,
                sourceHeight = source.height,

                repeatX = repeatX,
                repeatY = repeatY
            };

            job.Schedule(rawWidth * rawHeight, 64).Complete();

            repeatedTex.Apply(false, false);

            return SaveTextureInCache(operation, repeatedTex);
        }

        public static Texture2D Resize(Texture2D source, int width, int height)
        {
            if (source == null)
                throw new NullReferenceException("TexturesUtil::Resize source texture is null");

            if (!source.isReadable || source.format != TextureFormat.RGBA32)
                source = GetTextureUncompressed(source);

            TextureOperation operation = new TextureOperation(source, OperationType.Resize, width, height);

            if (TryGetTextureFromCache(operation, out Texture2D cached))
                return cached;

            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);

            NativeArray<Color32> srcPixels = source.GetPixelData<Color32>(0);
            NativeArray<Color32> dstPixels = result.GetPixelData<Color32>(0);

            var job = new TextureResizeJob
            {
                sourcePixels = srcPixels,
                resultPixels = dstPixels,

                sourceWidth = source.width,
                sourceHeight = source.height,
                resultWidth = width,
                resultHeight = height
            };
            
            job.Schedule(width * height, 64).Complete();

            result.Apply(false, false);

            return SaveTextureInCache(operation, result);
        }

        public static Texture2D UncompressRepeatResize(Texture2D source, int width, int height, int repeatX, int repeatY)
        {
            if (source == null)
                throw new NullReferenceException("TexturesUtil::UncompressRepeatResize source texture is null");

            TextureOperation operation = new TextureOperation(source, OperationType.UncompressRepeatResize, width + repeatX, height + repeatY);

            if (TryGetTextureFromCache(operation, out Texture2D cached))
                return cached;

            if (!source.isReadable || source.format != TextureFormat.RGBA32)
                source = GetTextureUncompressed(source);

            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);

            NativeArray<Color32> srcPixels = source.GetPixelData<Color32>(0);
            NativeArray<Color32> dstPixels = result.GetPixelData<Color32>(0);

            var job = new TextureRepeatAndResizeJob
            {
                sourcePixels = srcPixels,
                sourceWidth = source.width,
                sourceHeight = source.height,
            
                resultPixels = dstPixels,
                resultWidth = width,
                resultHeight = height,
                repeatX = repeatX,
                repeatY = repeatY
            };

            job.Schedule(width * height, 64).Complete();

            result.Apply(false, false);

            return SaveTextureInCache(operation, result);
        }

        public static void ClearCache()
        {
            if (_texturesCache != null)
            {
                foreach (var texture in _texturesCache.Values)
                {
                    if (texture == null)
                        continue;

                    UnityEngine.Object.DestroyImmediate(texture);
                }

                _texturesCache.Clear();
            }

            _cacheSizeMb = 0f;

            GC.Collect();
        }


        private static Texture2D CopyTextureByBlit(Texture2D source)
        {
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);

            Graphics.Blit(source, rt);

            RenderTexture.active = rt;

            Texture2D uncompressedTex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            uncompressedTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            uncompressedTex.Apply(false, false);

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return uncompressedTex;
        }

        private static Texture2D CopyNormalMapByBlit(Texture2D source)
        {
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            Graphics.Blit(source, rt);

            RenderTexture.active = rt;

            Texture2D uncompressedTex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            uncompressedTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);

            Color32[] pixels = uncompressedTex.GetPixels32();
            Color32[] outPixels = new Color32[pixels.Length];

            for (int i = 0; i < pixels.Length; i++)
            {
                float nx = pixels[i].a / 255f * 2f - 1f;
                float ny = pixels[i].g / 255f * 2f - 1f;
                float nz = Mathf.Sqrt(Mathf.Clamp01(1f - nx * nx - ny * ny));

                byte r = (byte)((nx * 0.5f + 0.5f) * 255f);
                byte g = (byte)((ny * 0.5f + 0.5f) * 255f);
                byte b = (byte)((nz * 0.5f + 0.5f) * 255f);

                outPixels[i] = new Color32(r, g, b, 255);
            }

            uncompressedTex.SetPixels32(outPixels);
            uncompressedTex.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return uncompressedTex;
        }


        private static bool TryGetTextureFromCache(TextureOperation operation, out Texture2D result)
        {
            if (_texturesCache == null)
            {
                result = null;
                return false;
            }

            if (_texturesCache.TryGetValue(operation, out result))
                return true;

            return false;
        }

        private static Texture2D SaveTextureInCache(TextureOperation operation, Texture2D texture)
        {
            try
            {
                if (_texturesCache == null)
                    _texturesCache = new Dictionary<TextureOperation, Texture2D>();

                if (!_texturesCache.ContainsKey(operation))
                {
                    _texturesCache.Add(operation, texture);

                    _cacheSizeMb += (texture.width * texture.height * 4) / (1024f * 1024f);
                }

                return texture;
            }
            catch (Exception ex)
            {
                Debug.Log($"TexturesUtil::SaveTextureInCache error : {ex.Message} \n {ex.StackTrace}");

                return texture;
            }
        }


        private enum OperationType
        {
            Uncompress = 0,
            Offset = 1,
            Repeat = 2,
            Resize = 3,
            UncompressRepeatResize = 4
        }

        private struct TextureOperation
        {
            public Texture2D source;
            public OperationType operation;
            public float value1;
            public float value2;

            public TextureOperation(Texture2D source, OperationType operation, float value1, float value2)
            {
                this.source = source;
                this.operation = operation;
                this.value1 = value1;
                this.value2 = value2;
            }

            public override bool Equals(object obj)
            {
                if (obj is TextureOperation other)
                    return Equals(other);

                return false;
            }

            public bool Equals(TextureOperation other)
            {
                return (source == other.source)
                    && (operation == other.operation)
                    && Approximately(value1, other.value1)
                    && Approximately(value2, other.value2);
            }

            public override int GetHashCode()
            {
                int hash = 17;

                hash = hash * 31 + (source != null ? source.GetInstanceID() : 0);
                hash = hash * 31 + operation.GetHashCode();

                hash = hash * 31 + value1.GetHashCode();
                hash = hash * 31 + value2.GetHashCode();

                return hash;
            }


            private static bool Approximately(float a, float b)
            {
                return Mathf.Abs(a - b) < 0.0001f;
            }
        }


        [BurstCompile]
        private struct TextureOffsetJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<Color32> sourcePixels;
            public int sourceWidth;
            public int sourceHeight;

            [WriteOnly]
            public NativeArray<Color32> resultPixels;

            public float2 offset;

            public void Execute(int index)
            {
                int x = index % sourceWidth;
                int y = index / sourceWidth;

                float2 uv = new float2(
                    (float)x / sourceWidth + offset.x,
                    (float)y / sourceHeight + offset.y);

                Color pixel = GetPixelBilinear(sourcePixels, sourceWidth, sourceHeight, uv);

                resultPixels[y * sourceWidth + x] = pixel;
            }

            private static Color GetPixelBilinear(NativeArray<Color32> pixels, int width, int height, float2 uv)
            {
                float2 fracUV = math.frac(uv);

                float x = fracUV.x * (width - 1);
                float y = fracUV.y * (height - 1);

                int xMin = (int)math.floor(x);
                int yMin = (int)math.floor(y);

                int xMax = math.min(xMin + 1, width - 1);
                int yMax = math.min(yMin + 1, height - 1);

                float xFrac = x - xMin;
                float yFrac = y - yMin;

                Color c00 = pixels[xMin + yMin * width];
                Color c10 = pixels[xMax + yMin * width];
                Color c01 = pixels[xMin + yMax * width];
                Color c11 = pixels[xMax + yMax * width];

                Color top = Color.Lerp(c00, c10, xFrac);
                Color bottom = Color.Lerp(c01, c11, xFrac);
                Color final = Color.Lerp(top, bottom, yFrac);

                return final;
            }
        }

        [BurstCompile]
        private struct TextureRepeatJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<Color32> sourcePixels;
            public int sourceWidth;
            public int sourceHeight;

            [WriteOnly]
            public NativeArray<Color32> resultPixels;
            public int repeatX;
            public int repeatY;

            public void Execute(int index)
            {
                int repeatedWidth = sourceWidth * repeatX;

                int row = index / repeatedWidth;
                int col = index % repeatedWidth;

                int srcRow = row % sourceHeight;
                int srcCol = col % sourceWidth;

                int srcIndex = srcRow * sourceWidth + srcCol;

                resultPixels[index] = sourcePixels[srcIndex];
            }
        }

        [BurstCompile]
        private struct TextureResizeJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<Color32> sourcePixels;
            public int sourceWidth;
            public int sourceHeight;

            [WriteOnly]
            public NativeArray<Color32> resultPixels;
            public int resultWidth;
            public int resultHeight;

            public void Execute(int index)
            {
                int x = index % resultWidth;
                int y = index / resultWidth;

                float u = (float)x / (resultWidth - 1);
                float v = (float)y / (resultHeight - 1);

                float srcX = u * (sourceWidth - 1);
                float srcY = v * (sourceHeight - 1);

                int xMin = (int)math.floor(srcX);
                int yMin = (int)math.floor(srcY);
                int xMax = math.min(xMin + 1, sourceWidth - 1);
                int yMax = math.min(yMin + 1, sourceHeight - 1);

                float uRatio = srcX - xMin;
                float vRatio = srcY - yMin;
                float uOpposite = 1f - uRatio;
                float vOpposite = 1f - vRatio;

                Color32 c00 = sourcePixels[yMin * sourceWidth + xMin];
                Color32 c10 = sourcePixels[yMin * sourceWidth + xMax];
                Color32 c01 = sourcePixels[yMax * sourceWidth + xMin];
                Color32 c11 = sourcePixels[yMax * sourceWidth + xMax];

                float4 fc00 = new float4(c00.r, c00.g, c00.b, c00.a);
                float4 fc10 = new float4(c10.r, c10.g, c10.b, c10.a);
                float4 fc01 = new float4(c01.r, c01.g, c01.b, c01.a);
                float4 fc11 = new float4(c11.r, c11.g, c11.b, c11.a);

                float4 fc0 = fc00 * uOpposite + fc10 * uRatio;
                float4 fc1 = fc01 * uOpposite + fc11 * uRatio;

                float4 finalColor = fc0 * vOpposite + fc1 * vRatio;

                byte r = (byte)math.clamp(math.round(finalColor.x), 0, 255);
                byte g = (byte)math.clamp(math.round(finalColor.y), 0, 255);
                byte b = (byte)math.clamp(math.round(finalColor.z), 0, 255);
                byte a = (byte)math.clamp(math.round(finalColor.w), 0, 255);

                resultPixels[index] = new Color32(r, g, b, a);
            }
        }

        [BurstCompile]
        private struct TextureRepeatAndResizeJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<Color32> sourcePixels;
            public int sourceWidth;
            public int sourceHeight;

            [WriteOnly]
            public NativeArray<Color32> resultPixels;
            public int resultWidth;
            public int resultHeight;
            public int repeatX;
            public int repeatY;

            public void Execute(int index)
            {
                int x = index % resultWidth;
                int y = index / resultWidth;

                float u = (float)x / (resultWidth - 1);
                float v = (float)y / (resultHeight - 1);

                float srcX = u * (sourceWidth * repeatX - 1);
                float srcY = v * (sourceHeight * repeatY - 1);

                int srcXMod = ((int)srcX % sourceWidth + sourceWidth) % sourceWidth;
                int srcYMod = ((int)srcY % sourceHeight + sourceHeight) % sourceHeight;

                int xMin = (int)math.floor(srcXMod);
                int yMin = (int)math.floor(srcYMod);
                int xMax = math.min(xMin + 1, sourceWidth - 1);
                int yMax = math.min(yMin + 1, sourceHeight - 1);

                float uRatio = srcXMod - xMin;
                float vRatio = srcYMod - yMin;
                float uOpposite = 1f - uRatio;
                float vOpposite = 1f - vRatio;

                Color32 c00 = sourcePixels[yMin * sourceWidth + xMin];
                Color32 c10 = sourcePixels[yMin * sourceWidth + xMax];
                Color32 c01 = sourcePixels[yMax * sourceWidth + xMin];
                Color32 c11 = sourcePixels[yMax * sourceWidth + xMax];

                float4 fc00 = new float4(c00.r, c00.g, c00.b, c00.a);
                float4 fc10 = new float4(c10.r, c10.g, c10.b, c10.a);
                float4 fc01 = new float4(c01.r, c01.g, c01.b, c01.a);
                float4 fc11 = new float4(c11.r, c11.g, c11.b, c11.a);

                float4 fc0 = fc00 * uOpposite + fc10 * uRatio;
                float4 fc1 = fc01 * uOpposite + fc11 * uRatio;
                float4 finalColor = fc0 * vOpposite + fc1 * vRatio;

                byte r = (byte)math.clamp(math.round(finalColor.x), 0, 255);
                byte g = (byte)math.clamp(math.round(finalColor.y), 0, 255);
                byte b = (byte)math.clamp(math.round(finalColor.z), 0, 255);
                byte a = (byte)math.clamp(math.round(finalColor.w), 0, 255);

                resultPixels[index] = new Color32(r, g, b, a);
            }
        }
    }
}
