using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    public static class DC_ControllerAssignHelper
    {
        public static void AssignSources(this DC_Controller controller, GameObject parent = null, CullingMethod cullingMethod = CullingMethod.KeepShadows)
        {
            LODGroup[] groups = FindLODGroups(parent);
            MeshRenderer[] renderers = FindMeshRenderers(parent);

            ProcessLODGroups(controller, groups, cullingMethod, out HashSet<Renderer> lodRenderers);
            ProcessMeshRenderers(controller, renderers, lodRenderers, cullingMethod);
        }

        public static void AssignSourcesFast(this DC_Controller controller, GameObject parent = null, CullingMethod cullingMethod = CullingMethod.KeepShadows)
        {
            LODGroup[] groups = FindLODGroups(parent);
            MeshRenderer[] renderers = FindMeshRenderers(parent);

            ProcessLODGroupsFast(controller, groups, cullingMethod);
            ProcessMeshRenderersFast(controller, renderers, cullingMethod);
        }


        private static LODGroup[] FindLODGroups(GameObject parent)
        {
            if (parent == null)
                return UnityAPI.FindObjectsOfType<LODGroup>();

            else
                return parent.GetComponentsInChildren<LODGroup>();
        }

        private static MeshRenderer[] FindMeshRenderers(GameObject parent)
        {
            if (parent == null)
                return UnityAPI.FindObjectsOfType<MeshRenderer>();

            else
                return parent.GetComponentsInChildren<MeshRenderer>();
        }


        private static void ProcessLODGroups(DC_Controller controller, LODGroup[] groups, CullingMethod cullingMethod, out HashSet<Renderer> lodRenderers)
        {
            lodRenderers = new HashSet<Renderer>();

            foreach (var group in groups)
            {
                LOD[] lods = group.GetLODs();

                foreach (var lod in lods)
                {
                    foreach (var renderer in lod.renderers)
                        lodRenderers.Add(renderer);
                }

                if (!CheckLODGroup(group))
                    continue;

                controller.AddObjectForCulling(group, cullingMethod);
            }
        }

        private static void ProcessLODGroupsFast(DC_Controller controller, LODGroup[] groups, CullingMethod cullingMethod)
        {
            foreach (var group in groups)
            {
                if (!CheckLODGroup(group))
                    continue;

                controller.AddObjectForCulling(group, cullingMethod);
            }
        }

        private static bool CheckLODGroup(LODGroup group)
        {
            if (!group.gameObject.activeInHierarchy)
                return false;

            if (group.GetComponent<DC_IgnoreByAssign>() != null)
                return false;

            if (group.GetComponent<DC_SourceSettings>() != null)
                return false;

            if (group.GetComponent<DC_Occluder>() != null)
                return false;

            return true;
        }


        private static void ProcessMeshRenderers(DC_Controller controller, MeshRenderer[] renderers, HashSet<Renderer> lodRenderers, CullingMethod cullingMethod)
        {
            foreach (var renderer in renderers)
            {
                if (!CheckMeshRenderer(renderer))
                    continue;

                if (lodRenderers.Contains(renderer))
                    continue;

                controller.AddObjectForCulling(renderer, cullingMethod);
            }
        }

        private static void ProcessMeshRenderersFast(DC_Controller controller, MeshRenderer[] renderers, CullingMethod cullingMethod)
        {
            foreach (var renderer in renderers)
            {
                if (!CheckMeshRenderer(renderer))
                    continue;

                if (renderer.GetComponentInParent<LODGroup>() != null)
                    continue;

                controller.AddObjectForCulling(renderer, cullingMethod);
            }
        }

        private static bool CheckMeshRenderer(MeshRenderer renderer)
        {
            if (!renderer.gameObject.activeInHierarchy)
                return false;

            if (renderer.GetComponent<DC_IgnoreByAssign>() != null)
                return false;

            if (renderer.GetComponent<DC_SourceSettings>() != null)
                return false;

            if (renderer.GetComponent<DC_Occluder>() != null)
                return false;

            return true;
        }
    }
}
