using System.IO;
using System.Linq;
using System.Text;
using System;
using Woi.HazardSystem;
using System.Collections.Generic;
using UnityEngine;

namespace Woi.DataHandler
{
    public static class HazardCsvExporter
    {
        private const string FileName = "HazardResults.csv";
        private const char Sep = ';';
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        private const string Header =
            "Player Name;" +
            "Player ID;" +
            "Player Duration;" +
            "Detected Hazards;" +
            "Undetected Hazards;" +
            "Detected Count;" +
            "Undetected Count;" +
            "Safety Score" +
            "Date Time;";

        //public static void Append(string playerName, int playerID, TimeSpan duration, HazardCheckResult result)
        // {
        //     string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        //     string filePath = Path.Combine(desktopPath, FileName);

        //     bool fileExists = File.Exists(filePath);
        //     var sb = new StringBuilder();

        //     if (!fileExists)
        //         sb.AppendLine(Header);

        //     string dateTime = DateTime.Now.ToString(DateTimeFormat);
        // 	string durationData = $"{duration.Minutes:00}:{duration.Seconds:00}";

        //     // Hücre içi ALT ALTA liste (• ile)
        //     string founded = FormatList(result.foundedChecks.Select(x => x.TaskName));
        //     string missed = FormatList(result.missedChecks.Select(x => x.TaskName));

        //     string scoreText = $"{result.Score} ({GetScoreLabel(result.Score)})";

        //     sb.Append(Escape(playerName)).Append(Sep)
        //         .Append(Escape(playerID.ToString())).Append(Sep)
        //         .Append(Escape(durationData)).Append(Sep)
        //       .Append(Escape(founded)).Append(Sep)
        //       .Append(Escape(missed)).Append(Sep)
        //       .Append(result.foundedChecks.Count).Append(Sep)
        //       .Append(result.missedChecks.Count).Append(Sep)
        //       .Append(Escape(scoreText)).Append(Sep)
        //       .Append(Escape(dateTime))
        //       .AppendLine();

        //     // UTF8 BOM → Türkçe karakterler Excel'de garanti
        //     File.AppendAllText(
        //         filePath,
        //         sb.ToString(),
        //         new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
        //     );
        // }

        public static void AppendSession(PlayerSession session, TimeSpan duration, HazardCheckResult result)
        {
            if (session == null)
            {
                Debug.LogError("[CSV] Session null! Kayıt yapılamadı.");
                return;
            }

            // CSV satırını oluştur
            string csvLine = GenerateCSVLine(
                session.PlayerName,
                session.PlayerID,
                duration,
                result
            );

            SessionManager.Instance.SendResultToPC(csvLine);
            Debug.Log($"[CSV] Sonuç oluşturuldu ve PC'ye gönderildi: {session}");
        }

        // CSV satırını oluşturan yardımcı metod
        private static string GenerateCSVLine(
            string playerName,
            int playerID,
            TimeSpan duration,
            HazardCheckResult result)
        {
            var sb = new StringBuilder();

            string dateTime = DateTime.Now.ToString(DateTimeFormat);
            string durationData = $"{duration.Minutes:00}:{duration.Seconds:00}";

            string founded = FormatList(result.foundedChecks.Select(x => x.TaskName));
            string missed = FormatList(result.missedChecks.Select(x => x.TaskName));
            string scoreText = $"{result.Score} ({GetScoreLabel(result.Score)})";

            sb.Append(Escape(playerName)).Append(Sep)
              .Append(Escape(playerID.ToString())).Append(Sep)
              .Append(Escape(durationData)).Append(Sep)
              .Append(Escape(founded)).Append(Sep)
              .Append(Escape(missed)).Append(Sep)
              .Append(result.foundedChecks.Count).Append(Sep)
              .Append(result.missedChecks.Count).Append(Sep)
              .Append(Escape(scoreText)).Append(Sep)
              .Append(Escape(dateTime));

            return sb.ToString();
        }

        private static string FormatList(System.Collections.Generic.IEnumerable<string> items)
        {
            var list = items.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (list.Count == 0)
                return "—";

            return "• " + string.Join("\n• ", list);
        }

        private static string GetScoreLabel(int score)
        {
            if (score < 40) return "Zayıf";
            if (score < 70) return "Orta";
            return "İyi";
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            bool mustQuote =
                value.Contains(Sep) ||
                value.Contains(',') ||
                value.Contains('"') ||
                value.Contains('\n') ||
                value.Contains('\r');

            if (!mustQuote) return value;

            value = value.Replace("\"", "\"\"");
            return $"\"{value}\"";
        }
    }

}
