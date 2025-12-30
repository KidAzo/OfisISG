using System;
using System.Collections.Generic;
using UnityEngine;


namespace NGS.AdvancedCullingSystem.Static
{
    public abstract class CullingTarget : MonoBehaviour
    {
        [field: SerializeField]
        public Bounds Bounds { get; set; }

        private bool _isVisible;
        private bool _makedVisible;


        private void LateUpdate()
        {
            if (_isVisible)
            {
                if (!_makedVisible)
                {
                    MakeVisible();
                    _makedVisible = true;
                }

                _isVisible = false;

                return;
            }
            else
            {
                MakeInvisible();

                _makedVisible = false;
                enabled = false;
            }
        }

        public void SetVisible()
        {
            if (!_isVisible)
            {
                enabled = true;
                _isVisible = true;
            }
        }


        protected abstract void MakeVisible();

        protected abstract void MakeInvisible();
    }
}
