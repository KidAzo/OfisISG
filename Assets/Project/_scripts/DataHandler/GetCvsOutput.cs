using System.Runtime.InteropServices;
using Reflex.Attributes;
using Sirenix.OdinInspector;
using UnityEngine;
using Woi.HazardSystem;


namespace Woi.DataHandler
{   
    public class GetCvsOutput : MonoBehaviour
    {
        [Inject] IHazardManagerService hazardManagerService;
        [SerializeField] string playerName;

        [Button]
        public void ExportHazardData()
        {
            HazardCsvExporter.Append(playerName, hazardManagerService.HazardCheckResult);
        }
    }
}

