using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class StaticCullingCamera : MonoBehaviour
    {
        [SerializeField]
        private bool _drawCells;

        [Range(0, 1)]
        [SerializeField]
        private Vector2 _tolerance;

        private VisibilityTree _tree;


        private void Reset()
        {
            _tolerance = Vector2.one;
        }

        private void Start()
        {
            if (CameraZone.Instances.Count == 0)
            {
                Debug.Log("StaticCullingCamera : Not found Camera Zones in scene");
                enabled = false;
                return;
            }

            _tree = FindNearestVisibilityTree();

            if (_tree == null)
            {
                Debug.Log("StaticCullingCamera : Can't find nearest CameraZone");
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            Vector3 point = transform.position;

            if (_tree == null || !_tree.Root.Bounds.Contains(point))
            {
                _tree = FindNearestVisibilityTree();

                if (_tree == null)
                    return;
            }
            
            _tree.SetVisible(point, _tolerance);
        }

        private VisibilityTree FindNearestVisibilityTree()
        {
            if (CameraZone.Instances.Count == 0)
                return null;

            Vector3 point = transform.position;

            foreach (var zone in CameraZone.Instances)
            {
                if (zone == null)
                    continue;

                VisibilityTree tree = zone.VisibilityTree;

                if (tree == null || tree.CullingTargets == null)
                    continue;

                if (tree.Root.Bounds.Contains(point))
                    return tree;
            }

            return null;
        }


#if UNITY_EDITOR

        public static bool DrawGizmo;

        private void OnDrawGizmos()
        {
            if (DrawGizmo)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(transform.position, Vector3.one);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawCells || _tree == null)
                return;
            
            _tree.DrawCellsGizmo(transform.position, _tolerance);
        }

#endif
    }
}
