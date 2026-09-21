using WoiUtils.AudioSystem;

namespace Woi.Events
{
    public interface IEvent
    {
    }

    public struct OnSceneGroupLoaded : IEvent
    {
    }

    public struct OnHazardFixed : IEvent
    {
        public SoundDefinition soundDefinition;
        public string hazardTitle;
        public string description;
        public int score;
        public int hazardID;

        public OnHazardFixed(string hazardTitle, string description, int score, SoundDefinition soundDefinition, int hazardID)
        {
            this.hazardTitle = hazardTitle;
            this.score = score;
            this.description = description;
            this.soundDefinition = soundDefinition;
            this.hazardID = hazardID;
        }
    }

    public struct OnHazardResult : IEvent
    {
        public bool state;

        public OnHazardResult(bool state)
        {
            this.state = state;
        }
    }

    public struct OnHazardModeFinished : IEvent
    {
    }

    public struct HazardHuntLogged : IEvent
    {
        public string playerName;
        public int playerID;
        public int language;

        public HazardHuntLogged(string playerName, int playerID, int language)
        {
            this.playerName = playerName;
            this.playerID = playerID;
            this.language = language;
        }
    }

    public struct OnLanguageChanged : IEvent
    {
        public int language;

        public OnLanguageChanged(int language)
        {
            this.language = language;
        }
    }

    public struct OnLeaderboardUpdated : IEvent
    {
    }
}
