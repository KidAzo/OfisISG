using System;

namespace Woi.DataHandler
{
    [Serializable]
    public sealed class PlayerSession
    {
        public string PlayerName;
        public int PlayerID;
        public DateTime StartTime;
        public bool IsActive;

        public override string ToString()
            => $"PlayerSession(Name={PlayerName}, ID={PlayerID}, Active={IsActive})";
    }
}
