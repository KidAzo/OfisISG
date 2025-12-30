using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using NGS.SLO.Shared;

#if UNITY_EDITOR

using UnityEditor;

#endif

namespace NGS.SLO.MaterialsCombine
{

#if UNITY_EDITOR

    public enum TextureSize 
    {
        _128 = 128,
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096,
        _8192 = 8192,
        _16384 = 16384
    };

    public enum TexturePackerType
    {
        CustomPacker,
        UnityPacker
    }

    public class MaterialsCombineController : MonoBehaviour
    {
        public bool ShowRenderers
        {
            get
            {
                return _showRenderers;
            }
            set
            {
                _showRenderers = value;
            }
        }
        public bool ShowCombinedRenderers
        {
            get
            {
                return _showCombinedRenderers;
            }
            set
            {
                _showCombinedRenderers = value;
            }
        }
        public bool ShowRenderersWithCombineErrors
        {
            get
            {
                return _showRenderersWithCombineErrors;
            }
            set
            {
                _showRenderersWithCombineErrors = value;
            }
        }

        public bool IncludeOnlyStatic
        {
            get
            {
                return _includeOnlyStaticRenderers;
            }
            set
            {
                _includeOnlyStaticRenderers = value;
            }
        }
        public bool IncludeMeshRenderers
        {
            get
            {
                return _includeMeshRenderers;
            }
            set
            {
                _includeMeshRenderers = value;
            }
        }
        public bool IncludeSkinnedMeshRenderers
        {
            get
            {
                return _includeSkinnedMeshRenderers;
            }
            set
            {
                _includeSkinnedMeshRenderers = value;
            }
        }

        public IReadOnlyList<Renderer> Renderers
        {
            get
            {
                return _renderers;
            }
        }
        public IReadOnlyList<ShaderCombineInfo> ShaderInfos
        {
            get
            {
                return _shaderCombineInfos;
            }
        }

        public TexturePackerType TexturePackerType
        {
            get
            {
                return _texturePackerType;
            }
            set
            {
                _texturePackerType = value;
            }
        }
        public TextureSize MaxAtlasSize
        {
            get
            {
                return (TextureSize) _combineOptions.maxAtlasSize;
            }
            set
            {
                _combineOptions.maxAtlasSize = (int) value;
            }
        }
        public TextureSize MaxTileTextureUpscaledSize
        {
            get
            {
                return (TextureSize) _combineOptions.maxTileTextureUpscaledSize;
            }
            set
            {
                _combineOptions.maxTileTextureUpscaledSize = (int) value;
            }
        }
        public int MaxTileTextureDownscale
        {
            get
            {
                return _combineOptions.maxTileTextureDownscale;
            }
            set
            {
                _combineOptions.maxTileTextureDownscale = Mathf.Max(1, value);
            }
        }
        public int Padding
        {
            get
            {
                return _combineOptions.padding;
            }
            set
            {
                _combineOptions.padding = Mathf.Max(0, value);
            }
        }
        public bool FillEmptyTextures
        {
            get
            {
                return _combineOptions.fillEmptyTextures;
            }
            set
            {
                _combineOptions.fillEmptyTextures = value;
            }
        }
        public bool SaveAssets
        {
            get
            {
                return _saveAssets;
            }
            set
            {
                _saveAssets = value;
            }
        }
        public string DataOutputPath
        {
            get
            {
                return _dataOutputPath;
            }
            set
            {
                string relativePath;

                if (!PathUtil.TryGetProjectRelative(value, out relativePath))
                {
                    Debug.Log($"Unable to change 'DataOutputPath' to {value}");

                    _dataOutputPath = CreateDefaultDataOutputPath();
                    return;
                }

                _dataOutputPath = relativePath;
            }
        }

        public BackupOption BackupOption
        {
            get
            {
                return _backupOption;
            }
            set
            {
                _backupOption = value;
            }
        }

        public int CombinedMaterialInstancesCount
        {
            get
            {
                return _combinedMaterialInstancesCount;
            }
        }
        public int MaterialInstancesWithCombineErrorsCount
        {
            get
            {
                return _materialInstancesWithCombineErrorsCount;
            }
        }

        [SerializeField]
        private bool _showRenderers;

        [SerializeField]
        private bool _includeOnlyStaticRenderers;

        [SerializeField]
        private bool _includeMeshRenderers;

        [SerializeField]
        private bool _includeSkinnedMeshRenderers;

        [SerializeField]
        private bool _showCombinedRenderers;

        [SerializeField]
        private bool _showRenderersWithCombineErrors;
      
        [SerializeField]
        private List<Renderer> _renderers = new List<Renderer>();

        [SerializeField]
        private List<ShaderCombineInfo> _shaderCombineInfos;

        [SerializeField]
        private TexturePackerType _texturePackerType;

        [SerializeField]
        private MaterialsCombineOptions _combineOptions;

        [SerializeField]
        private bool _saveAssets;

        [SerializeField]
        private string _dataOutputPath;

        [SerializeField]
        private BackupOption _backupOption;

        [SerializeField]
        private int _combinedMaterialInstancesCount;

        [SerializeField]
        private int _materialInstancesWithCombineErrorsCount;


        [MenuItem("Tools/NGSTools/Super Level Optimizer 2/Materials Combiner")]
        private static void CreateControllerGameObject()
        {
            GameObject controllerGo = new GameObject("Materials Combiner");

            controllerGo.AddComponent<MaterialsCombineController>();

            Selection.activeGameObject = controllerGo;
        }

        private void Reset()
        {
            _includeOnlyStaticRenderers = false;
            _includeMeshRenderers = true;
            _includeSkinnedMeshRenderers = true;

            _combineOptions = MaterialsCombineOptions.Default;

            _saveAssets = true;
            _dataOutputPath = CreateDefaultDataOutputPath();
        }

        private void OnDrawGizmos()
        {
            if (_renderers != null && _showRenderers)
                DrawRenderersGizmos();

            if (_showCombinedRenderers)
                DrawCombinedRenderersGizmos();

            if (_showRenderersWithCombineErrors)
                DrawRenderersWithCombinedErrorsGizmos();
        }

        private void DrawRenderersGizmos()
        {
            foreach (var renderer in _renderers)
            {
                Bounds bounds = renderer.bounds;

                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }

        private void DrawCombinedRenderersGizmos()
        {
            MaterialCombineOutput[] outputs = UnityAPI.FindObjectsOfType<MaterialCombineOutput>();

            foreach (var output in outputs)
            {
                foreach (var report in output.Reports)
                {
                    if (report.combineStatus == MaterialCombineStatus.Combined)
                    {
                        Bounds bounds = output.GetComponent<Renderer>().bounds;

                        Gizmos.color = Color.green;
                        Gizmos.DrawWireCube(bounds.center, bounds.size);

                        break;
                    }
                }
            }
        }

        private void DrawRenderersWithCombinedErrorsGizmos()
        {
            MaterialCombineOutput[] outputs = UnityAPI.FindObjectsOfType<MaterialCombineOutput>();

            foreach (var output in outputs)
            {
                foreach (var report in output.Reports)
                {
                    if (report.combineStatus == MaterialCombineStatus.CombineError)
                    {
                        Bounds bounds = output.GetComponent<Renderer>().bounds;

                        Gizmos.color = Color.red;
                        Gizmos.DrawWireCube(bounds.center, bounds.size);

                        break;
                    }
                }
            }
        }


        public void AddSelectedRenderers()
        {
            int count = _renderers.Count;

            foreach (var selectedGO in Selection.gameObjects)
            {
                foreach (var renderer in selectedGO.GetComponentsInChildren<Renderer>())
                {
                    TryAddRenderer(renderer);
                }
            }

            UpdateShaderCombineInfosList();

            count = _renderers.Count - count;

            Debug.Log("Added " + count + " new renderers");
        }

        public void AddAllRenderers()
        {
            _renderers.Clear();

            int count = _renderers.Count;

            foreach (var renderer in UnityAPI.FindObjectsOfType<Renderer>())
            {
                TryAddRenderer(renderer);
            }

            UpdateShaderCombineInfosList();

            count = _renderers.Count - count;

            Debug.Log("Added " + count + " new renderers");
        }

        public void RemoveSelectedRenderers()
        {
            int count = _renderers.Count;

            foreach (var selectedGO in Selection.gameObjects)
            {
                foreach (var renderer in selectedGO.GetComponentsInChildren<Renderer>())
                {
                    RemoveRenderer(renderer);
                }
            }

            UpdateShaderCombineInfosList();

            count = count - _renderers.Count;

            Debug.Log("Removed " + count + " renderers");
        }

        public void RemoveAllRenderers()
        {
            int count = _renderers.Count;

            _renderers.Clear();

            Debug.Log("Removed " + count + " renderers");
        }

        public void DestroyAllBackups()
        {
            if (!EditorUtility.DisplayDialog("Confirmation", "Are You sure that you want to delete all backups?", "Yes", "Cancel"))
                return;

            MaterialCombineSourceBackup[] backups = UnityAPI.FindObjectsOfType<MaterialCombineSourceBackup>();

            foreach (var backup in backups)
            {
                backup.DestroyBackup();
                DestroyImmediate(backup);
            }
        }

        public void RevertAllBackups()
        {
            if (!EditorUtility.DisplayDialog("Confirmation", "Are You sure that you want to revert all backups?", "Yes", "Cancel"))
                return;

            MaterialCombineSourceBackup[] backups = UnityAPI.FindObjectsOfType<MaterialCombineSourceBackup>();

            foreach (var backup in backups)
            {
                try
                {
                    if (backup.BackupCreated)
                        backup.RevertAll();

                    MaterialCombineOutput combineOutput = backup.GetComponent<MaterialCombineOutput>();

                    combineOutput?.Reset();
                }
                catch (Exception ex)
                {
                    Debug.Log($"MaterialsCombineController::error during revert backup {ex.Message}\n{ex.StackTrace}");
                }
            }

            DestroyAllBackups();

            CalculateOutputDataInScene();
        }

        public void CalculateOutputDataInScene()
        {
            _combinedMaterialInstancesCount = 0;
            _materialInstancesWithCombineErrorsCount = 0;

            foreach (var output in UnityAPI.FindObjectsOfType<MaterialCombineOutput>())
            {
                foreach (var report in output.Reports)
                {
                    if (report.combineStatus == MaterialCombineStatus.Combined)
                        _combinedMaterialInstancesCount++;

                    if (report.combineStatus == MaterialCombineStatus.CombineError)
                        _materialInstancesWithCombineErrorsCount++;
                }
            }
        }

        public void DestroyOutputData()
        {
            if (!EditorUtility.DisplayDialog("Confirmation", "Are You sure that you want to delete all output data?", "Yes", "Cancel"))
                return;

            foreach (var output in UnityAPI.FindObjectsOfType<MaterialCombineOutput>())
            {
                DestroyImmediate(output);
            }
        }


        public void CombineMaterials()
        {
            if (_saveAssets)
            {
                if (!PathUtil.TryGetProjectRelative(_dataOutputPath, out _))
                {
                    Debug.Log("MaterialsCombineController::unsupported 'dataOutputPath'. Change before combine");
                    return;
                }
            }

            FilterRenderersAndCreateBackups();

            if (_renderers.Count == 0)
            {
                Debug.Log("MaterialsCombineController::nothing to combine. Add renderers firstly");
                return;
            }

            List<MaterialCombineInstance> instances = GatherMaterialInstances(_renderers);
            List<MaterialCombineInstancesBatch> batches = GatherReadyToCombineBatches(instances);
            List<MaterialsCombineGroup> combineGroups = CreateCombineGroups(batches);

            if (combineGroups.Count == 0)
            {
                Debug.Log("MaterialsCombineController::Couldn't find the matching materials for the combine");
                return;
            }

            int combinedInstancesCount = 0;
            int combineErrorsCount = 0;

            MaterialsCombiner combiner = CreateMaterialsCombiner();

            for (int i = 0; i < combineGroups.Count; i++)
            {
                try
                {
                    MaterialsCombineGroup combineGroup = combineGroups[i];

                    float progress = (float)i / combineGroups.Count;

                    if (EditorUtility.DisplayCancelableProgressBar("Progress...", $"Combined Groups {i} of {combineGroups.Count}", progress))
                        break;

                    MaterialCombineResult combineResult = combiner.Combine(combineGroup.Instances, _combineOptions);

                    if (combineResult.success)
                    {
                        combinedInstancesCount += combineResult.instances.Length;
                    }
                    else
                    {
                        Debug.Log("MaterialsCombineController::Error While Combine Materials: " + combineResult.errorMessage);

                        combineErrorsCount++;
                    }

                    TextureUtil.ClearCache();

                    if (EditorUtility.DisplayCancelableProgressBar("Progress...(Apply Result)", $"Combined Groups {i} of {combineGroups.Count}", progress))
                        break;

                    ApplyCombineResult(combineGroup, batches, combineResult);

                    if (_saveAssets)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar("Progress...(Exporting Assets)", $"Combined Groups {i} of {combineGroups.Count}", progress))
                            break;

                        ExportMissingAssets(combineGroup, batches);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"MaterialsCombineController::error during combine group {ex.Message}\n{ex.StackTrace}");
                }
            }

            EditorUtility.ClearProgressBar();

            MeshUtil.ClearCache();
            TextureUtil.ClearCache();
            AssetsExporter.ClearCache();

            CalculateOutputDataInScene();

            if (combineErrorsCount != 0)
                Debug.Log("MaterialsCombineController::Combine Errors: " + combineErrorsCount);

            Debug.Log("MaterialsCombineController::Combined Uniq Materials: " + combinedInstancesCount);
        }

        private void FilterRenderersAndCreateBackups()
        {
            int i = 0;
            while (i < _renderers.Count)
            {
                Renderer renderer = _renderers[i];

                if (renderer == null)
                {
                    _renderers.RemoveAt(i);
                    continue;
                }

                if (!FilterRenderer(renderer, out string message))
                {
                    Debug.Log("Can't add " + renderer.name + ": " + message);
                    RemoveRenderer(renderer);
                    continue;
                }

                MaterialCombineSourceBackup backup = renderer.GetComponent<MaterialCombineSourceBackup>();

                if (backup == null)
                    backup = renderer.gameObject.AddComponent<MaterialCombineSourceBackup>();

                if (!backup.BackupCreated)
                {
                    backup.BackupOption = _backupOption;
                    backup.CreateBackup();
                }

                i++;
            }
        }

        private List<MaterialCombineInstance> GatherMaterialInstances(IList<Renderer> renderers)
        {
            List<MaterialCombineInstance> instances = new List<MaterialCombineInstance>();
            List<Material> materials = new List<Material>();

            foreach (var renderer in renderers)
            {
                materials.Clear();

                renderer.GetSharedMaterials(materials);

                for (int i = 0; i < materials.Count; i++)
                {
                    Material material = materials[i];

                    if (material == null || material.shader == null)
                        continue;

                    ShaderCombineInfo shaderInfo = _shaderCombineInfos.FirstOrDefault(s => s.Shader == material.shader);

                    if (shaderInfo == null)
                    {
                        Debug.Log("MaterialsCombineController::Can't find shader combine info for " + material.shader.name);
                        continue;
                    }

                    MaterialCombineInstance instance = new MaterialCombineInstance(renderer, i);
                    instance.GatherCombineData(shaderInfo, _combineOptions);

                    instances.Add(instance);
                }
            }

            return instances;
        }

        private List<MaterialCombineInstancesBatch> GatherReadyToCombineBatches(IList<MaterialCombineInstance> instances)
        {
            List<MaterialCombineInstancesBatch> readyForCombineBatches = new List<MaterialCombineInstancesBatch>();

            for (int i = 0; i < instances.Count; i++)
            {
                MaterialCombineInstance instance = instances[i];

                if (!instance.ReadyForCombine)
                    continue;

                MaterialCombineInstancesBatch batch = readyForCombineBatches.FirstOrDefault(b => b.IsSameInstance(instance));

                if (batch == null)
                {
                    readyForCombineBatches.Add(new MaterialCombineInstancesBatch(instance));
                }
                else
                {
                    batch.Append(instance);
                }
            }

            return readyForCombineBatches;
        }

        private List<MaterialsCombineGroup> CreateCombineGroups(List<MaterialCombineInstancesBatch> batches)
        {
            List<MaterialsCombineGroup> combineGroups = new List<MaterialsCombineGroup>();

            foreach (var batch in batches)
            {
                bool appended = false;

                for (int i = 0; i < combineGroups.Count; i++)
                {
                    MaterialsCombineGroup group = combineGroups[i];

                    if (group.TryAddInstance(batch.Source))
                    {
                        appended = true;
                        break;
                    }
                }

                if (!appended)
                {
                    MaterialsCombineGroup group = new MaterialsCombineGroup(
                        batch.Source.ShaderInfo,
                        _combineOptions,
                        new TexturePlacer(_combineOptions.maxAtlasSize, _combineOptions.maxAtlasSize));

                    group.TryAddInstance(batch.Source);

                    combineGroups.Add(group);
                }
            }

            return combineGroups.Where(g => g.Instances.Count > 1).ToList();
        }

        private MaterialsCombiner CreateMaterialsCombiner()
        {
            ITexturePacker texturePacker;

            if (_texturePackerType == TexturePackerType.CustomPacker)
                texturePacker = new TexturePacker();

            else
                texturePacker = new UnityTexturePacker();

            return new MaterialsCombiner(texturePacker);
        }

        private void ApplyCombineResult(MaterialsCombineGroup combineGroup, List<MaterialCombineInstancesBatch> batches, MaterialCombineResult combineResult)
        {
            for (int c = 0; c < combineGroup.Instances.Count; c++)
            {
                MaterialCombineInstancesBatch batch = batches.First(b => b.Source == combineGroup.Instances[c]);

                foreach (var instance in batch.Instances)
                {
                    MaterialCombineOutput output = instance.Renderer.GetComponent<MaterialCombineOutput>();

                    if (output == null)
                        output = instance.Renderer.gameObject.AddComponent<MaterialCombineOutput>();

                    output.ApplyCombineResult(instance, combineResult, c);
                }
            }
        }

        private void ExportMissingAssets(MaterialsCombineGroup combineGroup, List<MaterialCombineInstancesBatch> batches)
        {
            List<GameObject> gosForExport = new List<GameObject>();

            for (int c = 0; c < combineGroup.Instances.Count; c++)
            {
                MaterialCombineInstancesBatch batch = batches.First(b => b.Source == combineGroup.Instances[c]);

                foreach (var instance in batch.Instances)
                {
                    if (instance.Renderer == null)
                        continue;

                    gosForExport.Add(instance.Renderer.gameObject);
                }
            }

            AssetsExporter.ExportMissingAssets(gosForExport, _dataOutputPath);

            EditorUtility.UnloadUnusedAssetsImmediate();
        }


        private string CreateDefaultDataOutputPath()
        {
            return $"Assets/slo_output/{SceneManager.GetActiveScene().name.ToLower()}_{Mathf.Abs(SceneManager.GetActiveScene().GetHashCode())}";
        }

        private bool TryAddRenderer(Renderer renderer)
        {
            if (_renderers.Contains(renderer))
                return true;

            if (_includeOnlyStaticRenderers)
            {
                if (!renderer.gameObject.isStatic)
                    return false;
            }

            if (!_includeMeshRenderers)
            {
                if (renderer is MeshRenderer)
                    return false;
            }

            if (!_includeSkinnedMeshRenderers)
            {
                if (renderer is SkinnedMeshRenderer)
                    return false;
            }

            string failureMessage;

            if (FilterRenderer(renderer, out failureMessage))
            {
                _renderers.Add(renderer);

                return true;
            }

            Debug.Log($"MaterialsCombineController::Can't add {renderer.name}: {failureMessage}");

            return false;
        }

        private void RemoveRenderer(Renderer renderer)
        {
            _renderers.Remove(renderer);
        }

        private bool FilterRenderer(Renderer renderer, out string reason)
        {
            if (!renderer.enabled)
            {
                reason = "Renderer is disabled";
                return false;
            }

            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
            {
                reason = "Not MeshRenderer or SkinnedMeshRenderer";
                return false;
            }

            if (renderer.sharedMaterials.Length == 0)
            {
                reason = "Not contains materials";
                return false;
            }

            if (!renderer.TryGetSharedMesh(out _))
            {
                reason = "Mesh not found";
                return false;
            }

            reason = "";
            return true;
        }

        private void UpdateShaderCombineInfosList()
        {
            if (_shaderCombineInfos == null)
                _shaderCombineInfos = new List<ShaderCombineInfo>();

            int c = 0;
            while (c < _shaderCombineInfos.Count)
            {
                if (_shaderCombineInfos[c] == null)
                {
                    _shaderCombineInfos.RemoveAt(c);
                    continue;
                }

                c++;
            }

            List<Material> sharedMaterials = new List<Material>();

            foreach (var renderer in _renderers)
            {
                sharedMaterials.Clear();

                renderer.GetSharedMaterials(sharedMaterials);

                for (int i = 0; i < sharedMaterials.Count; i++)
                {
                    Material material = sharedMaterials[i];

                    if (material == null || material.shader == null)
                        continue;

                    Shader shader = material.shader;
                    ShaderCombineInfo shaderInfo = _shaderCombineInfos.FirstOrDefault(s => s.Shader == shader);

                    if (shaderInfo == null)
                        _shaderCombineInfos.Add(new ShaderCombineInfo(shader));
                }
            }
        }



        private class MaterialCombineInstancesBatch
        {
            public MaterialCombineInstance Source
            {
                get
                {
                    return _source;
                }
            }
            public IReadOnlyList<MaterialCombineInstance> Instances
            {
                get
                {
                    return _instances;
                }
            }

            private MaterialCombineInstance _source;
            private List<MaterialCombineInstance> _instances;


            public MaterialCombineInstancesBatch(MaterialCombineInstance source)
            {
                _source = source;

                _instances = new List<MaterialCombineInstance>();

                _instances.Add(_source);
            }

            public bool IsSameInstance(MaterialCombineInstance other)
            {
                if (_source.Material != other.Material)
                    return false;

                if (_source.Data.MainTextureRepeats != other.Data.MainTextureRepeats)
                    return false;

                return true;
            }

            public void Append(MaterialCombineInstance instance)
            {
                _instances.Add(instance);
            }
        }

        private class MaterialsCombineGroup
        {
            public IReadOnlyList<MaterialCombineInstance> Instances
            {
                get
                {
                    return _materialInstances;
                }
            }

            private List<MaterialCombineInstance> _materialInstances;
            private ShaderCombineInfo _shaderInfo;
            private MaterialsCombineOptions _combineOptions;
            private ITexturePlacer _texturePlacer;


            public MaterialsCombineGroup(ShaderCombineInfo shaderInfo, MaterialsCombineOptions combineOptions, ITexturePlacer texturePlacer)
            {
                _materialInstances = new List<MaterialCombineInstance>();

                _shaderInfo = shaderInfo;
                _combineOptions = combineOptions;
                _texturePlacer = texturePlacer;
            }

            public bool CanAddInstance(MaterialCombineInstance instance)
            {
                if (instance.Material.shader != _shaderInfo.Shader)
                    return false;

                if (_materialInstances.Count == 0)
                    return true;

                if (instance.Renderer.GetType() != _materialInstances[0].Renderer.GetType())
                    return false;

                for (int i = 0; i < _materialInstances.Count; i++)
                {
                    if (instance.Material == _materialInstances[i].Material)
                        return false;

                    bool isEqual = MaterialCombineInstancesComparer.HasEqualParameters(
                        _shaderInfo, _combineOptions, instance, _materialInstances[i]);

                    if (!isEqual)
                        return false;
                }

                Vector2 textureSize = instance.Data.MainTextureTargetSize;

                return _texturePlacer.CanInsert((int)textureSize.x, (int)textureSize.y);
            }

            public bool TryAddInstance(MaterialCombineInstance instance)
            {
                if (!CanAddInstance(instance))
                    return false;

                Vector2 textureSize = instance.Data.MainTextureTargetSize;

                if (!_texturePlacer.TryInsert((int)textureSize.x, (int)textureSize.y, out _))
                    return false;

                _materialInstances.Add(instance);

                return true;
            }
        }
    }

#endif
}
