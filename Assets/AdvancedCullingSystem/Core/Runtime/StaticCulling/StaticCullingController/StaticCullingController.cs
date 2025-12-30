using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    public class StaticCullingController : MonoBehaviour
    {
        public IReadOnlyList<CameraZone> CameraZones
        {
            get
            {
                return _cameraZones;
            }
        }

        public int GeometryTreeDepth
        {
            get
            {
                return _geometryTreeDepth;
            }

            set
            {
                _geometryTreeDepth = Mathf.Clamp(value, 7, 20);
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
                _cellSize = Mathf.Max(value, 0.1f);
            }
        }
        public int TotalCellsCount
        {
            get
            {
                return _totalCellsCount;
            }
        }
        public float RaysPerUnit
        {
            get
            {
                return _raysPerUnit;
            }
            set
            {
                _raysPerUnit = Mathf.Max(0.1f, value);
            }
        }
        public int MaxRaysPerSource
        {
            get
            {
                return _maxRaysPerSource;
            }
            set
            {
                _maxRaysPerSource = Mathf.Max(10, value);
            }
        }

        [SerializeField]
        private List<CameraZone> _cameraZones;

        [SerializeField]
        private GeometryTree _geometryTree;

        [SerializeField]
        private int _geometryTreeDepth = 11;

        [SerializeField]
        private float _cellSize = 5f;

        [SerializeField]
        private int _totalCellsCount;

        [SerializeField]
        private float _raysPerUnit = 10f;

        [SerializeField]
        private int _maxRaysPerSource = 300;


        public bool AddCameraZone(CameraZone zone)
        {
            if (zone == null)
                return false;

            if (_cameraZones == null)
                _cameraZones = new List<CameraZone>();

            if (_cameraZones.Contains(zone))
                return false;

            _cameraZones.Add(zone);

            return true;
        }

        public bool RemoveCameraZone(CameraZone zone)
        {
            if (zone == null)
                return false;

            if (_cameraZones == null)
                return false;

            if (!_cameraZones.Contains(zone))
                return false;

            return _cameraZones.Remove(zone);
        }

        public void CreatePreviewGeometryTree()
        {
            string error;

            if (!ReadyToCreateGeometryTree(out error))
            {
                Debug.Log("Unable to create GeometryTree : " + error);
                return;
            }

            List<StaticCullingSource> validSources = new List<StaticCullingSource>();

            foreach (var source in FindObjectsOfType<StaticCullingSource>())
            {
                if (source.Validate())
                {
                    source.PrepareForBaking();
                    validSources.Add(source);
                }
            }

            CreateGeometryTree(validSources, out error);

            foreach (var source in validSources)
            {
                source.ClearAfterBaking();
                DestroyImmediate(source.gameObject.GetComponent<CullingTarget>());
            }
        }

        public void CreatePreviewCameraZones()
        {
            string error;

            if (!ReadyToBakeCameraZones(out error))
            {
                Debug.Log("Unable to bake camera zones : " + error);
                return;
            }

            if (!CreateVisibilityTrees(out error))
            {
                Debug.Log("Unable to bake camera zones : " + error);
            }
        }

        public void Bake()
        {
            string error;

            if (!ReadyToBake(out error))
            {
                Debug.Log("Unable to bake scene : " + error);
                return;
            }

            List<StaticCullingSource> sources;

            if (!PrepareForBake(out sources, out error))
            {
                Debug.Log("Baking process aborted");
                Debug.Log("Reason : " + error);
                ClearBakedData();
            }

            if (!CreateGeometryTree(sources, out error))
            {
                Debug.Log("Baking process aborted");
                Debug.Log("Reason : " + error);
                ClearBakedData();
            }

            if (!CreateVisibilityTrees(out error))
            {
                Debug.Log("Baking process aborted");
                Debug.Log("Reason : " + error);
                ClearBakedData();
            }

            if (BakeScene(_geometryTree, out error))
            {
                ClearAfterBaking(sources);
                Debug.Log("Scene sucessfully baked!");
            }
            else
            {
                Debug.Log("Baking process aborted");
                Debug.Log("Reason : " + error);
                ClearBakedData();
            }
        }

        public void Clear()
        {
            ClearBakedData();
        }


        private bool ReadyToCreateGeometryTree(out string error)
        {
            StaticCullingSource[] sources = FindObjectsOfType<StaticCullingSource>();

            if (sources == null || sources.Length == 0)
            {
                error = "StaticCullingSources not found. Add in 'Step 1'";
                return false;
            }

            foreach (var source in sources)
            {
                if (source.Validate())
                {
                    error = "";
                    return true;
                }
            }

            error = "Valid StaticCullingSources not found. Check in 'Step 1'";
            return false;
        }

        private bool ReadyToBakeCameraZones(out string error)
        {
            if (_cameraZones == null || _cameraZones.Count == 0)
            {
                error = "Camera Zones not added. Add in 'Step 3'";
                return false;
            }

            int i = 0;
            while (i < _cameraZones.Count)
            {
                if (_cameraZones[i] == null)
                    _cameraZones.RemoveAt(i);
                else
                    i++;
            }

            if (_cameraZones.Count == 0)
            {
                error = "Camera Zones not added. Add in 'Step 3'";
                return false;
            }

            error = "";
            return true;
        }

        private bool ReadyToBake(out string error)
        {
            if (!ReadyToCreateGeometryTree(out error))
                return false;

            if (!ReadyToBakeCameraZones(out error))
                return false;

            error = "";
            return true;
        }


        private bool PrepareForBake(out List<StaticCullingSource> sources, out string error)
        {
            sources = new List<StaticCullingSource>();

            try
            {
                StaticCullingSource[] sceneSources = FindObjectsOfType<StaticCullingSource>();

                foreach (var source in sceneSources)
                {
                    if (source.Validate())
                    {
                        source.PrepareForBaking();
                        sources.Add(source);
                    }
                }

                error = "";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message + ex.StackTrace;
                return false;
            }
        }

        private bool CreateGeometryTree(List<StaticCullingSource> sources, out string error)
        {
            try
            {
                _geometryTree = new GeometryTree(sources
                    .Select(s => s.CullingTarget)
                    .ToArray(), _geometryTreeDepth);

                error = "";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message + ex.StackTrace;
                return false;
            }
        }

        private bool CreateVisibilityTrees(out string error)
        {
            try
            {
                _totalCellsCount = 0;

                foreach (var zone in _cameraZones)
                {
                    if (zone != null)
                    {
                        zone.ClearVisibilityTree();
                        zone.CreateVisibilityTree(_cellSize);

                        _totalCellsCount += zone.CellsCount;
                    }
                }
               
                error = "";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message + ex.StackTrace;
                return false;
            }
        }

        private bool BakeScene(GeometryTree geometryTree, out string error)
        {
            StaticCullingBaker baker = new StaticCullingBaker(geometryTree);

            error = "";
            bool aborted = false;

            foreach (var zone in _cameraZones)
            {
                if (!baker.Bake(zone.VisibilityTree, _raysPerUnit, _maxRaysPerSource, out error))
                {
                    aborted = true;
                    break;
                }
            }

            baker.Dispose();

            return !aborted;
        }

        private void ClearAfterBaking(List<StaticCullingSource> sources)
        {
            foreach (var source in sources)
            {
                source.ClearAfterBaking();
                DestroyImmediate(source);
            }
        }

        private void ClearBakedData()
        {
            foreach (var source in FindObjectsOfType<StaticCullingSource>())
                source.ClearAfterBaking();

            int clearedCullingTargets = 0;

            foreach (var target in FindObjectsOfType<CullingTarget>())
            {
                target.gameObject.AddComponent<StaticCullingSource>();
                DestroyImmediate(target);
                clearedCullingTargets++;
            }

            int clearedCameraZones = 0;

            if (_cameraZones != null)
            {
                foreach (var zone in _cameraZones)
                {
                    if (zone != null)
                    {
                        zone.ClearVisibilityTree();
                        clearedCameraZones++;
                    }
                }
            }

            Debug.Log("Cleared Culling Targets : " + clearedCullingTargets);
            Debug.Log("Cleared Camera Zones : " + clearedCameraZones);
        }


#if UNITY_EDITOR

        public bool DrawGeometryTreeGizmo;
        public bool DrawCameraZones;

        private BinaryTreeDrawer _treeDrawer;

        private void OnDrawGizmos()
        {
            if (_treeDrawer == null)
                _treeDrawer = new BinaryTreeDrawer();

            if (DrawGeometryTreeGizmo && _geometryTree != null)
            {
                _treeDrawer.Color = Color.blue;
                _treeDrawer.DrawTreeGizmos(_geometryTree.Root);
            }

            if (DrawCameraZones)
            {
                if (_cameraZones != null)
                {
                    foreach (var zone in _cameraZones)
                    {
                        if (zone.VisibilityTree != null)
                        {
                            _treeDrawer.Color = Color.white;
                            _treeDrawer.DrawTreeGizmos(zone.VisibilityTree.Root);
                        }
                    }
                }
            }
        }

#endif
    }
}
