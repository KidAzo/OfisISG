using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Woi.Events.Data
{
    /// <summary>
    /// Kalıcı yerel skor tablosu (PlayerPrefs + JSON). Aynı oyuncu anahtarı tekrar oynayınca eski skorun üzerine yazar;
    /// en yüksek 10 skor tutulur.
    /// </summary>
    public static class TrainingLeaderboardStore
    {
        public const int MaxEntries = 10;
        public const string EmptySlotDisplay = "-----";

        const string PrefsKey = "woi.training.leaderboard.v1";

        [Serializable]
        sealed class PersistedList
        {
            public List<PersistedEntry> items = new List<PersistedEntry>();
        }

        [Serializable]
        sealed class PersistedEntry
        {
            public string playerKey;
            public string displayName;
            public int scorePercent;
        }

        /// <summary>
        /// Oyuncu anahtarı: dolu <paramref name="userId"/> trim + küçük harf; yoksa <paramref name="displayName"/> küçük harf trim.
        /// </summary>
        public static string BuildPlayerKey(string displayName, string userId)
        {
            if (!string.IsNullOrWhiteSpace(userId))
                return userId.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName.Trim().ToLowerInvariant();

            return string.Empty;
        }

        /// <summary>
        /// Oturum bittiğinde çağırın. Geçerli anahtar yoksa no-op.
        /// </summary>
        public static void TryRecordScore(string displayName, string userId, int scorePercent)
        {
            string key = BuildPlayerKey(displayName, userId);
            if (string.IsNullOrEmpty(key))
                return;

            scorePercent = Mathf.Clamp(scorePercent, 0, 100);
            string name = string.IsNullOrWhiteSpace(displayName) ? (string.IsNullOrWhiteSpace(userId) ? key : userId.Trim()) : displayName.Trim();

            PersistedList list = LoadList();
            list.items.RemoveAll(e => e != null && string.Equals(e.playerKey, key, StringComparison.Ordinal));
            list.items.Add(new PersistedEntry { playerKey = key, displayName = name, scorePercent = scorePercent });
            list.items = list.items
                .Where(e => e != null && !string.IsNullOrEmpty(e.playerKey))
                .OrderByDescending(e => e.scorePercent)
                .ThenBy(e => e.displayName, StringComparer.OrdinalIgnoreCase)
                .Take(MaxEntries)
                .ToList();

            SaveList(list);
        }

        /// <summary>
        /// Tam <see cref="MaxEntries"/> satır; boş slotlarda <see cref="EmptySlotDisplay"/>.
        /// </summary>
        public static IReadOnlyList<string> GetDisplayLines(int maxLines = MaxEntries)
        {
            PersistedList list = LoadList();
            var lines = new List<string>(maxLines);
            int i = 0;
            for (; i < list.items.Count && i < maxLines; i++)
            {
                PersistedEntry e = list.items[i];
                if (e == null)
                {
                    lines.Add(EmptySlotDisplay);
                    continue;
                }

                string left = string.IsNullOrEmpty(e.displayName) ? e.playerKey : e.displayName;
                if (left.Length > 22)
                    left = left.Substring(0, 20) + "...";

                lines.Add($"{left} — {e.scorePercent}");
            }

            for (; i < maxLines; i++)
                lines.Add(EmptySlotDisplay);

            return lines;
        }

        static PersistedList LoadList()
        {
            try
            {
                string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
                if (string.IsNullOrEmpty(json))
                    return new PersistedList();

                PersistedList parsed = JsonUtility.FromJson<PersistedList>(json);
                if (parsed?.items == null)
                    return new PersistedList();

                return parsed;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TrainingLeaderboardStore] Resetting leaderboard prefs: {ex.Message}");
                return new PersistedList();
            }
        }

        static void SaveList(PersistedList list)
        {
            string json = JsonUtility.ToJson(list ?? new PersistedList());
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
        }
    }
}
