using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.AdvancedCullingSystem.Static
{
    public class LODGroupCullingTarget : CullingTarget
    {
        [field: SerializeField]
        public CullingMethod CullingMethod { get; set; }

        [field: SerializeField, HideInInspector]
        public bool IsOccluder { get; set; }

        [SerializeField, HideInInspector]
        private Renderer[] _renderers;

        private Action _makeVisibleAction;
        private Action _makeInvisibleAction;


        private void Awake()
        {
            if (CullingMethod == CullingMethod.FullDisable)
            {
                _makeVisibleAction = MakeRenderersVisible;
                _makeInvisibleAction = MakeRenderersInvisible;
            }
            else
            {
                _makeVisibleAction = MakeRenderersVisibleKeepShadows;
                _makeInvisibleAction = MakeRenderersInvisibleKeepShadows;
            }
        }

        public void SetRenderers(IEnumerable<Renderer> renderers)
        {
            _renderers = renderers.ToArray();
        }

        protected override void MakeVisible()
        {
            _makeVisibleAction();
        }

        protected override void MakeInvisible()
        {
            _makeInvisibleAction();
        }


        private void MakeRenderersVisible()
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].enabled = true;
        }

        private void MakeRenderersInvisible()
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].enabled = false;
        }

        private void MakeRenderersVisibleKeepShadows()
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].shadowCastingMode = ShadowCastingMode.On;
        }

        private void MakeRenderersInvisibleKeepShadows()
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }
}
