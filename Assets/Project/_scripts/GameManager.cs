using UnityEngine;
using Woi.Events;
using Woi.Localization;

public class GameManager : MonoBehaviour, IGameManager  
{
    public struct GameSettings
    {
        public string PlayerName;
        public int PlayerID;
        public Language Language;
    }

    GameSettings gameSettings;

    void OnEnable()
    {
        EventBus.Subscribe<OnLogged>(GetGameSettings);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<OnLogged>(GetGameSettings);
    }


    public void GetGameSettings(OnLogged evt)
    {
       gameSettings = new GameSettings
       {
           PlayerName = evt.playerName,
           PlayerID = evt.playerID,
           Language = (Language)evt.language
       };
    }

    public GameManager.GameSettings GetGameSettings()
    {
        return gameSettings;
    }
}

public interface IGameManager
{
    GameManager.GameSettings GetGameSettings();
}
