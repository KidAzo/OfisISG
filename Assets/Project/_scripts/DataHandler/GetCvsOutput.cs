using System.Runtime.InteropServices;
using Reflex.Attributes;
using Sirenix.OdinInspector;
using UnityEngine;
using Woi.HazardSystem;
using Obvious.Soap;
using System;

namespace Woi.DataHandler
{
    public class GetCvsOutput : MonoBehaviour
    {
        [Inject] IHazardManagerService hazardManagerService;
        [SerializeField] string playerName;
        [SerializeField] int playerID;
        [SerializeField] SceneTimer sceneTimer;
        [Inject] IGameManager gameManager;

        void Start()
        {
            var gameSettings = gameManager.GetGameSettings();
            playerName = gameSettings.PlayerName;
            playerID = gameSettings.PlayerID;
        }
        
        [Button]
        public void ExportHazardData()
        {
            TimeSpan duration = DateTime.Now - SessionManager.Instance.CurrentSession.StartTime;
            
            HazardCsvExporter.AppendSession(
                SessionManager.Instance.CurrentSession, 
                duration,                                
                hazardManagerService.HazardCheckResult   
            );
            
            SessionManager.Instance.ClearSession();      
      }
    }
}

