using System;
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

    public void SetLanguage(Language language)
    {
        gameSettings.Language = language;
        LanguageManager.SetLanguage(language);
        EventBus.Publish(new OnLanguageChanged((int)language));
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
    void SetLanguage(Language language);
}

