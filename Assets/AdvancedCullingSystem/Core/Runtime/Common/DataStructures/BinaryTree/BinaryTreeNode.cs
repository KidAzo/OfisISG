using UnityEngine;

namespace NGS.AdvancedCullingSystem
{
    public abstract class BinaryTreeNode
    {
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
        public Bounds Bounds
        {
            get
            {
                return _bounds;
            }
        }
        public bool HasChilds
        {
            get
            {
                return GetLeft() != null;
            }
        }
        public bool IsLeaf
        {
            get
            {
                return _isLeaf;
            }
        }

        [SerializeField]
        private Bounds _bounds;

        [SerializeField]
        private bool _isLeaf;

        public BinaryTreeNode(Vector3 center, Vector3 size, bool isLeaf)
        {
            _bounds = new Bounds(center, size);
            _isLeaf = isLeaf;
        }

        public abstract BinaryTreeNode GetLeft();

        public abstract BinaryTreeNode GetRight();
    }
}
