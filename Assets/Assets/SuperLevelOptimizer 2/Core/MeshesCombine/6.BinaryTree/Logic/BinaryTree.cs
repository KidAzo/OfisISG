using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    [Serializable]
    public class BinaryTree
    {
        public BinaryTreeNode Root
        {
            get
            {
                return _root;
            }
        }

        [SerializeReference]
        private BinaryTreeNode _root;

        [SerializeField]
        private int _height;

        [SerializeField]
        private float _cellSize;

        public BinaryTree(float cellSize)
        {
            _height = 0;
            _cellSize = Mathf.Max(cellSize, 0.01f);
        }

        public void CreateTree(IReadOnlyList<BinaryTreeData> datas)
        {
            if (_root == null)
            {
                _root = CreateRoot(datas);
                _height = 1;
            }

            foreach (var data in datas)
            {
                if (!_root.Contains(data.position))
                    GrowTreeUp(data);

                Add(_root, data, 1);
            }
        }

        public void GetNotEmptyLeafs(List<BinaryTreeNode> outLeafs)
        {
            if (_root == null)
                return;

            GetNotEmptyLeafs(_root, outLeafs);
        }

        public void Clear()
        {
            _root = null;
            _height = 0;
        }


        private BinaryTreeNode CreateRoot(IReadOnlyList<BinaryTreeData> datas)
        {
            Vector3 min = Vector3.one * float.MaxValue;

            foreach(var data in datas)
            {
                min.x = Mathf.Min(data.position.x, min.x);
                min.y = Mathf.Min(data.position.y, min.y);
                min.z = Mathf.Min(data.position.z, min.z);
            }

            return new BinaryTreeNode(min, Vector3.one * _cellSize, true);
        }

        private BinaryTreeNode ExpandRoot(BinaryTreeNode root, BinaryTreeData target)
        {
            Bounds rootBounds = root.Bounds;
            Vector3 targetPosition = target.position;

            Vector3 parentCenter = Vector3.zero;
            Vector3 parentSize = Vector3.zero;

            Vector3 childCenter = Vector3.zero;

            bool rootIsLeft = false;

            for (int i = 0; i < 3; i++)
            {
                if (targetPosition[i] < rootBounds.min[i])
                {
                    parentSize = rootBounds.size;
                    parentSize[i] *= 2;

                    parentCenter = rootBounds.center;
                    parentCenter[i] -= rootBounds.size[i] / 2;

                    childCenter = rootBounds.center;
                    childCenter[i] -= rootBounds.size[i];

                    break;
                }

                if (targetPosition[i] > rootBounds.max[i])
                {
                    parentSize = rootBounds.size;
                    parentSize[i] *= 2;

                    parentCenter = rootBounds.center;
                    parentCenter[i] += rootBounds.size[i] / 2;

                    childCenter = rootBounds.center;
                    childCenter[i] += rootBounds.size[i];

                    rootIsLeft = true;

                    break;
                }
            }

            BinaryTreeNode parent = new BinaryTreeNode(parentCenter, parentSize, false);
            BinaryTreeNode child = new BinaryTreeNode(childCenter, rootBounds.size, root.IsLeaf);

            if (rootIsLeft)
                parent.SetChilds(_root, child);
            else
                parent.SetChilds(child, _root);

            return parent;
        }

        private void GrowTreeUp(BinaryTreeData target)
        {
            if (_root.Contains(target.position))
                return;

            _root = ExpandRoot(_root, target);
            _height++;

            GrowTreeUp(target);
        }

        private void GrowTreeDown(BinaryTreeNode node, BinaryTreeData target, int currentDepth)
        {
            if (node.HasChilds)
                throw new Exception("GrowTreeDown::" + currentDepth + " node already has childs");

            Bounds nodeBounds = node.Bounds;
            Vector3 offset;
            Vector3 size;

            if (nodeBounds.size.x > nodeBounds.size.y && nodeBounds.size.x > nodeBounds.size.z)
            {
                offset = new Vector3(nodeBounds.size.x / 4, 0, 0);
                size = new Vector3(nodeBounds.size.x / 2, nodeBounds.size.y, nodeBounds.size.z);
            }
            else if (nodeBounds.size.y > nodeBounds.size.x || nodeBounds.size.y > nodeBounds.size.z)
            {
                offset = new Vector3(0, nodeBounds.size.y / 4, 0);
                size = new Vector3(nodeBounds.size.x, nodeBounds.size.y / 2, nodeBounds.size.z);
            }
            else
            {
                offset = new Vector3(0, 0, nodeBounds.size.z / 4);
                size = new Vector3(nodeBounds.size.x, nodeBounds.size.y, nodeBounds.size.z / 2);
            }

            bool isLeaf = (currentDepth == _height);

            BinaryTreeNode left = new BinaryTreeNode(nodeBounds.center - offset, size, isLeaf);
            BinaryTreeNode right = new BinaryTreeNode(nodeBounds.center + offset, size, isLeaf);

            node.SetChilds(left, right);

            if (isLeaf)
                return;

            if (left.Contains(target.position))
                GrowTreeDown(left, target, currentDepth + 1);

            if (right.Contains(target.position))
                GrowTreeDown(right, target, currentDepth + 1);
        }

        private void Add(BinaryTreeNode node, BinaryTreeData data, int currentDepth)
        {
            if (node.IsLeaf)
            {
                node.AddData(data);
                return;
            }

            if (!node.HasChilds)
                GrowTreeDown(node, data, currentDepth + 1);

            BinaryTreeNode left = node.Left;
            BinaryTreeNode right = node.Right;

            if (left.Contains(data.position))
                Add(left, data, currentDepth + 1);

            else
                Add(right, data, currentDepth + 1);
        }

        private void GetNotEmptyLeafs(BinaryTreeNode current, List<BinaryTreeNode> outLeafs)
        {
            if (current.IsLeaf)
            {
                if (current.HasData)
                {
                    outLeafs.Add(current);
                    return;
                }
            }

            if (!current.HasChilds)
                return;

            GetNotEmptyLeafs(current.Left, outLeafs);
            GetNotEmptyLeafs(current.Right, outLeafs);
        }
    }
}
