using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    public class LightStaticCullingSourceStrategy : IStaticCullingSourceStrategy
    {
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

        [SerializeField]
        private GameObject _context;

        [SerializeField]
        private Light _light;

        [SerializeField]
        private Bounds _localBounds;

        public LightStaticCullingSourceStrategy(GameObject context)
        {
            _context = context;

            _light = _context.GetComponent<Light>();

            LightCullingTarget target = _context.GetComponent<LightCullingTarget>();

            if (target != null)
            {
                _localBounds = new Bounds(
                    target.Bounds.center - target.transform.position,
                    target.Bounds.size);
            }
            else if (_light != null)
            {
                _localBounds = new Bounds
                {
                    center = Vector3.zero,
                    size = Vector3.one
                };

                if (_light.type == LightType.Point)
                    _localBounds.size = _light.range * Vector3.one;
            }
        }

        public bool Validate(out string errorMessage)
        {
            _light = _context.GetComponent<Light>();

            if (_light == null)
            {
                errorMessage = "Light component not found";
                return false;
            }

            errorMessage = "";
            return true;
        }

        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = _localBounds;

            bounds.center += _context.transform.position;

            return true;
        }

        public CullingTarget CreateCullingTarget()
        {
            LightCullingTarget target = _context.gameObject.AddComponent<LightCullingTarget>();

            TryGetBounds(out Bounds bounds);

            target.Bounds = bounds;

            return target;
        }

        public void PrepareForBaking()
        {

        }

        public void ClearAfterBaking()
        {

        }
    }
}
