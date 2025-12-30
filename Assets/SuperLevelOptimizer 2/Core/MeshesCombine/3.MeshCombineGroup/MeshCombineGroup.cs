using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NGS.SLO.Shared;

namespace NGS.SLO.MeshesCombine
{
    public class MeshCombineGroup
    {
        public IList<MeshCombineInstance> Instances
        {
            get
            {
                return _instances;
            }
        }
        public MeshCombineOptions CombineOptions
        {
            get
            {
                return _options;
            }
        }
        public int VertexCount
        {
            get
            {
                return _vertexCount;
            }
        }

        private List<MeshCombineInstance> _instances;
        private MeshCombineOptions _options;
        private int _vertexCount;


        public MeshCombineGroup(MeshCombineOptions options)
        {
            _instances = new List<MeshCombineInstance>();
            _options = options;
        }

        public bool CanAddInstance(MeshCombineInstance instance)
        {
            if (_instances.Count == 0)
                return true;

            if (_instances[0].renderer.gameObject.layer != instance.renderer.gameObject.layer)
                return false;

            Material materialA = _instances[0].renderer.GetSharedMaterial(_instances[0].submeshIndex);
            Material materialB = instance.renderer.GetSharedMaterial(instance.submeshIndex);

            if (materialA != materialB)
                return false;

            if (!MeshCombineInstancesComparer.HasEqualParameters(_instances[0], instance))
                return false;

            if (_options.limit65kVertices)
            {
                int newVertexCount = _vertexCount + instance.vertexCount;

                if (newVertexCount >= ushort.MaxValue)
                    return false;
            }

            return true;
        }

        public bool TryAddInstance(MeshCombineInstance instance)
        {
            if (!CanAddInstance(instance))
                return false;

            _instances.Add(instance);
            _vertexCount += instance.vertexCount;

            return true;
        }
    }
}
