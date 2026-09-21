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

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        EventBus.Register<HazardHuntLogged>(GetGameSettings);
    }

    void OnDisable()
    {
        EventBus.Deregister<HazardHuntLogged>(GetGameSettings);
    }

    public void SetLanguage(Language language)
    {
        gameSettings.Language = language;
        LanguageManager.SetLanguage(language);
        EventBus.Raise(new OnLanguageChanged((int)language));
    }

    public void GetGameSettings(HazardHuntLogged evt)
    {
        gameSettings = new GameSettings
        {
            PlayerName = evt.playerName,
            PlayerID = evt.playerID,
            Language = (Language)evt.language
        };
        SetLanguage(gameSettings.Language);
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
