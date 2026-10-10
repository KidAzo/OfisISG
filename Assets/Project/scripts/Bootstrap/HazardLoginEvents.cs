namespace Woi.Events
{
    public struct HazardHuntLogged
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

    public struct OnLanguageChanged
    {
        public int language;

        public OnLanguageChanged(int language)
        {
            this.language = language;
        }
    }
}
