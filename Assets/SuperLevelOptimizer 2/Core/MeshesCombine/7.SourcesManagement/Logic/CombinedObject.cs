using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    [DisallowMultipleComponent]
    public class CombinedObject : ToggleableObject
    {
        public void Destroy()
        {
            if (gameObject)
                DestroyImmediate(gameObject);
        }
    }
}
