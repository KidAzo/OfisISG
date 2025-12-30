using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    public class LODGroupStaticCullingSourceStrategy : IStaticCullingSourceStrategy
    {
        [SerializeField]
        private GameObject _context;

        [SerializeField]
        private CullingMethod _cullingMethod;

        [SerializeField]
        private bool _isOccluder;

        [SerializeField]
        private LODGroup _lodGroup;

        [SerializeField]
        private List<Renderer> _renderers;

        [SerializeField]
        private Bounds? _localBounds;

        [SerializeField]
        private List<Collider> _colliders;


        public LODGroupStaticCullingSourceStrategy(GameObject context)
        {
            _context = context;

            LODGroupCullingTarget target = context.GetComponent<LODGroupCullingTarget>();

            if (target != null)
                _isOccluder = target.IsOccluder;
            else
                _isOccluder = true;
        }

        public bool Validate(out string errorMessage)
        {
            _lodGroup = _context.GetComponent<LODGroup>();

            if (_lodGroup == null)
            {
                errorMessage = "LODGroup not found";
                return false;
            }

            if (_isOccluder)
            {
                LOD lod = _lodGroup.GetLODs()[0];
                bool containsRenderersForColliders = false;

                for (int i = 0; i < lod.renderers.Length; i++)
                {
                    if (CheckRenderer(lod.renderers[i]))
                    {
                        containsRenderersForColliders = true;
                        break;
                    }
                }

                if (!containsRenderersForColliders)
                {
                    errorMessage = "Not found valid Renderers on LOD0 for creating colliders";
                    return false;
                }
            }

            CollectRenderers();

            if (_renderers.Count == 0)
            {
                errorMessage = "Not found valid Renderers";
                return false;
            }

            errorMessage = "";
            return true;
        }

        public bool TryGetBounds(out Bounds bounds)
        {
            if (_localBounds.HasValue)
            {
                bounds = _localBounds.Value;
                bounds.center += _context.transform.position;
                return true;
            }

            if (_renderers == null || _renderers.Count == 0)
            {
                bounds = default;
                return false;
            }

            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = -Vector3.one * float.MaxValue;

            for (int i = 0; i < _renderers.Count; i++)
            {
                Bounds rBounds = _renderers[i].bounds;

                Vector3 rMin = rBounds.min;
                Vector3 rMax = rBounds.max;

                min.x = Mathf.Min(min.x, rMin.x);
                min.y = Mathf.Min(min.y, rMin.y);
                min.z = Mathf.Min(min.z, rMin.z);

                max.x = Mathf.Max(max.x, rMax.x);
                max.y = Mathf.Max(max.y, rMax.y);
                max.z = Mathf.Max(max.z, rMax.z);
            }

            _localBounds = new Bounds(min + ((max - min) / 2) - _context.transform.position, max - min);

            bounds = _localBounds.Value;
            bounds.center += _context.transform.position;

            return true;
        }

        public CullingTarget CreateCullingTarget()
        {
            LODGroupCullingTarget cullingTarget = _context.AddComponent<LODGroupCullingTarget>();

            CollectRenderers();

            _localBounds = null;

            TryGetBounds(out Bounds bounds);

            cullingTarget.Bounds = bounds;
            cullingTarget.SetRenderers(_renderers);
            cullingTarget.CullingMethod = _cullingMethod;
            cullingTarget.IsOccluder = _isOccluder;

            return cullingTarget;
        }

        public void PrepareForBaking()
        {
            if (!_isOccluder)
                return;

            LOD lod = _lodGroup.GetLODs()[0];

            for (int c = 0; c < lod.renderers.Length; c++)
            {
                Renderer renderer = lod.renderers[c];

                if (CheckRenderer(renderer))
                {
                    Collider collider = CreateCollider(renderer.GetComponent<MeshFilter>());

                    if (_colliders == null)
                        _colliders = new List<Collider>();

                    _colliders.Add(collider);
                }
            }
        }

        public void ClearAfterBaking()
        {
            if (_colliders == null || _colliders.Count == 0)
                return;

            for (int i = 0; i < _colliders.Count; i++)
                UnityEngine.Object.DestroyImmediate(_colliders[i].gameObject);

            _colliders.Clear();
        }


        private void CollectRenderers()
        {
            if (_renderers == null)
                _renderers = new List<Renderer>();
            else
                _renderers.Clear();

            LOD[] lods = _lodGroup.GetLODs();

            for (int i = 0; i < _lodGroup.lodCount; i++)
            {
                LOD lod = lods[i];

                for (int c = 0; c < lod.renderers.Length; c++)
                {
                    Renderer renderer = lod.renderers[c];

                    if (CheckRenderer(renderer))
                        _renderers.Add(renderer);
                }
            }
        }

        private bool CheckRenderer(Renderer renderer)
        {
            if (renderer == null)
                return false;

            MeshFilter filter = renderer.GetComponent<MeshFilter>();

            if (filter == null || filter.sharedMesh == null)
                return false;

            return true;
        }

        public Collider CreateCollider(MeshFilter filter)
        {
            Mesh mesh = filter.sharedMesh;

            GameObject colliderGo = new GameObject("SC_Collider");

            colliderGo.layer = StaticCullingPreferences.Layer;
            colliderGo.transform.parent = _context.transform;
            colliderGo.transform.localPosition = Vector3.zero;
            colliderGo.transform.localEulerAngles = Vector3.zero;
            colliderGo.transform.localScale = Vector3.one;

            MeshCollider collider = colliderGo.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            return collider;
        }
    }
}
