using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    public class LightCullingTarget : CullingTarget
    {
        private Light _light;

        private void Awake()
        {
            _light = GetComponent<Light>();
        }

        protected override void MakeVisible()
        {
            _light.enabled = true;
        }

        protected override void MakeInvisible()
        {
            _light.enabled = false;
        }
    }
}
