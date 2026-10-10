using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Woi.Leaderboard
{
    public static class LeaderboardService
    {
        private static readonly string FilePath = Path.Combine(Application.persistentDataPath, "leaderboard.json");

        public static LeaderboardData Load()
        {
            if (!File.Exists(FilePath))
            {
                var newData = new LeaderboardData();
                Save(newData);
                return newData;
            }

            string json = File.ReadAllText(FilePath);

            if (string.IsNullOrWhiteSpace(json))
                return new LeaderboardData();

            return JsonUtility.FromJson<LeaderboardData>(json) ?? new LeaderboardData();
        }

        public static void Save(LeaderboardData data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FilePath, json);
        }

        public static void SubmitScore(string userId, string userName, int score, float duration)
        {
            LeaderboardData data = Load();

            var existing = data.Entries.FirstOrDefault(x => x.UserId == userId);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (existing == null)
            {
                data.Entries.Add(new LeaderboardEntry
                {
                    UserId = userId,
                    UserName = userName,
                    BestScore = score,
                    BestDuration = duration,
                    BestDate = now,
                    AttemptCount = 1,
                    LastPlayedAt = now
                });
            }
            else
            {
                existing.AttemptCount++;
                existing.LastPlayedAt = now;

                bool betterScore = score > existing.BestScore;
                bool sameScoreButBetterTime = score == existing.BestScore && duration < existing.BestDuration;

                if (betterScore || sameScoreButBetterTime)
                {
                    existing.UserName = userName;
                    existing.BestScore = score;
                    existing.BestDuration = duration;
                    existing.BestDate = now;
                }
            }

            Save(data);
        }

        public static LeaderboardEntry[] GetTop10()
        {
            LeaderboardData data = Load();

            return data.Entries
                .OrderByDescending(x => x.BestScore)
                .ThenBy(x => x.BestDuration)
                .Take(10)
                .ToArray();
        }

        public static string GetFilePath()
        {
            return FilePath;
        }

        public static void ResetLeaderboard()
        {
            Save(new LeaderboardData());
        }
    }
}