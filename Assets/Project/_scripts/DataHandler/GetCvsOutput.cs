using System.Runtime.InteropServices;
using Reflex.Attributes;
using Sirenix.OdinInspector;
using UnityEngine;
using Woi.HazardSystem;
using Obvious.Soap;
using System;
using Woi.Porting;

namespace Woi.DataHandler
{
    public class GetCvsOutput : MonoBehaviour
    {
        [Inject] IHazardManagerService hazardManagerService;
        [SerializeField] string playerName;
        [SerializeField] int playerID;
        [SerializeField] SceneTimer sceneTimer;
        [Inject] IGameManager gameManager;
        [Inject] IPortingService portingService;

        void Start()
        {
            var gameSettings = gameManager.GetGameSettings();
            playerName = gameSettings.PlayerName;
            playerID = gameSettings.PlayerID;
        }

        [Button]
        public void ExportHazardData()
        {
            if(portingService.CurrentMode == AppMode.PC)
            {
                HazardCsvExporter.Append(
                    playerName,
                    playerID,
                    sceneTimer.GetElapsedTime(),
                    hazardManagerService.HazardCheckResult
                );
                return; 
            }    

            TimeSpan duration = DateTime.Now - SessionManager.Instance.CurrentSession.StartTime;

            if (portingService.CurrentMode == AppMode.XR)
            {

                HazardCsvExporter.AppendSession(
                    SessionManager.Instance.CurrentSession,
                    duration,
                    hazardManagerService.HazardCheckResult
                );

                SessionManager.Instance.ClearSession();

                return;
            }

           HazardCsvExporter.Append(playerName, playerID, duration, hazardManagerService.HazardCheckResult);
        }
    }
}

