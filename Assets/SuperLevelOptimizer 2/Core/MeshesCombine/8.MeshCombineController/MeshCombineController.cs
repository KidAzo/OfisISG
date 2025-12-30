using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using NGS.SLO.Shared;

# if UNITY_EDITOR

using UnityEditor;

#endif

namespace NGS.SLO.MeshesCombine
{
#if UNITY_EDITOR

    public class MeshCombineController : MonoBehaviour
    {
        public bool DrawSelectedObjects
        {
            get
            {
                return _drawSelectedObjects;
            }
            set
            {
                _drawSelectedObjects = value;
            }
        }
        public bool IncludeOnlyStatic
        {
            get
            {
                return _includeOnlyStatic;
            }
            set
            {
                _includeOnlyStatic = value;
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
        public bool IncludeLODGroups
        {
            get
            {
                return _includeLODGroups;
            }
            set
            {
                _includeLODGroups = value;
            }
        }

        public bool DrawGroups
        {
            get
            {
                return _drawGroups;
            }
            set
            {
                _drawGroups = value;
            }
        }
        public bool SplitInGroups
        {
            get
            {
                return _splitInGroups;
            }
            set
            {
                if (!value)
                    _binaryTree?.Clear();

                _splitInGroups = value;
            }
        }
        public float CellSize
        {
            get
            {
                return _cellSize;
            }
            set
            {
                _cellSize = Mathf.Max(value, 0.05f);
            }
        }
        public bool Limit65kVertices
        {
            get
            {
                return _combineOptions.limit65kVertices;
            }
            set
            {
                _combineOptions.limit65kVertices = value;
            }
        }
        public bool UseLightweightBuffers
        {
            get
            {
                return _combineOptions.useLightweightBuffers;
            }
            set
            {
                _combineOptions.useLightweightBuffers = value;
            }
        }
        public bool RebuildUV2
        {
            get
            {
                return _combineOptions.rebuildUV2;
            }
            set
            {
                _combineOptions.rebuildUV2 = value;
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
                    Debug.Log("Unsupported path");

                    _dataOutputPath = CreateDefaultDataOutputPath();

                    return;
                }

                _dataOutputPath = relativePath;
            }
        }

        public bool DrawSources
        {
            get
            {
                return _drawSources;
            }
            set
            {
                _drawSources = value;
            }
        }
        public int CombinedSourcesCount
        {
            get
            {
                return _combinedSourcesCount;
            }
        }
        public int CombineErrorsCount
        {
            get
            {
                return _combineErrorsCount;
            }
        }

        public int SourceObjectsCount
        {
            get
            {
                return _sourceObjectsCount;
            }
        }
        public int CombinedObjectsCount
        {
            get
            {
                return _combinedObjectsCount;
            }
        }

        public IReadOnlyList<Renderer> SelectedRenderers
        {
            get
            {
                return _selectedRenderers;
            }
        }
        public IReadOnlyList<LODGroup> SelectedLODGroups
        {
            get
            {
                return _selectedLODGroups;
            }
        }

        [SerializeField]
        private bool _drawSelectedObjects;

        [SerializeField]
        private bool _includeOnlyStatic;

        [SerializeField]
        private bool _includeMeshRenderers;

        [SerializeField]
        private bool _includeSkinnedMeshRenderers;

        [SerializeField]
        private bool _includeLODGroups;

        [SerializeField]
        private List<Renderer> _selectedRenderers;

        [SerializeField]
        private List<LODGroup> _selectedLODGroups;

        [SerializeField]
        private bool _drawGroups;

        [SerializeField]
        private bool _splitInGroups;

        [Min(0.01f)]
        [SerializeField]
        private float _cellSize;

        [SerializeField]
        private MeshCombineOptions _combineOptions;

        [SerializeField]
        private bool _saveAssets;

        [SerializeField]
        private string _dataOutputPath;

        [SerializeField]
        private bool _drawSources;

        [SerializeField]
        private int _sourceObjectsCount;

        [SerializeField]
        private int _combinedObjectsCount;

        [SerializeField]
        private int _combinedSourcesCount;

        [SerializeField]
        private int _combineErrorsCount;

        private BinaryTree _binaryTree;


        [MenuItem("Tools/NGSTools/Super Level Optimizer 2/Meshes Combiner")]
        private static void CreateGO()
        {
            GameObject controllerGo = new GameObject("Meshes Combiner");

            controllerGo.AddComponent<MeshCombineController>();

            Selection.activeGameObject = controllerGo;
        }


        private void Reset()
        {
            _includeMeshRenderers = true;
            _includeSkinnedMeshRenderers = true;
            _includeLODGroups = true;

            _selectedRenderers = new List<Renderer>();
            _selectedLODGroups = new List<LODGroup>();

            _splitInGroups = false;
            _cellSize = 80;

            _saveAssets = true;
            _dataOutputPath = CreateDefaultDataOutputPath();
        }

        private void OnDrawGizmos()
        {
            if (_drawGroups)
            {
                if (_binaryTree != null && _binaryTree.Root != null)
                    _binaryTree.DrawGizmos(Color.white);
            }

            if (_drawSelectedObjects)
                DrawSelectedObjectsGizmos();

            if (_drawSources)
                DrawSourcesGizmos();
        }

        private void DrawSelectedObjectsGizmos()
        {
            foreach (var renderer in _selectedRenderers)
            {
                if (renderer == null)
                    continue;

                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(renderer.bounds.center, renderer.bounds.size);
            }

            foreach (var lodGroup in _selectedLODGroups)
            {
                if (lodGroup == null)
                    continue;

                foreach (var renderer in lodGroup.GetLODs().SelectMany(l => l.renderers))
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawWireCube(renderer.bounds.center, renderer.bounds.size);
                }
            }
        }

        private void DrawSourcesGizmos()
        {
            foreach (var source in UnityAPI.FindObjectsOfType<MeshCombineSource>())
            {
                bool containsError = false;

                foreach (var report in source.Reports)
                {
                    if (report.status == MeshCombineStatus.CombineError)
                    {
                        containsError = true;
                        break;
                    }
                }

                Bounds bounds = source.GetComponent<Renderer>().bounds;

                if (containsError)
                    Gizmos.color = Color.red;
                else
                    Gizmos.color = Color.green;

                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }


        public void AddObjectsAutomatic()
        {
            _selectedRenderers.Clear();
            _selectedLODGroups.Clear();

            int count = _selectedRenderers.Count + _selectedLODGroups.Count;

            if (_includeMeshRenderers)
            {
                foreach (var meshRenderer in UnityAPI.FindObjectsOfType<MeshRenderer>())
                    TryAddRenderer(meshRenderer);
            }

            if (_includeSkinnedMeshRenderers)
            {
                foreach (var skinnedRenderer in UnityAPI.FindObjectsOfType<SkinnedMeshRenderer>())
                    TryAddRenderer(skinnedRenderer);
            }

            if (_includeLODGroups)
            {
                foreach (var lodGroup in UnityAPI.FindObjectsOfType<LODGroup>())
                    TryAddLODGroup(lodGroup);
            }

            Debug.Log($"Added {(_selectedRenderers.Count + _selectedLODGroups.Count) - count} new sources");
        }

        public void AddSelectedObjects()
        {
            int count = _selectedRenderers.Count + _selectedLODGroups.Count;

            if (_includeMeshRenderers)
            {
                foreach (var meshRenderer in Selection.gameObjects.SelectMany(g => g.GetComponentsInChildren<MeshRenderer>()))
                {
                    TryAddRenderer(meshRenderer);
                }
            }

            if (_includeSkinnedMeshRenderers)
            {
                foreach (var skinnedRenderer in Selection.gameObjects.SelectMany(g => g.GetComponentsInChildren<SkinnedMeshRenderer>()))
                    TryAddRenderer(skinnedRenderer);
            }

            if (_includeLODGroups)
            {
                foreach (var lodGroup in Selection.gameObjects.SelectMany(g => g.GetComponentsInParent<LODGroup>()))
                    TryAddLODGroup(lodGroup);
            }

            Debug.Log($"Added {(_selectedRenderers.Count + _selectedLODGroups.Count) - count} new sources");
        }

        public void RemoveAllObjects()
        {
            _selectedRenderers.Clear();
            _selectedLODGroups.Clear();

            Debug.Log("All objects removed");
        }

        public void RemoveSelectedObjects()
        {
            int count = _selectedRenderers.Count + _selectedLODGroups.Count;

            foreach (var renderer in Selection.gameObjects.SelectMany(g => g.GetComponentsInChildren<Renderer>()))
            {
                _selectedRenderers.Remove(renderer);
            }

            foreach (var lodGroup in Selection.gameObjects.SelectMany(g => g.GetComponentsInParent<LODGroup>()))
            {
                _selectedLODGroups.Remove(lodGroup);
            }

            Debug.Log($"Removed {count - (_selectedRenderers.Count + _selectedLODGroups.Count)} sources");
        }


        public void CreateBinaryTree()
        {
            FilterSelectionsLists();

            if (_selectedRenderers.Count == 0 && _selectedLODGroups.Count == 0)
            {
                Debug.Log("Can't update binary tree, no objects added");
                return;
            }

            List<BinaryTreeData> datas = new List<BinaryTreeData>();

            datas.AddRange(_selectedRenderers.Select(r => new BinaryTreeData(r)));
            datas.AddRange(_selectedLODGroups.Select(l => new BinaryTreeData(l)));

            float cellSize = _splitInGroups ? _cellSize : float.MaxValue;

            _binaryTree = new BinaryTree(cellSize);
            _binaryTree.CreateTree(datas);
        }


        public void SetSourcesEnable(bool combinedEnable, bool sourcesEnable)
        {
            MeshCombineSourcesController.SetEnable(combinedEnable, sourcesEnable);
        }

        public void AddToIgnoreAllSourcesWithError()
        {
            MeshCombineSourcesController.AddSourcesWithErrorToIgnore();
        }

        public void DestroySourceComponents()
        {
            if (!EditorUtility.DisplayDialog("Confirmation", "Are you sure that you want to destroy all source components?", "Yes", "Cancel"))
                return;

            if (!EditorUtility.DisplayDialog("Confirmation", "Are you really-really sure that you want to destroy all source components? (make backup)", "Yes", "Cancel"))
                return;

            MeshCombineSourcesController.DestroySourceComponents();

            RefreshSceneSourcesInfo();
        }

        public void ClearSourcesInfo()
        {
            if (!EditorUtility.DisplayDialog("Confirmation", "Are you sure that you want to clear all source infos? You will not be able to enable/disable sources after this action", "Yes", "Cancel"))
                return;

            foreach (var source in UnityAPI.FindObjectsOfType<MeshCombineSource>())
                DestroyImmediate(source);

            RefreshSceneSourcesInfo();
        }

        public void RefreshSceneSourcesInfo()
        {
            MeshCombineSource[] sources = UnityAPI.FindObjectsOfType<MeshCombineSource>();

            _combinedSourcesCount = sources
                .SelectMany(s => s.Reports)
                .Count(r => r.status == MeshCombineStatus.Combined);

            _combineErrorsCount = sources
                .SelectMany(s => s.Reports)
                .Count(r => r.status == MeshCombineStatus.CombineError);

            _sourceObjectsCount = UnityAPI.FindObjectsOfType<MeshCombineSource>().Length;
            _combinedObjectsCount = UnityAPI.FindObjectsOfType<CombinedObject>().Length;
        }

        public void DestroyCombinedObjects()
        {
            if (!EditorUtility.DisplayDialog("Confirmation", "Are you sure that you want to destroy all combined meshes?", "Yes", "Cancel"))
                return;

            MeshCombineSourcesController.DestroyCombinedMeshes();

            SetSourcesEnable(false, true);

            ClearSourcesInfo();

            RefreshSceneSourcesInfo();
        }


        public void CombineMeshes()
        {
            FilterSelectionsLists();

            if (_selectedRenderers.Count == 0 && _selectedLODGroups.Count == 0)
            {
                Debug.Log("MeshCombineController::Nothing to combine, no objects added");
                return;
            }

            if (_saveAssets)
            {
                if (!PathUtil.TryGetProjectRelative(_dataOutputPath, out _))
                {
                    Debug.Log("MeshCombineController::unsupported 'dataOutputPath'. Change before combine");
                    return;
                }
            }

            CreateBinaryTree();

            List<BinaryTreeNode> nodes = new List<BinaryTreeNode>();

            _binaryTree.GetNotEmptyLeafs(nodes);

            List<MeshCombineGroup> meshCombineGroups = new List<MeshCombineGroup>();
            List<LODCombineGroup> lodCombineGroups = new List<LODCombineGroup>();

            List<MeshCombineInstance> meshInstances = new List<MeshCombineInstance>();
            List<LODCombineInstance> lodInstances = new List<LODCombineInstance>();

            RendererCombiner rendererCombiner = new RendererCombiner(new UnityMeshCombiner(), new SkinnedMeshCombiner());
            LODGroupCombiner lodCombiner = new LODGroupCombiner(new UnityMeshCombiner(), new SkinnedMeshCombiner());

            List<GameObject> combinedObjects = new List<GameObject>();

            for (int i = 0; i < nodes.Count; i++)
            {
                float progress = (float)i / nodes.Count;

                if (EditorUtility.DisplayCancelableProgressBar("Progress...",  $"Combined Groups {i} of {nodes.Count}", progress))
                    break;

                BinaryTreeNode node = nodes[i];

                meshInstances.Clear();
                lodInstances.Clear();

                meshCombineGroups.Clear();
                lodCombineGroups.Clear();

                combinedObjects.Clear();

                foreach (var nodeData in node.Datas)
                {
                    if (nodeData.renderer != null)
                        MeshCombineInstancesGatherer.Gather(nodeData.renderer, meshInstances);

                    if (nodeData.lodGroup != null)
                        MeshCombineInstancesGatherer.Gather(nodeData.lodGroup, lodInstances);
                }

                if (meshInstances.Count > 0)
                {
                    MeshCombineInstancesGrouper.CreateCombineGroups(meshInstances, _combineOptions, meshCombineGroups);

                    foreach (var meshGroup in meshCombineGroups)
                    {
                        GameObject combinedGO;

                        MeshCombineResult result = rendererCombiner.CreateCombinedObject(meshGroup.Instances, _combineOptions, out combinedGO);

                        ProcessCombineResult(combinedGO, result);

                        if (result.success)
                            combinedObjects.Add(combinedGO);
                    }
                }

                if (lodInstances.Count > 0)
                {
                    LODCombineInstancesGrouper.CreateCombineGroups(lodInstances, lodCombineGroups);

                    foreach (var lodGroup in lodCombineGroups)
                    {
                        LODGroup combinedGroup;

                        MeshCombineResult[] results = lodCombiner.CreateCombinedObject(lodGroup.Instances, _combineOptions, out combinedGroup);

                        foreach (var result in results)
                            ProcessCombineResult((combinedGroup == null ? null : combinedGroup.gameObject), result);

                        if (combinedGroup != null)
                            combinedObjects.Add(combinedGroup.gameObject);
                    }
                }

                if (_saveAssets)
                    ExportAssets(combinedObjects);
            }

            EditorUtility.ClearProgressBar();

            RefreshSceneSourcesInfo();
            SetSourcesEnable(true, false);
        }


        private string CreateDefaultDataOutputPath()
        {
            return $"Assets/slo_output/{SceneManager.GetActiveScene().name.ToLower()}_{Mathf.Abs(SceneManager.GetActiveScene().GetHashCode())}";
        }

        private void TryAddRenderer(Renderer renderer)
        {
            if (renderer == null)
                return;

            if (_selectedRenderers.Contains(renderer))
                return;

            if (_includeOnlyStatic)
            {
                if (!renderer.gameObject.isStatic)
                    return;
            }

            if (MeshCombineSourcesFilter.IsPartOfLODGroup(renderer))
                return;

            if (renderer is MeshRenderer)
            {
                if (!_includeMeshRenderers)
                    return;

                if (!MeshCombineSourcesFilter.Filter((MeshRenderer)renderer, out string failureMessage))
                {
                    Debug.Log($"Can't add {renderer.name}: {failureMessage}");
                    return;
                }
            }
            else if (renderer is SkinnedMeshRenderer)
            {
                if (!_includeSkinnedMeshRenderers)
                    return;

                if (!MeshCombineSourcesFilter.Filter((SkinnedMeshRenderer)renderer, out string failureMessage))
                {
                    Debug.Log($"Can't add {renderer.name}: {failureMessage}");
                    return;
                }
            }

            _selectedRenderers.Add(renderer);
        }

        private void TryAddLODGroup(LODGroup lodGroup)
        {
            if (!_includeLODGroups)
                return;

            if (lodGroup == null)
                return;

            if (_selectedLODGroups.Contains(lodGroup))
                return;

            if (_includeOnlyStatic)
            {
                if (!lodGroup.gameObject.isStatic)
                    return;
            }

            if (!MeshCombineSourcesFilter.Filter(lodGroup, out string failureMessage))
            {
                Debug.Log($"Can't add {lodGroup.gameObject.name}: {failureMessage}");
                return;
            }

            _selectedLODGroups.Add(lodGroup);
        }

        private void FilterSelectionsLists()
        {
            int i = 0;
            while (i < _selectedRenderers.Count)
            {
                Renderer renderer = _selectedRenderers[i];

                if (renderer == null)
                {
                    _selectedRenderers.RemoveAt(i);
                    continue;
                }

                bool filter = false;
                string failureMessage = "";

                if (renderer is MeshRenderer)
                    filter = MeshCombineSourcesFilter.Filter((renderer as MeshRenderer), out failureMessage);

                else 
                    filter = MeshCombineSourcesFilter.Filter((renderer as SkinnedMeshRenderer), out failureMessage);

                if (!filter)
                {
                    Debug.Log($"Can't add {renderer.name}: {failureMessage}");

                    _selectedRenderers.RemoveAt(i);
                    continue;
                }

                i++;
            }


            i = 0;
            while (i < _selectedLODGroups.Count)
            {
                LODGroup group = _selectedLODGroups[i];

                if (group == null)
                {
                    _selectedLODGroups.RemoveAt(i);
                    continue;
                }

                if (!MeshCombineSourcesFilter.Filter(group, out string failureMessage))
                {
                    Debug.Log($"Can't add {group.name}: {failureMessage}");

                    _selectedRenderers.RemoveAt(i);
                    continue;
                }

                i++;
            }
        }

        private void ProcessCombineResult(GameObject combinedGO, MeshCombineResult result)
        {
            if (!result.success)
            {
                Debug.LogError($"MeshCombineController::Error during combine process: {result.errorMessage}");
            }
            else
            {
                if (!combinedGO.TryGetComponent<CombinedObject>(out _))
                {
                    combinedGO.AddComponent<CombinedObject>();
                    combinedGO.transform.parent = transform;
                }
            }

            if (result.instances != null)
            {
                for (int i = 0; i < result.instances.Length; i++)
                {
                    MeshCombineInstance instance = result.instances[i];

                    MeshCombineSource sourceComponent = instance.renderer.GetComponent<MeshCombineSource>();

                    if (sourceComponent == null)
                        sourceComponent = instance.renderer.gameObject.AddComponent<MeshCombineSource>();

                    sourceComponent.CreateCombineReport(result, i);
                }
            }
        }

        private void ExportAssets(List<GameObject> combinedObjects)
        {
            List<GameObject> gosForExport = new List<GameObject>();

            foreach (var combinedGO in combinedObjects)
            {
                foreach (var renderer in combinedGO.GetComponentsInChildren<Renderer>())
                {
                    gosForExport.Add(renderer.gameObject);
                }
            }

            AssetsExporter.ExportMissingAssets(gosForExport, _dataOutputPath, true, false, "CombinedMeshes");

            EditorUtility.UnloadUnusedAssetsImmediate();
        }


        private static class MeshCombineSourcesFilter
        {
            public static bool IsPartOfLODGroup(Renderer renderer)
            {
                foreach (var group in renderer.GetComponentsInParent<LODGroup>())
                {
                    if (group.GetLODs().SelectMany(l => l.renderers).Contains(renderer))
                        return true;
                }

                return false;
            }

            public static bool Filter(MeshRenderer renderer, out string failureMessage)
            {
                if (!renderer.gameObject.activeInHierarchy)
                {
                    failureMessage = $"GameObject is disabled";
                    return false;
                }

                if (!renderer.enabled)
                {
                    failureMessage = $"Renderer is disabled";
                    return false;
                }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();

                if (filter == null)
                {
                    failureMessage = $"MeshFilter not found";
                    return false;
                }

                if (filter.sharedMesh == null)
                {
                    failureMessage = $"SharedMesh is empty";
                    return false;
                }  

                failureMessage = "";
                return true;
            }

            public static bool Filter(SkinnedMeshRenderer renderer, out string failureMessage)
            {
                if (!renderer.gameObject.activeInHierarchy)
                {
                    failureMessage = $"GameObject is disabled";
                    return false;
                }

                if (!renderer.enabled)
                {
                    failureMessage = $"Renderer is disabled";
                    return false;
                }

                if (renderer.sharedMesh == null)
                {
                    failureMessage = $"'sharedMesh' is null";
                    return false;
                }

                failureMessage = "";
                return true;
            }

            public static bool Filter(LODGroup lodGroup, out string failureMessage)
            {
                if (!lodGroup.gameObject.activeInHierarchy)
                {
                    failureMessage = $"GameObject is disabled";
                    return false;
                }

                if (!lodGroup.enabled)
                {
                    failureMessage = $"LODGroup is disabled";
                    return false;
                }

                failureMessage = "";
                return true;
            }
        }

        private static class MeshCombineInstancesGatherer
        {
            private static List<Material> _sharedMaterials;

            public static void Gather(Renderer renderer, List<MeshCombineInstance> outInstances)
            {
                if (_sharedMaterials == null)
                    _sharedMaterials = new List<Material>();

                Mesh mesh;

                if (!renderer.TryGetSharedMesh(out mesh))
                    return;

                renderer.GetSharedMaterials(_sharedMaterials);

                int count = Mathf.Min(mesh.subMeshCount, _sharedMaterials.Count);

                for (int i = 0; i < count; i++)
                {
                    MeshCombineInstance instance = new MeshCombineInstance(renderer, i);

                    if (!instance.ReadyForCombine)
                        continue;

                    outInstances.Add(instance);
                }
            }

            public static void Gather(LODGroup group, List<LODCombineInstance> outInstances)
            {
                if (_sharedMaterials == null)
                    _sharedMaterials = new List<Material>();

                List<MeshCombineInstance> meshInstances = new List<MeshCombineInstance>();

                int lodLevel = 0;
                foreach (var lod in group.GetLODs())
                {
                    Renderer[] renderers = lod.renderers;

                    meshInstances.Clear();

                    for (int i = 0; i < renderers.Length; i++)
                        Gather(renderers[i], meshInstances);

                    for (int i = 0; i < meshInstances.Count; i++)
                    {
                        outInstances.Add(new LODCombineInstance()
                        {
                            lodGroup = group,
                            lodLevel = lodLevel,
                            instance = meshInstances[i]
                        });
                    }

                    lodLevel++;
                }
            }
        }
    }

#endif
}
