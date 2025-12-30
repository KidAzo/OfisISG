using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    [Serializable]
    public class BinaryTreeNode
    {
        public BinaryTreeNode Left
        {
            get
            {
                return _left;
            }
        }
        public BinaryTreeNode Right
        {
            get
            {
                return _right;
            }
        }

        public bool IsLeaf
        {
            get
            {
                return _isLeaf;
            }
        }
        public bool HasChilds
        {
            get
            {
                return _left != null;
            }
        }
        public bool HasData
        {
            get
            {
                return _datas != null && _datas.Count > 0;
            }
        }

        public Bounds Bounds
        {
            get
            {
                return _bounds;
            }
        }
        public Vector3 Center
        {
            get
            {
                return _bounds.center;
            }
        }
        public Vector3 Size
        {
            get
            {
                return _bounds.size;
            }
        }

        public IReadOnlyList<BinaryTreeData> Datas
        {
            get
            {
                return _datas;
            }
        }


        [SerializeReference]
        private BinaryTreeNode _left;

        [SerializeReference]
        private BinaryTreeNode _right;

        [SerializeField]
        private bool _isLeaf;

        [SerializeField]
        private Bounds _bounds;

        [SerializeField]
        private List<BinaryTreeData> _datas;


        public BinaryTreeNode(Vector3 center, Vector3 size, bool isLeaf)
        {
            _bounds = new Bounds(center, size);
            _isLeaf = isLeaf;
        }

        public void SetChilds(BinaryTreeNode left, BinaryTreeNode right)
        {
            _left = left;
            _right = right;
        }

        public bool Contains(Vector3 point)
        {
            return _bounds.Contains(point);
        }

        public void AddData(BinaryTreeData data)
        {
            if (_datas == null)
                _datas = new List<BinaryTreeData>();

            if (!_datas.Contains(data))
                _datas.Add(data);
        }
    }
}
