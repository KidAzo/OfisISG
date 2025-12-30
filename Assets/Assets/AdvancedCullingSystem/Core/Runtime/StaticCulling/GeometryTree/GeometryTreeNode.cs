using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    public class GeometryTreeNode : BinaryTreeNode
    {
        public int Index { get; set; }
        public bool IsEmpty { get; set; }

        public GeometryTreeNode Left { get; private set; }
        public GeometryTreeNode Right { get; private set; }
        public IReadOnlyList<CullingTarget> CullingTargets
        {
            get
            {
                return _targets;
            }
        }

        private HashSet<CullingTarget> _targetsSet;
        private List<CullingTarget> _targets;


        public GeometryTreeNode(Vector3 center, Vector3 size, bool isLeaf) 
            : base(center, size, isLeaf)
        {
            IsEmpty = true;
        }

        public override BinaryTreeNode GetLeft()
        {
            return Left;
        }

        public override BinaryTreeNode GetRight()
        {
            return Right;
        }

        public void SetChilds(GeometryTreeNode left, GeometryTreeNode right)
        {
            Left = left;
            Right = right;
        }

        public void AddCullingTarget(CullingTarget target)
        {
            if (_targetsSet == null)
            {
                _targetsSet = new HashSet<CullingTarget>();
                _targets = new List<CullingTarget>();
            }

            if (_targetsSet.Add(target))
                _targets.Add(target);
        }
    }
}
