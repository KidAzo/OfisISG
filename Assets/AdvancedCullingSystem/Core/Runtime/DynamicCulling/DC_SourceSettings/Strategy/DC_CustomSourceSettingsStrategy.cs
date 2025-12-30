using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    [Serializable]
    public class DC_CustomSourceSettingsStrategy : IDC_SourceSettingsStrategy
    {
        [field: SerializeField]
        public bool ReadyForCulling { get; private set; }
        public Bounds LocalBounds
        {
            get
            {
                return _localBounds;
            }
            set
            {
                _localBounds = value;
            }
        }

        public DC_CustomTargetEvent OnVisibleEvent
        {
            get
            {
                return _onVisible;
            }
        }
        public DC_CustomTargetEvent OnInvisibleEvent
        {
            get
            {
                return _onInvisible;
            }
        }

        [SerializeField]
        private DC_SourceSettings _context;

        [SerializeField]
        private Bounds _localBounds;

        [SerializeField]
        private bool _alignRotation;

        [SerializeField]
        private List<Renderer> _renderers;

        [SerializeField]
        private DC_CustomTargetEvent _onVisible;

        [SerializeField]
        private DC_CustomTargetEvent _onInvisible;

        [SerializeField]
        private BoxCollider _collider;


        public DC_CustomSourceSettingsStrategy(DC_SourceSettings context)
        {
            _context = context;
            _localBounds = new Bounds(Vector3.zero, Vector3.one);

            _renderers = new List<Renderer>();

            _onVisible = new DC_CustomTargetEvent();
            _onInvisible = new DC_CustomTargetEvent();
        }

        public bool CheckCompatibilityAndGetComponents(out string incompatibilityReason)
        {
            incompatibilityReason = "";
            return true;
        }

        public void PrepareForCulling()
        {
            if (ReadyForCulling)
                return;

            GameObject go = new GameObject("DC_Collider");

            go.transform.localScale = Vector3.one;

            go.transform.parent = _context.transform;
            go.layer = _context.CullingLayer;

            go.transform.localPosition = Vector3.zero;
            go.transform.eulerAngles = Vector3.zero;

            if (_alignRotation)
                go.transform.localEulerAngles = Vector3.zero;

            _collider = go.AddComponent<BoxCollider>();
            _collider.center = _localBounds.center;
            _collider.size = _localBounds.size;

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
            bounds.center = _context.transform.position + _localBounds.center;
            bounds.size = _localBounds.size;

            return true;
        }

        public ICullingTarget CreateCullingTarget()
        {
            Bounds bounds = new Bounds();

            TryGetBounds(ref bounds);

            RegisterRenderersInEvents();

            return new DC_CustomTarget(_context.gameObject, bounds, _onVisible, _onInvisible);
        }

        public IEnumerable<Collider> GetColliders()
        {
            if (_collider == null)
                yield break;

            yield return _collider;
        }


        public void AlignLocalBoundsByChildRenderers()
        {
            Renderer[] renderers = _context.GetComponentsInChildren<Renderer>();

            if (renderers == null || renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            bounds.center -= _context.transform.position;

            _localBounds = bounds;
        }

        public void AlignLocalBoundsByChildColliders()
        {
            Collider[] colliders = _context.GetComponentsInChildren<Collider>();

            if (colliders == null || colliders.Length == 0)
                return;

            Bounds bounds = colliders[0].bounds;

            for (int i = 1; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);

            bounds.center -= _context.transform.position;

            _localBounds = bounds;
        }


        public void AddChildRenderersToList()
        {
            foreach (var renderer in _context.GetComponentsInChildren<Renderer>())
            {
                if (!_renderers.Contains(renderer))
                    _renderers.Add(renderer);
            }
        }

        public void RemoveChildRenderersFromList()
        {
            foreach (var renderer in _context.GetComponentsInChildren<Renderer>())
                _renderers.Remove(renderer);
        }


        private void RegisterRenderersInEvents()
        {
            if (_renderers == null)
                return;

            foreach(var renderer in _renderers)
            {
                if (renderer == null)
                    continue;

                _onVisible.AddListener((t) => renderer.enabled = true);
                _onInvisible.AddListener((t) => renderer.enabled = false);
            }
        }
    }
}
