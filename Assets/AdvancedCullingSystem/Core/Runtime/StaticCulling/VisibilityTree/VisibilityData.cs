using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    public interface IVisibilityData
    {
        void SetVisible(CullingTarget[] allTargets);
    }

    public class VisibilityData : IVisibilityData
    {
        [SerializeField]
        private int[] _indexes;

        public VisibilityData(ICollection<int> indexes)
        {
            _indexes = indexes.ToArray();
        }

        public void SetVisible(CullingTarget[] allTargets)
        {
            for (int i = 0; i < _indexes.Length; i++)
                allTargets[_indexes[i]].SetVisible();
        }
    }

    public class CompactVisibilityData : IVisibilityData
    {
        [SerializeField]
        private ushort[] _indexes;

        public CompactVisibilityData(ICollection<int> indexes)
        {
            _indexes = indexes.Select(i => (ushort)i).ToArray();
        }

        public void SetVisible(CullingTarget[] allTargets)
        {
            for (int i = 0; i < _indexes.Length; i++)
                allTargets[_indexes[i]].SetVisible();
        }
    }
}
