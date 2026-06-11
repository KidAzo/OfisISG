using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Appends Office Fire session rows to Desktop/OfisYanginOyuncuSonuclari.
    /// Correct and missing objectives are stacked in single cells (newline-separated), like Waste module CSV.
    /// </summary>
    public static class OfficeFireSessionResultCsvExporter
    {
        private const char Delimiter = ';';
        private const string DesktopFolderName = "OfisYanginOyuncuSonuclari";
        private const string FileName = "ofis_yangin_oyuncu_sonuclari.csv";

        public static string ExportSession(OfficeFireScenarioReport report, bool turkish)
        {
            if (report == null)
            {
                Debug.LogWarning("[OfficeFireSessionResultCsvExporter] No report to export.");
                return null;
            }

            OfficeFireResultScreenModel model = OfficeFireResultScreenMapper.FromReport(report, turkish);
            ResolveIdentity(out string userName, out string userId);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string scenario = OfficeFireResultScreenMapper.GetScenarioTitle(report.scenarioId, turkish);
            string reactionTime = OfficeFireResultScreenMapper.FormatReactionTimeForExport(report.reactionTime);
            string fireControlled = FormatBool(
                report.fireControlled,
                turkish ? "Kontrol Altına Alındı" : "Brought Under Control",
                turkish ? "Kontrol Altına Alınmadı" : "Not Brought Under Control");
            string evacuated = FormatBool(
                report.evacuated,
                turkish ? "Tamamlandı" : "Completed",
                turkish ? "Tamamlanmadı" : "Not completed");
            string overall = model.StatusLabel;
            string corrects = JoinLines(model.CompletedObjectives);
            string missing = JoinLines(model.MissingObjectives);
            string mistakes = JoinLines(model.Mistakes);

            try
            {
                string directory = GetExportDirectory();
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string filePath = Path.Combine(directory, FileName);
                bool fileExists = File.Exists(filePath);

                using var writer = new StreamWriter(
                    filePath,
                    append: true,
                    encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: !fileExists));

                if (!fileExists)
                {
                    WriteHeaderRow(writer);
                }
                else
                {
                    writer.WriteLine();
                }

                WriteRow(
                    writer,
                    timestamp,
                    userName,
                    userId,
                    scenario,
                    reactionTime,
                    fireControlled,
                    evacuated,
                    overall,
                    corrects,
                    missing,
                    mistakes);

                Debug.Log($"[OfficeFireSessionResultCsvExporter] Session exported → {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OfficeFireSessionResultCsvExporter] Export failed: {ex.Message}");
                return null;
            }
        }

        public static string GetExportDirectory()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (string.IsNullOrWhiteSpace(desktop))
            {
                desktop = Application.persistentDataPath;
            }

            return Path.Combine(desktop, DesktopFolderName);
        }

        public static void ResolveIdentity(out string userName, out string userId)
        {
            if (!string.IsNullOrWhiteSpace(OfficeFireLoginSession.UserName)
                || !string.IsNullOrWhiteSpace(OfficeFireLoginSession.UserId))
            {
                userName = OfficeFireLoginSession.UserName ?? string.Empty;
                userId = OfficeFireLoginSession.UserId ?? string.Empty;
                return;
            }

            userName = string.Empty;
            userId = string.Empty;
        }

        private static void WriteHeaderRow(TextWriter writer)
        {
            WriteRow(
                writer,
                "Rapor Tarihi",
                "Oyuncu Adı",
                "Oyuncu ID",
                "Senaryo",
                "Tepki Süresi",
                "Yangın Kontrolü",
                "Tahliye",
                "Genel Sonuç",
                "Doğrular",
                "Eksikler",
                "Hatalar");
        }

        private static void WriteRow(TextWriter writer, params string[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0)
                {
                    writer.Write(Delimiter);
                }

                writer.Write(EscapeField(cells[i]));
            }

            writer.WriteLine();
        }

        private static string JoinLines(IReadOnlyList<string> items)
        {
            if (items == null || items.Count == 0)
            {
                return "-";
            }

            return string.Join("\n", items);
        }

        private static string FormatBool(bool value, string yes, string no)
        {
            return value ? yes : no;
        }

        private static string EscapeField(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            bool mustQuote = value.IndexOfAny(new[] { Delimiter, '"', '\r', '\n' }) >= 0;
            if (!mustQuote)
            {
                return value;
            }

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
