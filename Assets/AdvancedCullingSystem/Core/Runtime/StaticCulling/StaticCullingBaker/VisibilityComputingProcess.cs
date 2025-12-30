using System;
using System.Collections.Generic;
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
    public partial class StaticCullingBaker
    {
        private class VisibilityComputingProcess : IDisposable
        {
            private VisibilityTreeNode _cell;
            private Vector3 _cellPosition;
            private float _cellSize;

            private NativeArray<GeometryNodeStruct> _geometryTreeStruct;
            private NativeArray<CullingTargetStruct> _cullingTargetsStruct;
            private NativeHashMap_Int_UnsafeListInt _nodeTargetsMap;

            private NativeArray<bool> _geometryTreeVisibility;
            private NativeArray<bool> _cullingTargetsVisibility;
            private NativeArray<bool> _computedCullingTargets;

            private NativeList<RaycastBatchInfo> _raycastBatches;
            private NativeList<RaycastCommand> _commands;
            private NativeList<RaycastHit> _hits;
            private NativeArray<int> _lastNodeIndex;
            private NativeArray<int> _lastTargetIndex;

            private float _raysPerUnit;
            private int _maxRays;
            private float _maxDistance;
            private int _layerMask;

            private int _startDepth;
            private int _treeHeight;
            private int _depth;

            private JobHandle _handle;
            private int _jobIndex;

            public VisibilityComputingProcess(VisibilityTreeNode cell, StaticCullingBaker context)
            {
                _cell = cell;
                _cellPosition = _cell.Center;
                _cellSize = Mathf.Min(cell.Size.x, cell.Size.y, cell.Size.z);

                _geometryTreeStruct = context._geometryTreeStruct;
                _cullingTargetsStruct = context._cullingTargetsStruct;
                _nodeTargetsMap = context._cellTargetsMap;

                _geometryTreeVisibility = new NativeArray<bool>(_geometryTreeStruct.Length, Allocator.TempJob);
                _cullingTargetsVisibility = new NativeArray<bool>(_cullingTargetsStruct.Length, Allocator.TempJob);
                _computedCullingTargets = new NativeArray<bool>(_cullingTargetsStruct.Length, Allocator.TempJob);

                _raycastBatches = new NativeList<RaycastBatchInfo>(COMMANDS_LIMIT, Allocator.TempJob);
                _commands = new NativeList<RaycastCommand>(COMMANDS_LIMIT + context._maxRays, Allocator.TempJob);
                _hits = new NativeList<RaycastHit>(COMMANDS_LIMIT + context._maxRays, Allocator.TempJob);
                _lastNodeIndex = new NativeArray<int>(1, Allocator.TempJob);
                _lastTargetIndex = new NativeArray<int>(1, Allocator.TempJob);

                _startDepth = context._startDepth;
                _raysPerUnit = context._raysPerUnit;
                _maxRays = context._maxRays;
                _maxDistance = context._maxDistance;
                _layerMask = context._layerMask;
                _treeHeight = context._geometryTree.Height;

                _depth = _startDepth;
            }

            public void Update(out bool finished)
            {
                finished = false;

                if (!_handle.IsCompleted)
                    return;

                _handle.Complete();

                if (_jobIndex == 0)
                {
                    RunJob0();

                    _jobIndex = 1;
                }
                else if (_jobIndex == 1)
                {
                    RunJob1();

                    if (_lastNodeIndex[0] == 0)
                    {
                        if (_depth == _treeHeight)
                        {
                            _jobIndex = 2;
                        }
                        else
                        {
                            _depth++;
                            _jobIndex = 0;
                        }
                    }
                    else
                    {
                        _jobIndex = 0;
                    }
                }
                else if (_jobIndex == 2)
                {
                    RunJob2();

                    _jobIndex = 3;
                }
                else if (_jobIndex == 3)
                {
                    RunJob3();

                    if (_lastNodeIndex[0] == 0 && _lastTargetIndex[0] == 0)
                    {
                        _jobIndex = 4;
                    }
                    else
                    {
                        _jobIndex = 2;
                    }
                }
                else if (_jobIndex == 4)
                {
                    finished = true;
                }
            }

            public void ApplyData()
            {
                for (int i = 0; i < _cullingTargetsVisibility.Length; i++)
                {
                    if (_cullingTargetsVisibility[i])
                        _cell.AddVisibleCullingTarget(i);
                }
            }

            public void Dispose()
            {
                _handle.Complete();

                _geometryTreeVisibility.Dispose();
                _cullingTargetsVisibility.Dispose();
                _computedCullingTargets.Dispose();

                _raycastBatches.Dispose();
                _commands.Dispose();
                _hits.Dispose();
                _lastNodeIndex.Dispose();
                _lastTargetIndex.Dispose();
            }


            private void RunJob0()
            {
                _handle = new TreeCreateRaysJob()
                {

                    cellPosition = _cellPosition,
                    cellSize = _cellSize,

                    geometryTreeStruct = _geometryTreeStruct,
                    geometryTreeVisibility = _geometryTreeVisibility,

                    rayBatches = _raycastBatches,
                    commands = _commands,
                    hits = _hits,

                    lastNodeIndex = _lastNodeIndex,

                    raysPerUnit = _raysPerUnit,
                    maxRays = _maxRays,
                    maxDistance = _maxDistance,
                    layerMask = _layerMask,

                    startDepth = _startDepth,
                    targetDepth = _depth,
                    commandsLimit = COMMANDS_LIMIT

                }.Schedule();
            }

            private void RunJob1()
            {
                _handle = new TreeComputeResultsJob()
                {

                    cellPosition = _cellPosition,

                    geometryTree = _geometryTreeStruct,
                    cullingTargetsStruct = _cullingTargetsStruct,
                    nodeTargetsMap = _nodeTargetsMap,

                    rayBatches = _raycastBatches,
                    commands = _commands,
                    hits = _hits,

                    geometryTreeVisibility = _geometryTreeVisibility,
                    cullingTargetsVisibility = _cullingTargetsVisibility

                }.Schedule(RaycastCommand.ScheduleBatch(_commands, _hits, 1));
            }

            private void RunJob2()
            {
                _handle = new TargetsCreateRaysJob()
                {

                    cellPosition = _cellPosition,
                    cellSize = _cellSize,

                    geometryTreeStruct = _geometryTreeStruct,
                    cullingTargetsStruct = _cullingTargetsStruct,

                    geometryTreeVisibility = _geometryTreeVisibility,
                    cullingTargetsVisibility = _cullingTargetsVisibility,
                    computedCullingTargets = _computedCullingTargets,
                    nodeTargetsMap = _nodeTargetsMap,

                    rayBatches = _raycastBatches,
                    commands = _commands,
                    hits = _hits,

                    lastNodeIndex = _lastNodeIndex,
                    lastTargetIndex = _lastTargetIndex,

                    raysPerUnit = _raysPerUnit,
                    maxRays = _maxRays,
                    maxDistance = _maxDistance,
                    layerMask = _layerMask,

                    startDepth = _startDepth,
                    targetDepth = _depth,
                    commandsLimit = COMMANDS_LIMIT

                }.Schedule();
            }

            private void RunJob3()
            {
                _handle = new TargetsComputeResultsJob()
                {

                    cellPosition = _cellPosition,
                    geometryTree = _geometryTreeStruct,
                    cullingTargetsStruct = _cullingTargetsStruct,
                    nodeTargetsMap = _nodeTargetsMap,

                    rayBatches = _raycastBatches,
                    commands = _commands,
                    hits = _hits,

                    cullingTargetsVisibility = _cullingTargetsVisibility

                }.Schedule(RaycastCommand.ScheduleBatch(_commands, _hits, 1));
            }
        }


        [BurstCompile]
        private struct GeometryNodeStruct
        {
            public int index;
            public int left;
            public int right;
            public Bounds bounds;
            public bool isLeaf;
            public bool isEmpty;
        }

        [BurstCompile]
        private struct CullingTargetStruct
        {
            public int index;
            public Bounds bounds;
        }

        [BurstCompile]
        private struct RaycastBatchInfo
        {
            public int targetIndex;
            public int raysStart;
            public int raysEnd;
        }


        [BurstCompile]
        private struct TreeCreateRaysJob : IJob
        {
            private static readonly double g = 1.22074408460575947536;
            private static readonly double a1 = 1.0 / g;
            private static readonly double a2 = 1.0 / (g * g);
            private static readonly double a3 = 1.0 / (g * g * g);

            [ReadOnly]
            public NativeArray<GeometryNodeStruct> geometryTreeStruct;

            [ReadOnly]
            public NativeArray<bool> geometryTreeVisibility;

            [WriteOnly]
            public NativeList<RaycastCommand> commands;

            [WriteOnly]
            public NativeList<RaycastHit> hits;

            [WriteOnly]
            public NativeList<RaycastBatchInfo> rayBatches;

            public NativeArray<int> lastNodeIndex;

            public Vector3 cellPosition;
            public float cellSize;
            public float raysPerUnit;
            public int maxRays;
            public float maxDistance;
            public int layerMask;

            public int startDepth;
            public int targetDepth;
            public int commandsLimit;

            private int _commandsCount;


            public void Execute()
            {
                rayBatches.Clear();
                commands.Clear();
                hits.Clear();

                TraverseTree(geometryTreeStruct[0], 1);

                hits.Length = _commandsCount;

                if (_commandsCount <= commandsLimit)
                    lastNodeIndex[0] = 0;
            }

            private void TraverseTree(GeometryNodeStruct node, int depth)
            {
                if (node.isEmpty)
                    return;

                if (_commandsCount > commandsLimit)
                    return;

                if (depth == targetDepth)
                {
                    if (node.index > lastNodeIndex[0])
                    {
                        if (!geometryTreeVisibility[node.index])
                        {
                            CreateRaysBatch(node);

                            if (_commandsCount > commandsLimit)
                                lastNodeIndex[0] = node.index;
                        }
                    }

                    return;
                }

                if (!geometryTreeVisibility[node.index] && depth >= startDepth)
                    return;

                if (node.left < 0)
                    return;

                TraverseTree(geometryTreeStruct[node.left], depth + 1);
                TraverseTree(geometryTreeStruct[node.right], depth + 1);
            }

            private void CreateRaysBatch(GeometryNodeStruct node)
            {
                Bounds bounds = node.bounds;

                if (bounds.Contains(cellPosition))
                {
                    rayBatches.AddNoResize(new RaycastBatchInfo()
                    {
                        targetIndex = node.index,
                        raysStart = -1,
                        raysEnd = -1,
                    });

                    return;
                }

                float distance = Vector3.Distance(bounds.center, cellPosition);
                float distanceRatio = Mathf.Max((distance / maxDistance), 0.01f);

                int raysCount = Mathf.RoundToInt(5 + bounds.size.magnitude * raysPerUnit * distanceRatio);
                raysCount = Mathf.Min(raysCount, maxRays);

                rayBatches.AddNoResize(new RaycastBatchInfo()
                {
                    targetIndex = node.index,
                    raysStart = _commandsCount,
                    raysEnd = _commandsCount + raysCount,
                });

                _commandsCount += raysCount;

                for (int i = 0; i < raysCount; i++)
                {
                    Vector3 targetPoint = GetPointInsideBoundingBox(i, bounds);
                    Vector3 dir = (targetPoint - cellPosition).normalized;

                    RaycastCommand command = UnityAPI.NewRaycastCommand(cellPosition, dir, layerMask: layerMask);

                    commands.AddNoResize(command);
                }
            }

            private Vector3 GetPointInsideBoundingBox(int index, Bounds bounds)
            {
                Vector3 size = bounds.size;

                float x = (float)(0.5 + a1 * index) % 1;
                float y = (float)(0.5 + a2 * index) % 1;
                float z = (float)(0.5 + a3 * index) % 1;

                Vector3 offset = new Vector3(x * size.x, y * size.y, z * size.z);

                return bounds.min + offset;
            }
        }

        [BurstCompile]
        private struct TreeComputeResultsJob : IJob
        {
            [ReadOnly]
            public Vector3 cellPosition;

            [ReadOnly]
            public NativeArray<GeometryNodeStruct> geometryTree;

            [ReadOnly]
            public NativeArray<CullingTargetStruct> cullingTargetsStruct;

            [ReadOnly]
            public NativeHashMap_Int_UnsafeListInt nodeTargetsMap;

            [ReadOnly]
            public NativeList<RaycastBatchInfo> rayBatches;

            [ReadOnly]
            public NativeList<RaycastCommand> commands;

            [ReadOnly]
            public NativeList<RaycastHit> hits;

            public NativeArray<bool> geometryTreeVisibility;
            public NativeArray<bool> cullingTargetsVisibility;

            public void Execute()
            {
                for (int i = 0; i < rayBatches.Length; i++)
                {
                    RaycastBatchInfo raycastBatch = rayBatches[i];
                    GeometryNodeStruct node = geometryTree[raycastBatch.targetIndex];

                    if (raycastBatch.raysStart < 0)
                    {
                        geometryTreeVisibility[node.index] = true;
                        continue;
                    }

                    int start = raycastBatch.raysStart;
                    int end = raycastBatch.raysEnd;

                    int tracedRays = 0;
                    int tracedPoints = 0;

                    for (int c = start; c < end; c++)
                    {
                        RaycastHit hit = hits[c];
                        RaycastCommand command = commands[c];
                        Ray ray = new Ray(command.from, command.direction);

                        float targetDistance = 0;
                        float hitDistance = hit.distance;

                        node.bounds.IntersectRay(ray, out targetDistance);

                        if (hitDistance < 0.0001f)
                        {
                            hitDistance = float.MaxValue;
                        }
                        else if (tracedPoints < 5)
                        {
                            TracePoint(geometryTree[0], hit.point);
                            tracedPoints++;
                        }

                        if (hitDistance > targetDistance)
                        {
                            TraceRay(geometryTree[0], ray, hitDistance);

                            tracedRays++;

                            if (tracedRays >= 5)
                                break;
                        }
                    }
                }
            }

            private void TraceRay(GeometryNodeStruct node, Ray ray, float hitDistance)
            {
                if (node.isEmpty)
                    return;

                if (node.bounds.IntersectRay(ray, out float nodeIntersectDistance))
                {
                    if (nodeIntersectDistance < hitDistance)
                    {
                        geometryTreeVisibility[node.index] = true;

                        if (node.left != -1)
                        {
                            TraceRay(geometryTree[node.left], ray, hitDistance);
                            TraceRay(geometryTree[node.right], ray, hitDistance);
                        }
                    }
                }
            }

            private void TracePoint(GeometryNodeStruct node, Vector3 hitPoint)
            {
                if (node.isEmpty)
                    return;

                if (!node.bounds.Contains(hitPoint))
                    return;

                if (node.isLeaf)
                {
                    UnsafeList<int> targets = nodeTargetsMap[node.index];

                    for (int i = 0; i < targets.Length; i++)
                    {
                        int idx = targets[i];

                        if (cullingTargetsVisibility[idx])
                            continue;

                        CullingTargetStruct target = cullingTargetsStruct[idx];

                        if (target.bounds.Contains(hitPoint))
                            cullingTargetsVisibility[idx] = true;
                    }
                }
                else
                {
                    if (node.left >= 0)
                    {
                        TracePoint(geometryTree[node.left], hitPoint);
                        TracePoint(geometryTree[node.right], hitPoint);
                    }
                }
            }
        }

        [BurstCompile]
        private struct TargetsCreateRaysJob : IJob
        {
            private static readonly double g = 1.22074408460575947536;
            private static readonly double a1 = 1.0 / g;
            private static readonly double a2 = 1.0 / (g * g);
            private static readonly double a3 = 1.0 / (g * g * g);

            [ReadOnly]
            public Vector3 cellPosition;

            [ReadOnly]
            public float cellSize;

            [ReadOnly]
            public NativeArray<GeometryNodeStruct> geometryTreeStruct;

            [ReadOnly]
            public NativeArray<CullingTargetStruct> cullingTargetsStruct;

            [ReadOnly]
            public NativeHashMap_Int_UnsafeListInt nodeTargetsMap;

            [ReadOnly]
            public NativeArray<bool> geometryTreeVisibility;

            [ReadOnly]
            public NativeArray<bool> cullingTargetsVisibility;

            public NativeArray<bool> computedCullingTargets;

            [WriteOnly]
            public NativeList<RaycastBatchInfo> rayBatches;

            [WriteOnly]
            public NativeList<RaycastCommand> commands;

            [WriteOnly]
            public NativeList<RaycastHit> hits;
            public NativeArray<int> lastNodeIndex;
            public NativeArray<int> lastTargetIndex;

            public float raysPerUnit;
            public int maxRays;
            public float maxDistance;
            public int layerMask;

            public int startDepth;
            public int targetDepth;
            public int commandsLimit;

            private int _commandsCount;


            public void Execute()
            {
                rayBatches.Clear();
                commands.Clear();
                hits.Clear();

                TraverseTree(geometryTreeStruct[0], 1);

                hits.Length = _commandsCount;

                if (_commandsCount <= commandsLimit)
                {
                    lastNodeIndex[0] = 0;
                    lastTargetIndex[0] = 0;
                }
            }

            private void TraverseTree(GeometryNodeStruct node, int depth)
            {
                if (node.isEmpty)
                    return;

                if (_commandsCount > commandsLimit)
                    return;

                if (!geometryTreeVisibility[node.index] && depth >= startDepth)
                    return;

                if (node.isLeaf)
                {
                    if (node.index >= lastNodeIndex[0])
                    {
                        UnsafeList<int> targets = nodeTargetsMap[node.index];

                        int lastTarget = lastTargetIndex[0];

                        for (int i = 0; i < targets.Length; i++)
                        {
                            if (lastTarget != 0 && i <= lastTarget)
                                continue;

                            int targetIdx = targets[i];

                            if (computedCullingTargets[targetIdx])
                                continue;

                            if (cullingTargetsVisibility[targetIdx])
                            {
                                computedCullingTargets[targetIdx] = true;
                                continue;
                            }

                            CreateRaysBatch(cullingTargetsStruct[targetIdx]);
                            computedCullingTargets[targetIdx] = true;

                            if (_commandsCount > commandsLimit)
                            {
                                lastNodeIndex[0] = node.index;
                                lastTargetIndex[0] = i;
                                break;
                            }
                        }

                        if (_commandsCount <= commandsLimit)
                        {
                            if (lastNodeIndex[0] == node.index)
                                lastTargetIndex[0] = 0;
                        }

                        return;
                    }
                }

                if (node.left < 0)
                    return;

                TraverseTree(geometryTreeStruct[node.left], depth + 1);
                TraverseTree(geometryTreeStruct[node.right], depth + 1);
            }

            private void CreateRaysBatch(CullingTargetStruct target)
            {
                Bounds bounds = target.bounds;

                if (bounds.Contains(cellPosition))
                {
                    rayBatches.AddNoResize(new RaycastBatchInfo()
                    {
                        targetIndex = target.index,
                        raysStart = -1,
                        raysEnd = -1,
                    });

                    return;
                }

                float distance = Vector3.Distance(bounds.center, cellPosition);
                float distanceRatio = Mathf.Max((distance / maxDistance), 0.01f);

                int raysCount = Mathf.RoundToInt(5 + bounds.size.magnitude * raysPerUnit * distanceRatio);
                raysCount = Mathf.Min(raysCount, maxRays);

                rayBatches.AddNoResize(new RaycastBatchInfo()
                {
                    targetIndex = target.index,
                    raysStart = _commandsCount,
                    raysEnd = _commandsCount + raysCount,
                });

                _commandsCount += raysCount;

                for (int i = 0; i < raysCount; i++)
                {
                    Vector3 targetPoint = GetPointInsideBoundingBox(i, bounds);
                    Vector3 dir = (targetPoint - cellPosition).normalized;

                    RaycastCommand command = UnityAPI.NewRaycastCommand(cellPosition, dir, layerMask: layerMask);

                    commands.AddNoResize(command);
                }
            }

            private Vector3 GetPointInsideBoundingBox(int index, Bounds bounds)
            {
                Vector3 size = bounds.size;

                float x = (float)(0.5 + a1 * index) % 1;
                float y = (float)(0.5 + a2 * index) % 1;
                float z = (float)(0.5 + a3 * index) % 1;

                Vector3 offset = new Vector3(x * size.x, y * size.y, z * size.z);

                return bounds.min + offset;
            }
        }

        [BurstCompile]
        private struct TargetsComputeResultsJob : IJob
        {
            [ReadOnly]
            public Vector3 cellPosition;

            [ReadOnly]
            public NativeArray<GeometryNodeStruct> geometryTree;

            [ReadOnly]
            public NativeArray<CullingTargetStruct> cullingTargetsStruct;

            [ReadOnly]
            public NativeHashMap_Int_UnsafeListInt nodeTargetsMap;

            [ReadOnly]
            public NativeList<RaycastBatchInfo> rayBatches;

            [ReadOnly]
            public NativeList<RaycastCommand> commands;

            [ReadOnly]
            public NativeList<RaycastHit> hits;

            public NativeArray<bool> cullingTargetsVisibility;

            public void Execute()
            {
                for (int i = 0; i < rayBatches.Length; i++)
                {
                    RaycastBatchInfo raycastBatch = rayBatches[i];
                    CullingTargetStruct target = cullingTargetsStruct[raycastBatch.targetIndex];

                    if (raycastBatch.raysStart < 0)
                    {
                        cullingTargetsVisibility[target.index] = true;
                        continue;
                    }

                    int start = raycastBatch.raysStart;
                    int end = raycastBatch.raysEnd;

                    int tracedPoints = 0;

                    for (int c = start; c < end; c++)
                    {
                        RaycastHit hit = hits[c];
                        RaycastCommand command = commands[c];
                        Ray ray = new Ray(command.from, command.direction);

                        float targetDistance = 0;
                        float hitDistance = hit.distance;

                        target.bounds.IntersectRay(ray, out targetDistance);
                        targetDistance -= 0.01f;

                        if (hitDistance < 0.0001f)
                        {
                            hitDistance = float.MaxValue;
                        }
                        else if (tracedPoints < 2)
                        {
                            TracePoint(geometryTree[0], hit.point);
                            tracedPoints++;
                        }

                        if (hitDistance > targetDistance)
                        {
                            cullingTargetsVisibility[target.index] = true;
                            break;
                        }
                    }
                }
            }

            private void TracePoint(GeometryNodeStruct node, Vector3 hitPoint)
            {
                if (node.isEmpty)
                    return;

                if (!node.bounds.Contains(hitPoint))
                    return;

                if (node.isLeaf)
                {
                    UnsafeList<int> targets = nodeTargetsMap[node.index];

                    for (int i = 0; i < targets.Length; i++)
                    {
                        int idx = targets[i];

                        if (cullingTargetsVisibility[idx])
                            continue;

                        CullingTargetStruct target = cullingTargetsStruct[idx];

                        if (target.bounds.Contains(hitPoint))
                            cullingTargetsVisibility[idx] = true;
                    }
                }
                else
                {
                    if (node.left >= 0)
                    {
                        TracePoint(geometryTree[node.left], hitPoint);
                        TracePoint(geometryTree[node.right], hitPoint);
                    }
                }
            }
        }
    }
}
