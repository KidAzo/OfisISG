using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.AdvancedCullingSystem.Static
{
    public class MeshRendererStaticCullingSourceStrategy : IStaticCullingSourceStrategy
    {
        [SerializeField]
        private GameObject _context;

        [SerializeField]
        private MeshRenderer _renderer;

        [SerializeField]
        private MeshFilter _filter;

        [SerializeField]
        private MeshCollider _collider;

        [SerializeField]
        private CullingMethod _cullingMethod;

        [SerializeField]
        private bool _isOccluder;


        public MeshRendererStaticCullingSourceStrategy(GameObject context)
        {
            _context = context;

            MeshRendererCullingTarget target = context.GetComponent<MeshRendererCullingTarget>();

            if (target != null)
                _isOccluder = target.IsOccluder;
            else
                _isOccluder = !AllMaterialsIsTransarent(context);
        }

        public bool Validate(out string errorMessage)
        {
            _renderer = _context.GetComponent<MeshRenderer>();

            if (_renderer == null)
            {
                errorMessage = "MeshRenderer not found";
                return false;
            }

            _filter = _context.GetComponent<MeshFilter>();

            if (_filter == null)
            {
                errorMessage = "MeshFilter not found";
                return false;
            }

            if (_filter.sharedMesh == null)
            {
                errorMessage = "Mesh not found";
                return false;
            }

            errorMessage = "";
            return true;
        }

        public bool TryGetBounds(out Bounds bounds)
        {
            if (_renderer == null)
            {
                if ((_renderer = _context.GetComponent<MeshRenderer>()) == null)
                {
                    bounds = default(Bounds);
                    return false;
                }
            }

            bounds = _renderer.bounds;
            return true;
        }

        public CullingTarget CreateCullingTarget()
        {
            var cullingTarget = _context.AddComponent<MeshRendererCullingTarget>();

            cullingTarget.Bounds = _renderer.bounds;
            cullingTarget.CullingMethod = _cullingMethod;
            cullingTarget.IsOccluder = _isOccluder;

            return cullingTarget;
        }

        public void PrepareForBaking()
        {
            if (!_isOccluder)
                return;

            Mesh mesh = _filter.sharedMesh;

            GameObject colliderGo = new GameObject("SC_Collider");

            colliderGo.layer = StaticCullingPreferences.Layer;
            colliderGo.transform.parent = _context.transform;
            colliderGo.transform.localPosition = Vector3.zero;
            colliderGo.transform.localEulerAngles = Vector3.zero;
            colliderGo.transform.localScale = Vector3.one;

            _collider = colliderGo.AddComponent<MeshCollider>();
            _collider.sharedMesh = mesh;
        }

        public void ClearAfterBaking()
        {
            if (_collider != null)
                UnityEngine.Object.DestroyImmediate(_collider.gameObject);
        }


        private bool AllMaterialsIsTransarent(GameObject context)
        {
            MeshRenderer renderer = context.GetComponent<MeshRenderer>();

            if (renderer == null)
                return false;

            Material[] materials = renderer.sharedMaterials;

            if (materials == null || materials.Length == 0)
                return false;

            bool allTransparent = true;

            foreach (var material in materials)
            {
                if (material == null)
                    continue;

                if (material.renderQueue != (int)RenderQueue.Transparent)
                    allTransparent = false;
            }

            return allTransparent;
        }
    }
}
