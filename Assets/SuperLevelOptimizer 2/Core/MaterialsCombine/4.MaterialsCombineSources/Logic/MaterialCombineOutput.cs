using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NGS.SLO.Shared;

namespace NGS.SLO.MaterialsCombine
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public class MaterialCombineOutput : MonoBehaviour
    {
        public IReadOnlyList<MaterialCombineReport> Reports
        {
            get
            {
                return _combineReports;
            }
        }

        [SerializeField]
        [HideInInspector]
        private Renderer _renderer;

        [SerializeField]
        private MaterialCombineReport[] _combineReports;


        public void Reset()
        {
            _renderer = GetComponent<Renderer>();

            _combineReports = new MaterialCombineReport[_renderer.sharedMaterials.Length];

            for (int i = 0; i < _combineReports.Length; i++)
            {
                _combineReports[i] = new MaterialCombineReport()
                {
                    combineStatus = MaterialCombineStatus.NotCombined,
                    message = ""
                };
            }
        }

        public void ApplyCombineResult(MaterialCombineInstance instance, MaterialCombineResult combineResult, int instanceIndex)
        {
            int materialIndex = instance.MaterialIndex;

            if (materialIndex < 0 || materialIndex >= _renderer.sharedMaterials.Length)
            {
                Debug.Log("MaterialCombinedOutput::trying to apply combine result with out-of-bounds materialIndex");
                return;
            }

            CreateCombineReport(combineResult, materialIndex);

            if (!combineResult.success)
                return;

            try
            {
                AdjustUV(instance, combineResult.rects[instanceIndex]);

                _renderer.SetSharedMaterial(combineResult.combinedMaterial, materialIndex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"MaterialCombinedOutput::{gameObject.name}: Error while apply combined material : " + ex.Message);
                
                _combineReports[materialIndex] = new MaterialCombineReport()
                {
                    material = _renderer.GetSharedMaterial(materialIndex),
                    combineStatus = MaterialCombineStatus.CombineError,
                    message = "MaterialCombinedOutput::Error while apply combined material : " + ex.Message
                };
            }
        }


        private void CreateCombineReport(MaterialCombineResult combineResult, int materialIndex)
        {
            MaterialCombineReport report = default;

            if (!combineResult.success)
            {
                report.material = _renderer.GetSharedMaterial(materialIndex);
                report.combineStatus = MaterialCombineStatus.CombineError;
                report.message = combineResult.errorMessage;
            }
            else
            {
                report.material = combineResult.combinedMaterial;
                report.combineStatus = MaterialCombineStatus.Combined;
            }
            
            _combineReports[materialIndex] = report;
        }

        private void AdjustUV(MaterialCombineInstance instance, Rect rect)
        {
            Mesh mesh;

            if (!_renderer.TryGetSharedMesh(out mesh))
                throw new Exception("MaterialCombinedOutput::mesh not found");

            mesh = MeshUtil.GetUVAdjustedMesh(mesh, instance, rect);

            if (!_renderer.TrySetSharedMesh(mesh))
                throw new Exception("MaterialCombinedOutput::unable to set adjusted mesh");    
        }
    }
}
