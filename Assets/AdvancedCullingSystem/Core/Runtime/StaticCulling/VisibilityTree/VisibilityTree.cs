using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    public class VisibilityTree : BinaryTree<VisibilityTreeNode, Vector3>
    {
        [field: SerializeField]
        public CullingTarget[] CullingTargets { get; private set; }


        public VisibilityTree(float cellSize) 
            : base(cellSize)
        {
           
        }

        public void SetTargets(CullingTarget[] targets)
        {
            CullingTargets = targets;
        }

        public void Optimize()
        {
            Optimize(RootInternal);
        }

        public void Apply()
        {
            ApplyData(RootInternal);
        }

        public void SetVisible(Vector3 point, Vector2 tolerance)
        {
            Bounds cameraBounds = new Bounds(point, new Vector3(tolerance.x, tolerance.y, tolerance.x));

            SetVisibleInternal(RootInternal, cameraBounds);
        }

        public void DrawCellsGizmo(Vector3 point, Vector2 tolerance)
        {
            Bounds cameraBounds = new Bounds(point, new Vector3(tolerance.x, tolerance.y, tolerance.x));

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(cameraBounds.center, cameraBounds.size);

            DrawCellsGizmoInternal(RootInternal, cameraBounds);
        }


        private void SetVisibleInternal(VisibilityTreeNode node, Bounds cameraBounds)
        {
            if (!node.Bounds.Intersects(cameraBounds))
                return;

            node.SetVisible();

            if (node.HasChilds)
            {
                SetVisibleInternal(node.Left, cameraBounds);
                SetVisibleInternal(node.Right, cameraBounds);
            }
        }

        private void Optimize(VisibilityTreeNode current)
        {
            if (current.IsLeaf)
                return;

            if (current.HasChilds)
            {
                Optimize(current.Left);
                Optimize(current.Right);
            }

            current.RemoveDuplicatesFromChilds();
        }

        private void ApplyData(VisibilityTreeNode current)
        {
            current.ApplyData();

            if (current.HasChilds)
            {
                ApplyData(current.Left);
                ApplyData(current.Right);
            }
        }

        private void DrawCellsGizmoInternal(VisibilityTreeNode node, Bounds cameraBounds)
        {
            if (!node.Bounds.Intersects(cameraBounds))
                return;

            if (node.IsLeaf)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(node.Center, node.Size);

                return;
            }

            if (node.HasChilds)
            {
                DrawCellsGizmoInternal(node.Left, cameraBounds);
                DrawCellsGizmoInternal(node.Right, cameraBounds);
            }
        }


        protected override Bounds GetBounds(Vector3 point)
        {
            return new Bounds(point, Vector3.one * 0.1f);
        }

        protected override VisibilityTreeNode CreateNode(Vector3 center, Vector3 size, bool isLeaf)
        {
            return new VisibilityTreeNode(this, center, size, isLeaf);
        }

        protected override void SetChildsToNode(VisibilityTreeNode parent, VisibilityTreeNode leftChild, VisibilityTreeNode rightChild)
        {
            parent.SetChilds(leftChild, rightChild);
        }

        protected override void AddDataToNode(VisibilityTreeNode node, Vector3 point)
        {

        }
    }
}