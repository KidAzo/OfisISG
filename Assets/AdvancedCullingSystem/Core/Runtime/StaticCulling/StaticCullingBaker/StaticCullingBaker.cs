using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

#if COLLECTIONS_1_3_1_OR_NEWER

using NativeHashMap_Int_UnsafeListInt = Unity.Collections.NativeParallelHashMap<int, Unity.Collections.LowLevel.Unsafe.UnsafeList<int>>;

#else

using NativeHashMap_Int_UnsafeListInt = Unity.Collections.NativeHashMap<int, Unity.Collections.LowLevel.Unsafe.UnsafeList<int>>;

#endif

namespace NGS.AdvancedCullingSystem.Static
{
    public partial class StaticCullingBaker : IDisposable
    {
        private const int MAX_PROCESSES = 50;
        private const int COMMANDS_LIMIT = 100000;

        private GeometryTree _geometryTree;
        private IReadOnlyList<CullingTarget> _cullingTargets;

        private NativeArray<GeometryNodeStruct> _geometryTreeStruct;
        private NativeArray<CullingTargetStruct> _cullingTargetsStruct;
        private NativeHashMap_Int_UnsafeListInt _cellTargetsMap;

        private int _startDepth;
        private float _raysPerUnit;
        private int _maxRays;
        private float _maxDistance;
        private int _layerMask;

        private List<VisibilityComputingProcess> _processes;

        public StaticCullingBaker(GeometryTree geometryTree)
        {
            int index = 0;

            _geometryTree = geometryTree;
            _geometryTreeStruct = new NativeArray<GeometryNodeStruct>(_geometryTree.NodesCount, Allocator.Persistent);

            FillGeometryTreeStruct((GeometryTreeNode)_geometryTree.Root, -1, false, ref index);

            _cullingTargets = geometryTree.CullingTargets;
            _cullingTargetsStruct = new NativeArray<CullingTargetStruct>(_cullingTargets.Count, Allocator.Persistent);
            _cellTargetsMap = new NativeHashMap_Int_UnsafeListInt(_geometryTreeStruct.Length, Allocator.Persistent);

            index = 0;
            FillTargetsIndexesDic(out Dictionary<CullingTarget, int> targetsIndexes);
            FillCellTargetsMap((GeometryTreeNode)_geometryTree.Root, targetsIndexes, ref index);

            _startDepth = 7;
            _maxDistance = _geometryTree.Root.Bounds.size.magnitude;
            _layerMask = LayerMask.GetMask(StaticCullingPreferences.LayerName);

            _processes = new List<VisibilityComputingProcess>();
        }

        public bool Bake(VisibilityTree visibilityTree, float raysPerUnit, int maxRaysPerSource, out string error)
        {
            error = "";
            bool success = false;

            _raysPerUnit = raysPerUnit;
            _maxRays = maxRaysPerSource;

            try
            {
                _processes.Clear();

                List<VisibilityTreeNode> cells = new List<VisibilityTreeNode>();

                FillVisibilityCells((VisibilityTreeNode)visibilityTree.Root, cells);

                int current = 0;
                int finishedCount = 0;
                int cellsCount = cells.Count;

                string title = "Processing...";

                while (finishedCount < cellsCount)
                {                   
#if UNITY_EDITOR
                    string info = string.Format("Finished : {0} / {1}", finishedCount, cellsCount);
                    float progress = (float)finishedCount / cellsCount;

                    if (UnityEditor.EditorUtility.DisplayCancelableProgressBar(title, info, progress))
                    {
                        error = "Cancelled";
                        break;
                    }
#endif
                    while (_processes.Count < MAX_PROCESSES && current < cellsCount)
                    {
                        _processes.Add(new VisibilityComputingProcess(cells[current], this));

                        current++;
                    }

                    int i = 0;
                    while (i < _processes.Count)
                    {
                        VisibilityComputingProcess process = _processes[i];

                        process.Update(out bool finished);

                        if (finished)
                        {
                            process.ApplyData();
                            process.Dispose();

                            _processes.RemoveAt(i);

                            finishedCount++;
                        }
                        else
                            i++;
                    }
                }

                success = finishedCount >= cellsCount;
            }
            catch (Exception ex)
            {
                error = ex.Message + "\n" + ex.StackTrace;
            }

            if (success)
            {
                visibilityTree.SetTargets(_geometryTree.CullingTargets.ToArray());
                visibilityTree.Optimize();
                visibilityTree.Apply();
            }
            else
            {
                foreach (var process in _processes)
                    process.Dispose();

                _processes.Clear();
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.ClearProgressBar();
#endif

            return success;
        }

        public void Dispose()
        {
            foreach (var process in _processes)
                process.Dispose();

            _processes.Clear();

            if (_geometryTreeStruct.IsCreated)
                _geometryTreeStruct.Dispose();

            if (_cullingTargetsStruct.IsCreated)
                _cullingTargetsStruct.Dispose();

            if (_cellTargetsMap.IsCreated)
            {
#if COLLECTIONS_1_3_1_OR_NEWER

                foreach (var pair in _cellTargetsMap)
                    pair.Value.Dispose();

#else
                using (var keys = _cellTargetsMap.GetKeyArray(Allocator.Temp))
                {
                    foreach (var key in keys)
                    {
                        _cellTargetsMap[key].Dispose();
                    }
                }
#endif

                _cellTargetsMap.Dispose();
            }
        }


        private void FillGeometryTreeStruct(GeometryTreeNode current, int parentIndex, bool isLeft, ref int index)
        {
            GeometryNodeStruct nodeStruct = new GeometryNodeStruct()
            {
                index = index,
                bounds = current.Bounds,
                left = -1,
                right = -1,
                isEmpty = current.IsEmpty,
                isLeaf = current.IsLeaf,
            };

            if (parentIndex >= 0)
            {
                GeometryNodeStruct parent = _geometryTreeStruct[parentIndex];

                if (isLeft)
                    parent.left = index;
                else
                    parent.right = index;

                _geometryTreeStruct[parentIndex] = parent;
            }

            _geometryTreeStruct[index++] = nodeStruct;

            if (current.HasChilds)
            {
                FillGeometryTreeStruct(current.Left, nodeStruct.index, true, ref index);
                FillGeometryTreeStruct(current.Right, nodeStruct.index, false, ref index);
            }
        }

        private void FillTargetsIndexesDic(out Dictionary<CullingTarget, int> targetsIndexes)
        {
            targetsIndexes = new Dictionary<CullingTarget, int>();

            for (int i = 0; i < _cullingTargets.Count; i++)
            {
                _cullingTargetsStruct[i] = new CullingTargetStruct
                {
                    index = i,
                    bounds = _cullingTargets[i].Bounds
                };

                targetsIndexes.Add(_cullingTargets[i], i);
            }
        }

        private void FillCellTargetsMap(GeometryTreeNode current, Dictionary<CullingTarget, int> targetToIndexDic, ref int index)
        {
            int nodeIndex = index;

            index++;

            if (current.IsLeaf)
            {
                UnsafeList<int> targets;

                if (current.CullingTargets == null || current.CullingTargets.Count == 0)
                {
                    targets = new UnsafeList<int>(0, Allocator.Persistent);
                }
                else
                {
                    targets = new UnsafeList<int>(current.CullingTargets.Count, Allocator.Persistent);

                    foreach (var target in current.CullingTargets)
                        targets.Add(targetToIndexDic[target]);
                }

                _cellTargetsMap.Add(nodeIndex, targets);
            }
            else if (current.HasChilds)
            {
                FillCellTargetsMap(current.Left, targetToIndexDic, ref index);
                FillCellTargetsMap(current.Right, targetToIndexDic, ref index);
            }
        }

        private void FillVisibilityCells(VisibilityTreeNode current, List<VisibilityTreeNode> result)
        {
            if (current.IsLeaf)
            {
                result.Add(current);
            }
            else if (current.HasChilds)
            {
                FillVisibilityCells(current.Left, result);
                FillVisibilityCells(current.Right, result);
            }
        }
    }
}
