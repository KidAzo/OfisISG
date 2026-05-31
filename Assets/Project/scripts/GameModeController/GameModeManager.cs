using UnityEngine;
using System;

namespace Woi.GameMode{
    public class GameModeManager : MonoBehaviour
    {
        [SerializeField] private GameMode gameMode;
        [SerializeField] private GameModeData[] gameModeData;

        private void Start()
        {
            SelectGameMode(gameMode);
        }

        private void SelectGameMode(GameMode gameMode)
        {
            foreach (var data in gameModeData)
            {
                data.gameModeObject.SetActive(data.gameMode == gameMode);
            }
        }
    }

    public enum GameMode
    {
        FireTraining,
        WasteCollection,
    }

    [Serializable]
    public class GameModeData
    {
        public GameMode gameMode;
        public GameObject gameModeObject;
    }
}

