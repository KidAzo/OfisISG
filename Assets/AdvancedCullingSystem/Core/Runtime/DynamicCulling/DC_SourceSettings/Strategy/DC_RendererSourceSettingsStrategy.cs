using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    [Serializable]
    public class DC_RendererSourceSettingsStrategy : IDC_SourceSettingsStrategy
    {
        [field: SerializeField]
        public bool ReadyForCulling { get; private set; }

        public CullingMethod CullingMethod
        {
            get
            {
                return _cullingMethod;
            }
            set
            {
                _cullingMethod = value;
            }
        }
        public bool ConvexCollider
        {
            get
            {
                return _convexCollider;
            }
            set
            {
                _convexCollider = value;
            }
        }

        [SerializeField]
        private DC_SourceSettings _context;

        [SerializeField]
        private CullingMethod _cullingMethod;

        [SerializeField]
        private bool _convexCollider;

        [SerializeField]
        private MeshRenderer _renderer;

        [SerializeField]
        private Mesh _mesh;

        [SerializeField]
        private MeshCollider _collider;

        [SerializeField]
        private bool _rigibodiesChecked;


        public DC_RendererSourceSettingsStrategy(DC_SourceSettings context)
        {
            _context = context;
        }

        public bool CheckCompatibilityAndGetComponents(out string incompatibilityReason)
        {
            if (_renderer == null)
            {
                if (!_context.TryGetComponent(out _renderer))
                {
                    incompatibilityReason = "MeshRenderer not found";
                    return false;
                }
            }

            if (_mesh == null)
            {
                MeshFilter filter = _context.GetComponent<MeshFilter>();

                if (filter == null)
                {
                    incompatibilityReason = "MeshFilter not found";
                    return false;
                }

                _mesh = filter.sharedMesh;

                if (_mesh == null)
                {
                    incompatibilityReason = "Mesh not found";
                    return false;
                }
            }

            if (!_rigibodiesChecked)
            {
                foreach (var rb in _context.GetComponentsInParent<Rigidbody>())
                {
                    if (!rb.isKinematic)
                    {
                        ConvexCollider = true;
                        break;
                    }
                }

                _rigibodiesChecked = true;
            }

            incompatibilityReason = "";
            return true;
        }

        public void PrepareForCulling()
        {
            if (ReadyForCulling)
                return;

            GameObject go = new GameObject("DC_Collider");

            go.transform.parent = _renderer.transform;
            go.layer = _context.CullingLayer;

            go.transform.localPosition = Vector3.zero;
            go.transform.localEulerAngles = Vector3.zero;
            go.transform.localScale = Vector3.one;

            _collider = go.AddComponent<MeshCollider>();
            _collider.sharedMesh = _mesh;

            if (ConvexCollider)
                _collider.convex = true;

            ReadyForCulling = true;
        }

        public void ClearData()
        {
            if (!ReadyForCulling)
                return;

            if (_collider != null)
                UnityEngine.Object.DestroyImmediate(_collider.gameObject);

            _collider = null;

            ReadyForCulling = false;
        }


        public bool TryGetBounds(ref Bounds bounds)
        {
            if (_renderer != null)
            {
                bounds = _renderer.bounds;
                return true;
            }

            return false;
        }

        public ICullingTarget CreateCullingTarget()
        {
            if (CullingMethod == CullingMethod.KeepShadows)
                return new DC_RendererShadowsTarget(_renderer);

            return new DC_RendererTarget(_renderer);
        }

        public IEnumerable<Collider> GetColliders()
        {
            if (_collider == null)
                yield break;

            yield return _collider;
        }
    }
}
