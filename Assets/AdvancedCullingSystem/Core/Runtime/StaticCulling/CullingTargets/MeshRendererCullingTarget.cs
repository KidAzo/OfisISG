using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.AdvancedCullingSystem.Static
{
    public enum CullingMethod { KeepShadows, FullDisable }

    public class MeshRendererCullingTarget : CullingTarget
    {
        [field: SerializeField]
        public CullingMethod CullingMethod { get; set; }

        [field: SerializeField, HideInInspector]
        public bool IsOccluder { get; set; }

        private MeshRenderer _renderer;

        private Action _makeVisAction;
        private Action _makeInvisAction;


        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();

            if (CullingMethod == CullingMethod.FullDisable)
            {
                _makeVisAction = EnableRenderer;
                _makeInvisAction = DisableRenderer;
            }
            else
            {
                _makeVisAction = EnableRendererKeepShadows;
                _makeInvisAction = DisableRendererKeepShadows;
            }
        }


        protected override void MakeInvisible()
        {
            _makeInvisAction();
        }

        protected override void MakeVisible()
        {
            _makeVisAction();
        }


        private void EnableRenderer()
        {
            _renderer.enabled = true;
        }

        private void DisableRenderer()
        {
            _renderer.enabled = false;
        }

        private void EnableRendererKeepShadows()
        {
            _renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        private void DisableRendererKeepShadows()
        {
            _renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }
}
