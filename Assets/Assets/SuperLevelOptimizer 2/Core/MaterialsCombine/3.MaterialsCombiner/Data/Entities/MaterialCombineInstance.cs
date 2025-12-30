using System;
using UnityEngine;
using NGS.SLO.Shared;

namespace NGS.SLO.MaterialsCombine
{
    [Serializable]
    public class MaterialCombineInstance
    {
        public ShaderCombineInfo ShaderInfo
        {
            get
            {
                return _shaderInfo;
            }
        }

        public Material Material
        {
            get
            {
                return _material;
            }
        }
        public int MaterialIndex
        {
            get
            {
                return _materialIndex;
            }
        }

        public Renderer Renderer
        {
            get
            {
                return _renderer;
            }
        }
        public Mesh Mesh
        {
            get
            {
                return _mesh;
            }
        }

        public bool ReadyForCombine
        {
            get
            {
                return _readyForCombine;
            }
        }
        public string Reason
        {
            get
            {
                return _reason;
            }
        }

        public IReadableInstanceData Data
        {
            get
            {
                return _data;
            }
        }

        [SerializeField]
        private ShaderCombineInfo _shaderInfo;

        [SerializeField]
        private Material _material;

        [SerializeField]
        private int _materialIndex;

        [SerializeField]
        private Renderer _renderer;

        [SerializeField]
        private Mesh _mesh;

        [SerializeField]
        private InstanceData _data;

        [SerializeField]
        private bool _readyForCombine;

        [SerializeField]
        private string _reason;


        public MaterialCombineInstance(Renderer renderer, int materialIndex)
        {
            _renderer = renderer;

            _materialIndex = materialIndex;

            _material = renderer.GetSharedMaterial(materialIndex);

            if (_material == null)
                throw new NullReferenceException("Trying to create CombineInstance for empty material");

            _data = new InstanceData();

            SetNotReadyForCombine("Data Not Gathered");
        }

        public void GatherCombineData(ShaderCombineInfo shaderInfo, MaterialsCombineOptions combineOptions)
        {
            try
            {
                _shaderInfo = shaderInfo;

                if (!shaderInfo.AllowedCombine)
                {
                    SetNotReadyForCombine("Shader Not Allowed For Combine");
                    return;
                }

                _renderer.TryGetSharedMesh(out _mesh);

                if (_mesh == null)
                {
                    SetNotReadyForCombine("Mesh Not Found");
                    return;
                }

                if (_materialIndex >= _mesh.subMeshCount)
                {
                    SetNotReadyForCombine("MaterialIndex More Then Submeshes Count");
                    return;
                }

                GatherUVData();

                if (Mathf.Abs(_data.UVScale.x) < 0.0001f || Mathf.Abs(_data.UVScale.y) < 0.0001f)
                {
                    SetNotReadyForCombine("UVScale equals zero");
                    return;
                }

                if (string.IsNullOrWhiteSpace(shaderInfo.MainTextureName))
                {
                    SetNotReadyForCombine("ShaderInfo missed MainTextureName");
                    return;
                }

                Texture2D mainTexture = _material.GetTexture(shaderInfo.MainTextureName) as Texture2D;

                if (mainTexture == null)
                {
                    SetNotReadyForCombine("MainTexture Not Found");
                    return;
                }

                GatherMainTextureData(mainTexture, combineOptions);

                if (Mathf.Abs(_data.MainTextureScale.x) < 0.0001f || Mathf.Abs(_data.MainTextureScale.y) < 0.0001f)
                {
                    SetNotReadyForCombine("MainTexture Scale Equals Zero");
                    return;
                }

                if (_data.MainTextureTargetSize.x >= combineOptions.maxAtlasSize || _data.MainTextureTargetSize.y >= combineOptions.maxAtlasSize)
                {
                    SetNotReadyForCombine("MainTexture size more or equal to maxAtlasSize");
                    return;
                }

                if (_data.MainTextureRepeats.x == 0 || _data.MainTextureRepeats.y == 0)
                {
                    SetNotReadyForCombine("TextureRepeats equals to zero");
                    return;
                }

                if (Mathf.Max(_data.MainTextureDownscale.x, _data.MainTextureDownscale.y) > combineOptions.maxTileTextureDownscale)
                {
                    SetNotReadyForCombine("Too Much Main Texture Downscale");
                    return;
                }

                SetReadyForCombine();
            }
            catch (Exception ex)
            {
                SetNotReadyForCombine($"Error during gathering data: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public Vector2Int CalculateTextureRepeats(Vector2 textureScale, Vector2 textureOffset)
        {
            if (_shaderInfo == null)
            {
                Debug.Log("MaterialCombineInstance::CalculateTextureRepeats - needed to GatherCombineData first");
                return Vector2Int.zero;
            }

            textureScale.x = Mathf.Abs(textureScale.x);
            textureScale.y = Mathf.Abs(textureScale.y);

            textureOffset.x = Mathf.Repeat(textureOffset.x, 1);
            textureOffset.y = Mathf.Repeat(textureOffset.y, 1);

            return new Vector2Int(
                Mathf.CeilToInt(Mathf.Abs(_data.UVScale.x + Mathf.Repeat(_data.UVOffset.x, 1)) * textureScale.x + textureOffset.x),
                Mathf.CeilToInt(Mathf.Abs(_data.UVScale.y + Mathf.Repeat(_data.UVOffset.y, 1)) * textureScale.y + textureOffset.y));
        }


        private void GatherUVData()
        {
            int[] indices = _mesh.GetIndices(_materialIndex);
            Vector2[] uvs = _mesh.uv;

            Vector2 uvMin = Vector2.one * float.MaxValue;
            Vector2 uvMax = Vector2.one * float.MinValue;

            for (int i = 0; i < indices.Length; i++)
            {
                int idx = indices[i];

                Vector2 uv = uvs[idx];

                uv.x = uv.x.SnapToInt();
                uv.y = uv.y.SnapToInt();

                uvMin.x = Mathf.Min(uvMin.x, uv.x);
                uvMin.y = Mathf.Min(uvMin.y, uv.y);

                uvMax.x = Mathf.Max(uvMax.x, uv.x);
                uvMax.y = Mathf.Max(uvMax.y, uv.y);
            }

            _data.UVScale = new Vector2(Mathf.Abs(uvMax.x - uvMin.x), Mathf.Abs(uvMax.y - uvMin.y));
            _data.UVOffset = uvMin;
        }

        private void GatherMainTextureData(Texture2D mainTexture, MaterialsCombineOptions combineOptions)
        {
            Vector2 mainTextureScale = _material.GetTextureScale(_shaderInfo.MainTextureName);
            Vector2 mainTextureOffset = _material.GetTextureOffset(_shaderInfo.MainTextureName);

            mainTextureScale.x = Mathf.Abs(mainTextureScale.x);
            mainTextureScale.y = Mathf.Abs(mainTextureScale.y);

            mainTextureOffset.x = Mathf.Repeat(mainTextureOffset.x, 1);
            mainTextureOffset.y = Mathf.Repeat(mainTextureOffset.y, 1);

            Vector2Int totalRepeats = CalculateTextureRepeats(mainTextureScale, mainTextureOffset);

            Vector2 mainTextureSize = new Vector2(mainTexture.width, mainTexture.height);
            Vector2 scaledSize = new Vector2(mainTextureSize.x * totalRepeats.x, mainTextureSize.y * totalRepeats.y);

            int maxTextureSize = (int)Mathf.Max(combineOptions.maxTileTextureUpscaledSize, mainTextureSize.x, mainTextureSize.y);
            float scaleFactor = maxTextureSize / Mathf.Max(scaledSize.x, scaledSize.y);

            scaleFactor = Mathf.Min(1f, scaleFactor);

            _data.MainTextureOffset = mainTextureOffset;
            _data.MainTextureScale = mainTextureScale;
            _data.MainTextureRepeats = totalRepeats;

            _data.MainTextureTargetSize = new Vector2(
                (scaledSize.x * scaleFactor).FloorToPowerOfTwo(),
                (scaledSize.y * scaleFactor).FloorToPowerOfTwo());

            _data.MainTextureDownscale = new Vector2(
                scaledSize.x / _data.MainTextureTargetSize.x,
                scaledSize.y / _data.MainTextureTargetSize.y);
        }


        private void SetReadyForCombine()
        {
            _readyForCombine = true;
            _reason = "";
        }

        private void SetNotReadyForCombine(string reason)
        {
            _readyForCombine = false;
            _reason = reason;
        }


        public interface IReadableInstanceData
        {
            Vector2 UVOffset { get; }
            Vector2 UVScale { get; }

            Vector2 MainTextureOffset { get; }
            Vector2 MainTextureScale { get; }

            Vector2Int MainTextureRepeats { get; }

            Vector2 MainTextureTargetSize { get; }
            Vector2 MainTextureDownscale { get; }
        }

        [Serializable]
        private struct InstanceData : IReadableInstanceData
        {
            [field: SerializeField]
            public Vector2 UVOffset { get; set; }

            [field: SerializeField]
            public Vector2 UVScale { get; set; }

            [field: SerializeField]
            public Vector2 MainTextureOffset { get; set; }

            [field: SerializeField]
            public Vector2 MainTextureScale { get; set; }

            [field: SerializeField]
            public Vector2Int MainTextureRepeats { get; set; }

            [field: SerializeField]
            public Vector2 MainTextureTargetSize { get; set; }

            [field: SerializeField]
            public Vector2 MainTextureDownscale { get; set; }
        }
    }
}
