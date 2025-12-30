using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    public class MeshCombineSource : ToggleableObject
    {
        public IReadOnlyList<MeshCombineReport> Reports
        {
            get
            {
                return _reports;
            }
        }

        [SerializeField]
        private List<MeshCombineReport> _reports;

        public void CreateCombineReport(MeshCombineResult combineResult, int instanceIndex)
        {
            if (_reports == null)
                _reports = new List<MeshCombineReport>();

            int submeshIndex = combineResult.instances[instanceIndex].submeshIndex;

            _reports.RemoveAll(r => r.submeshIndex == submeshIndex);

            _reports.Add(new MeshCombineReport()
            {
                submeshIndex = submeshIndex,
                status = combineResult.success ? MeshCombineStatus.Combined : MeshCombineStatus.CombineError,
                combineError = combineResult.errorMessage
            });

            _reports = _reports
                .OrderBy(r => r.submeshIndex)
                .ToList();
        }

        public void DestroySourceComponents()
        {
            Renderer renderer = GetComponent<Renderer>();

            if (renderer == null)
                return;

            MeshFilter filter = GetComponent<MeshFilter>();

            if (filter != null)
                DestroyImmediate(filter);

            if (renderer != null)
                DestroyImmediate(renderer);
        }

        public void AddToCombineIgnore()
        {
            MeshCombineIgnore ignore = GetComponent<MeshCombineIgnore>();

            if (ignore != null)
                return;

            gameObject.AddComponent<MeshCombineIgnore>();
        }
    }
}
