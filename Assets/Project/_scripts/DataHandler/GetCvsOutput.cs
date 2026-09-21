using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Woi.HazardSystem;
using Obvious.Soap;

namespace Woi.DataHandler
{
    public class GetCvsOutput : MonoBehaviour
    {
        [SerializeField] string playerName;
        [SerializeField] int playerID;
        [SerializeField] SceneTimer sceneTimer;
        [SerializeField] ScriptableEventNoParam onSessionStarted;

        IHazardManagerService _hazardManagerService;
        IGameManager _gameManager;

        void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _hazardManagerService = FindFirstObjectByType<HazardManager>();
            if (sceneTimer == null)
                sceneTimer = FindFirstObjectByType<SceneTimer>();

            if (_gameManager != null)
            {
                var gameSettings = _gameManager.GetGameSettings();
                playerName = gameSettings.PlayerName;
                playerID = gameSettings.PlayerID;
            }
        }

        [Button]
        public void ExportHazardData()
        {
            if (_hazardManagerService == null)
                _hazardManagerService = FindFirstObjectByType<HazardManager>();
            if (sceneTimer == null)
                sceneTimer = FindFirstObjectByType<SceneTimer>();
            if (_gameManager == null)
                _gameManager = FindFirstObjectByType<GameManager>();

            if (_gameManager != null)
            {
                var gameSettings = _gameManager.GetGameSettings();
                playerName = gameSettings.PlayerName;
                playerID = gameSettings.PlayerID;
            }

            var result = _hazardManagerService != null ? _hazardManagerService.HazardCheckResult : null;
            if (result == null && _hazardManagerService != null)
                result = _hazardManagerService.BuildHazardCheckResult();

            if (result == null)
            {
                Debug.LogWarning("[GetCvsOutput] No HazardCheckResult available.");
                return;
            }

            var duration = sceneTimer != null ? sceneTimer.GetElapsedTime() : TimeSpan.Zero;
            HazardCsvExporter.Append(playerName, playerID, duration, result);
        }

        [Button]
        void RaiseOnSessesionStarted()
        {
            onSessionStarted?.Raise();
        }
    }
}
