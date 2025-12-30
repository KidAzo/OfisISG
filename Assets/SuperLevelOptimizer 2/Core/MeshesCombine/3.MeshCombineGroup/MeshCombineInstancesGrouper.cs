using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    public static class MeshCombineInstancesGrouper
    {
        public static void CreateCombineGroups(IList<MeshCombineInstance> instances, MeshCombineOptions options, List<MeshCombineGroup> groups)
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
                    MeshCombineGroup group = new MeshCombineGroup(options);

                    group.TryAddInstance(instance);

                    groups.Add(group);
                }
            }
        }
    }
}
