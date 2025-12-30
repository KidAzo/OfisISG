using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    public class GeometryTree : BinaryTree<GeometryTreeNode, CullingTarget>
    {
        public IReadOnlyList<CullingTarget> CullingTargets { get; private set; }
        public int NodesCount { get; private set; }

        public GeometryTree(CullingTarget[] targets, int maxDepth) :
            base(targets, maxDepth)
        {
            CullingTargets = targets;
        }

        protected override GeometryTreeNode CreateNode(Vector3 center, Vector3 size, bool isLeaf)
        {
            NodesCount++;
            return new GeometryTreeNode(center, size, isLeaf);
        }

        protected override Bounds GetBounds(CullingTarget target)
        {
            return target.Bounds;
        }

        protected override void AddInternal(GeometryTreeNode node, CullingTarget data, int depth)
        {
            node.IsEmpty = false;

            base.AddInternal(node, data, depth);
        }

        protected override void AddDataToNode(GeometryTreeNode node, CullingTarget target)
        {
            node.AddCullingTarget(target);
        }

        protected override void SetChildsToNode(GeometryTreeNode parent, GeometryTreeNode left, GeometryTreeNode right)
        {
            parent.SetChilds(left, right);
        }
    }
}
