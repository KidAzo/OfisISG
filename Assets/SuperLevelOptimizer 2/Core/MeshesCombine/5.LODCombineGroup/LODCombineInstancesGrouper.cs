using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    public static class LODCombineInstancesGrouper
    {
        public static void CreateCombineGroups(IList<LODCombineInstance> instances, List<LODCombineGroup> groups)
        {
            foreach (var instance in instances)
            {
                bool groupFound = false;

                for (int i = 0; i < groups.Count; i++)
                {
                    if (groups[i].TryAddInstance(instance))
                    {
                        groupFound = true;
                        break;
                    }
                }

                if (!groupFound)
                {
                    LODCombineGroup group = new LODCombineGroup();

                    group.TryAddInstance(instance);

                    groups.Add(group);
                }
            }
        }
    }
}
