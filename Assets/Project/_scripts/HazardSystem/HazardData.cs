using HazardSystem;
using UnityEngine;
using WoiUtils.AudioSystem;

namespace Woi.HazardSystem
{
    [CreateAssetMenu(fileName = "HazardData", menuName = "SO/HazardData", order = 1)]
    public class HazardData : ScriptableObject, ICheckable
    {
        public string TaskName => hazardName;
        public string hazardName;
        [TextArea] public string description;
        public int score;
        public HazardType type;
        public SoundDefinition soundDefinition;
    }
}
