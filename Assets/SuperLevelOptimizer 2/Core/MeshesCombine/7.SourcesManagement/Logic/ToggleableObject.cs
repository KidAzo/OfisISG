using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    public class ToggleableObject : MonoBehaviour
    {
        public void SetEnable(bool enable)
        {
            LODGroup lodGroup = GetComponent<LODGroup>();

            if (lodGroup != null)
            {
                SetLODGroupEnable(lodGroup, enable);
            }
            else
            {
                Renderer renderer = GetComponent<Renderer>();

                if (renderer == null)
                {
                    Debug.Log("ToggleableObject::no LODs or Renderers found");
                    return;
                }

                SetRendererEnable(renderer, enable);
            }
        }

        private void SetRendererEnable(Renderer renderer, bool enable)
        {
            renderer.enabled = enable;
        }

        private void SetLODGroupEnable(LODGroup group, bool enable)
        {
            foreach (var lod in group.GetLODs())
            {
                foreach (var renderer in lod.renderers)
                {
                    if (renderer == null)
                        continue;

                    renderer.enabled = enable;
                }
            }
        }
    }
}