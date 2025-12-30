using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    public class VisibilityTreeNode : BinaryTreeNode
    {
        public VisibilityTreeNode Left
        {
            get
            {
                return _left;
            }
        }
        public VisibilityTreeNode Right
        {
            get
            {
                return _right;
            }
        }

        [SerializeReference]
        private VisibilityTree _tree;

        [SerializeReference]
        private VisibilityTreeNode _left;

        [SerializeReference]
        private VisibilityTreeNode _right;

        [SerializeReference]
        private IVisibilityData _visibilityData;

        private HashSet<int> _uniqTargets;


        public VisibilityTreeNode(VisibilityTree tree, Vector3 center, Vector3 size, bool isLeaf) 
            : base(center, size, isLeaf)
        {
            _tree = tree;
        }

        public override BinaryTreeNode GetLeft()
        {
            return Left;
        }

        public override BinaryTreeNode GetRight()
        {
            return Right;
        }

        public void SetChilds(VisibilityTreeNode left, VisibilityTreeNode right)
        {
            _left = left;
            _right = right;
        }

        public void AddVisibleCullingTarget(int targetIndex)
        {
            if (_uniqTargets == null)
                _uniqTargets = new HashSet<int>();

            _uniqTargets.Add(targetIndex);
        }

        public void RemoveDuplicatesFromChilds()
        {
            if (!HasChilds)
                return;

            HashSet<int> leftTargets = Left._uniqTargets;
            HashSet<int> rightTargets = Right._uniqTargets;

            if (leftTargets == null || rightTargets == null)
                return;

            foreach (var target in leftTargets)
            {
                if (rightTargets.Contains(target))
                {
                    AddVisibleCullingTarget(target);
                }
            }

            if (_uniqTargets != null)
            {
                foreach (var target in _uniqTargets)
                {
                    leftTargets.Remove(target);
                    rightTargets.Remove(target);
                }
            }
        }

        public void ApplyData()
        {
            if (_uniqTargets == null || _uniqTargets.Count == 0)
                return;

            if (_uniqTargets.Any(t => t >= 65535))
                _visibilityData = new VisibilityData(_uniqTargets);
            else
                _visibilityData = new CompactVisibilityData(_uniqTargets);
        }

        public void SetVisible()
        {
            if (_visibilityData == null)
                return;

            try
            {
                _visibilityData.SetVisible(_tree.CullingTargets);
            }
            catch(MissingReferenceException)
            {
                Debug.Log("Looks like some of baked objects was destroyed. Rebake scene");
            }
        }
    }
}
