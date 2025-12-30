using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    public static class StaticCullingPreferences
    {
        public static string LayerName 
        {
            get
            {
                return "ACSCulling";
            }
        }

        public static int Layer
        {
            get
            {
                return LayerMask.NameToLayer(LayerName);
            }
        }
    }
}
