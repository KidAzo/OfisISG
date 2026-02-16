using UnityEngine;
using Obvious.Soap;

namespace Woi.Porting
{
    [CreateAssetMenu(fileName ="ScriptableEnumPortingVariable", menuName = "Soap/ScriptableEnums/PortingVariable")]
    public class ScriptableEnumPortingVariable : ScriptableEnumBase
    {
        [SerializeField] 
        private AppMode _value;

        public AppMode Value
        {
            get => _value;
        }
    }
}
