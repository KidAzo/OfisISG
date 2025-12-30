using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    public class LODCombineGroup
    {
        public IList<LODCombineInstance> Instances
        {
            get
            {
                return _instances;
            }
        }

        private List<LODCombineInstance> _instances;


        public LODCombineGroup()
        {
            _instances = new List<LODCombineInstance>();
        }

        public bool CanAddInstance(LODCombineInstance instance)
        {
            if (_instances.Count == 0)
                return true;

            if (!LODCombineInstancesComparer.HasEqualParameters(_instances[0], instance))
                return false;

            return true;
        }

        public bool TryAddInstance(LODCombineInstance instance)
        {
            if (!CanAddInstance(instance))
                return false;

            _instances.Add(instance);

            return true;
        }
    }
}
