using System;
using System.Collections.Generic;

namespace Woi.Leaderboard 
{

    [Serializable]
    public class LeaderboardEntry
    {
        public string UserId;
        public string UserName;
        public int BestScore;
        public float BestDuration;
        public string BestDate;
        public int AttemptCount;
        public string LastPlayedAt;
    }

    [Serializable]
    public class LeaderboardData
    {
        public List<LeaderboardEntry> Entries = new();
    }
}