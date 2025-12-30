using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NGS.SLO.Shared;

namespace NGS.SLO.MeshesCombine
{
    public static class MeshCombineSourcesController 
    {
        public static void SetEnable(bool combinedEnable, bool sourcesEnable)
        {
            foreach (var combined in UnityAPI.FindObjectsOfType<CombinedObject>())
                combined.SetEnable(combinedEnable);

            foreach (var source in UnityAPI.FindObjectsOfType<MeshCombineSource>())
                source.SetEnable(sourcesEnable);
        }

        public static void DestroyCombinedMeshes()
        {
            foreach (var combined in UnityAPI.FindObjectsOfType<CombinedObject>())
                combined.Destroy();
        }

        public static void DestroySourceComponents()
        {
            foreach (var source in UnityAPI.FindObjectsOfType<MeshCombineSource>())
            {
                source.DestroySourceComponents();
                Object.DestroyImmediate(source);
            }
        }

        public static void AddSourcesWithErrorToIgnore()
        {
            foreach (var source in UnityAPI.FindObjectsOfType<MeshCombineSource>())
            {
                foreach (var report in source.Reports)
                {
                    if (report.status == MeshCombineStatus.CombineError)
                    {
                        source.AddToCombineIgnore();
                        break;
                    }
                }
            }
        }
    }
}
