using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    public class DC_MultiSource : DC_Source
    {
        private List<ICullingTarget> _cullingTargets;
        private bool _visible;


        private void Awake()
        {
            _cullingTargets = new List<ICullingTarget>();
        }


        public override void SetCullingTarget(ICullingTarget target)
        {
            if (_cullingTargets == null)
                _cullingTargets = new List<ICullingTarget>();

            _cullingTargets.Add(target);

            if (_visible)
                target.MakeVisible();
            else
                target.MakeInvisible();
        }

        public override void RemoveCullingTarget(ICullingTarget target)
        {
            _cullingTargets.Remove(target);
        }


        protected override void OnHitInternal()
        {
            if (_visible)
                return;

            int i = 0;
            while (i < _cullingTargets.Count)
            {
                try
                {
                    _cullingTargets[i].MakeVisible();
                    i++;
                }
                catch(MissingReferenceException)
                {
                    RemoveCullingTarget(_cullingTargets[i]);
                }
            }

            _visible = true;
        }

        protected override void OnTimeout()
        {
            int i = 0;
            while (i < _cullingTargets.Count)
            {
                try
                {
                    _cullingTargets[i].MakeInvisible();
                    i++;
                }
                catch (MissingReferenceException)
                {
                    RemoveCullingTarget(_cullingTargets[i]);
                }
            }

            _visible = false;
        }
    }
}
