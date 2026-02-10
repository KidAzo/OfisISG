using System.Runtime.InteropServices;
using Reflex.Attributes;
using Sirenix.OdinInspector;
using UnityEngine;
using Woi.HazardSystem;
using Obvious.Soap;

namespace Woi.DataHandler
{   
    public class GetCvsOutput : MonoBehaviour
    {
        [Inject] IHazardManagerService hazardManagerService;
        [SerializeField] string playerName;
        [SerializeField] int playerID;
        [SerializeField] SceneTimer sceneTimer;
  
        [Button]
        public void ExportHazardData()
        {
            HazardCsvExporter.Append(playerName, playerID, sceneTimer.GetElapsedTime(), hazardManagerService.HazardCheckResult);
        }
    }
}

