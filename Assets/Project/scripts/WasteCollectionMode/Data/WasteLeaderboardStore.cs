using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Local waste-collection leaderboard (PlayerPrefs + JSON). Same player key overwrites prior score;
    /// top entries sorted by success rate.
    /// </summary>
    public static class WasteLeaderboardStore
    {
        public const int MaxEntries = 10;
        public const string EmptySlotDisplay = "-----";

        private const string PrefsKey = "woi.waste.leaderboard.v1";

        [Serializable]
        private sealed class PersistedList
        {
            public List<PersistedEntry> items = new();
        }

        [Serializable]
        private sealed class PersistedEntry
        {
            public string playerKey;
            public string displayName;
            public int successPercent;
        }

        public static string BuildPlayerKey(string displayName, string userId)
        {
            if (!string.IsNullOrWhiteSpace(userId))
                return userId.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName.Trim().ToLowerInvariant();

            return string.Empty;
        }

        public static void TryRecordScore(string displayName, string userId, int successPercent)
        {
            string key = BuildPlayerKey(displayName, userId);
            if (string.IsNullOrEmpty(key))
                return;

            successPercent = Mathf.Clamp(successPercent, 0, 100);
            string name = string.IsNullOrWhiteSpace(displayName)
                ? (string.IsNullOrWhiteSpace(userId) ? key : userId.Trim())
                : displayName.Trim();

            PersistedList list = LoadList();
            list.items.RemoveAll(e => e != null && string.Equals(e.playerKey, key, StringComparison.Ordinal));
            list.items.Add(new PersistedEntry
            {
                playerKey = key,
                displayName = name,
                successPercent = successPercent
            });

            list.items = list.items
                .Where(e => e != null && !string.IsNullOrEmpty(e.playerKey))
                .OrderByDescending(e => e.successPercent)
                .ThenBy(e => e.displayName, StringComparer.OrdinalIgnoreCase)
                .Take(MaxEntries)
                .ToList();

            SaveList(list);
        }

        /// <summary>
        /// Tüm leaderboard verisini siler (PlayerPrefs anahtarını kaldırır).
        /// </summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        public static IReadOnlyList<string> GetDisplayLines(int maxLines = MaxEntries)
        {
            PersistedList list = LoadList();
            var lines = new List<string>(maxLines);
            int i = 0;

            for (; i < list.items.Count && i < maxLines; i++)
            {
                PersistedEntry entry = list.items[i];
                if (entry == null)
                {
                    lines.Add(EmptySlotDisplay);
                    continue;
                }

                string left = string.IsNullOrEmpty(entry.displayName) ? entry.playerKey : entry.displayName;
                if (left.Length > 22)
                    left = left.Substring(0, 20) + "...";

                lines.Add($"{left} — %{entry.successPercent}");
            }

            for (; i < maxLines; i++)
                lines.Add(EmptySlotDisplay);

            return lines;
        }

        private static PersistedList LoadList()
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
                Debug.LogWarning($"[WasteLeaderboardStore] Resetting leaderboard prefs: {ex.Message}");
                return new PersistedList();
            }
        }

        private static void SaveList(PersistedList list)
        {
            string json = JsonUtility.ToJson(list ?? new PersistedList());
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
        }
    }
}
