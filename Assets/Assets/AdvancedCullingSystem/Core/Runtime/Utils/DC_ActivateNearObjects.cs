using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NGS.AdvancedCullingSystem.Dynamic;

namespace NGS.AdvancedCullingSystem.Utils
{
    public class DC_ActivateNearObjects : MonoBehaviour
    {
        [SerializeField]
        private bool _drawGizmos = false;

        [Space]

        [Min(0.1f)]
        [SerializeField]
        private float _radius = 20f;

        [Min(1)]
        [SerializeField]
        private int _maxObjectsCount = 100;

        private IReadOnlyDictionary<Collider, IHitable> _hitablesDic;
        private int _layer;

        private Collider[] _hits;


        private void Start()
        {
            _hitablesDic = DC_Controller.GetHitables();
            _layer = LayerMask.GetMask(DC_Controller.GetCullingLayerName());
            _hits = new Collider[_maxObjectsCount];
        }

        private void LateUpdate()
        {
            int hitsCount = Physics.OverlapSphereNonAlloc(transform.position, _radius, _hits, _layer);

            for (int i = 0; i < hitsCount; i++)
            {
                Collider collider = _hits[i];

                if (_hitablesDic.TryGetValue(collider, out IHitable hitable))
                    hitable.OnHit();
            }
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmos)
                return;
            
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
