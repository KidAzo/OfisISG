using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Woi.Events.Data;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Appends Excel-friendly flat session rows to Desktop/AtıkToplamaOyuncuSonucları.
    /// Player summary is written once on the first row; following rows in the same session
    /// contain only waste detail columns. Each new session appends below the previous one.
    /// </summary>
    public static class WasteSessionResultCsvExporter
    {
        private const char Delimiter = ';';
        private const string StatusCorrect = "🟢 DOĞRU";
        private const string StatusIncorrect = "🔴 HATALI";
        private const string DesktopFolderName = "AtıkToplamaOyuncuSonucları";
        private const string FileName = "atik_toplama_oyuncu_sonuclari.csv";

        public static string AppendSession(IReadOnlyList<WasteClassificationRecord> classifications)
        {
            if (classifications == null || classifications.Count == 0)
            {
                Debug.LogWarning("[WasteSessionResultCsvExporter] No classifications to export.");
                return null;
            }

            try
            {
                string directory = GetExportDirectory();
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string filePath = Path.Combine(directory, FileName);
                bool fileExists = File.Exists(filePath);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

                ResolveIdentity(out string userName, out string userId);
                BuildStats(classifications, out int correct, out int incorrect, out int successPercent, out string overall);

                using var writer = new StreamWriter(
                    filePath,
                    append: true,
                    encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: !fileExists));

                if (!fileExists)
                    WriteHeaderRow(writer);
                else
                    writer.WriteLine();

                WriteSessionRows(
                    writer,
                    timestamp,
                    userName,
                    userId,
                    classifications.Count,
                    correct,
                    incorrect,
                    successPercent,
                    overall,
                    classifications);

                Debug.Log($"[WasteSessionResultCsvExporter] Session exported → {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WasteSessionResultCsvExporter] Export failed: {ex.Message}");
                return null;
            }
        }

        public static string GetExportDirectory()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (string.IsNullOrWhiteSpace(desktop))
                desktop = Application.persistentDataPath;

            return Path.Combine(desktop, DesktopFolderName);
        }

        private static void WriteHeaderRow(TextWriter writer)
        {
            WriteRow(
                writer,
                "Rapor Tarihi",
                "Oyuncu Adı",
                "Oyuncu ID",
                "Toplam Atık",
                "Doğru Sayısı",
                "Hatalı Sayısı",
                "Başarı Oranı",
                "Genel Sonuç",
                "Atık Adı",
                "Atılan Kutu",
                "Doğru Atılması Gereken Yer",
                "Durum");
        }

        private static void WriteSessionRows(
            TextWriter writer,
            string timestamp,
            string userName,
            string userId,
            int total,
            int correct,
            int incorrect,
            int successPercent,
            string overall,
            IReadOnlyList<WasteClassificationRecord> classifications)
        {
            string totalText = total.ToString(CultureInfo.InvariantCulture);
            string correctText = correct.ToString(CultureInfo.InvariantCulture);
            string incorrectText = incorrect.ToString(CultureInfo.InvariantCulture);
            string successText = $"%{successPercent}";

            for (int i = 0; i < classifications.Count; i++)
            {
                WasteClassificationRecord record = classifications[i];
                string wasteName = FormatWasteName(record.wasteName);
                string selectedBin = ResolveSelectedBinName(record);
                string correctBin = ResolveCorrectBinName(record);
                string status = FormatStatus(record.isCorrect);

                if (i == 0)
                {
                    WriteRow(
                        writer,
                        timestamp,
                        userName,
                        userId,
                        totalText,
                        correctText,
                        incorrectText,
                        successText,
                        overall,
                        wasteName,
                        selectedBin,
                        correctBin,
                        status);
                }
                else
                {
                    WriteRow(
                        writer,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        wasteName,
                        selectedBin,
                        correctBin,
                        status);
                }
            }
        }

        private static string FormatStatus(bool isCorrect)
        {
            return isCorrect ? StatusCorrect : StatusIncorrect;
        }

        private static void WriteRow(TextWriter writer, params string[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0)
                    writer.Write(Delimiter);

                writer.Write(EscapeField(cells[i]));
            }

            writer.WriteLine();
        }

        private static void ResolveIdentity(out string userName, out string userId)
        {
            if (WasteLoginSession.IsSet)
            {
                userName = WasteLoginSession.UserName;
                userId = WasteLoginSession.UserId;
                return;
            }

            if (GameSessionData.IsSet)
            {
                userName = GameSessionData.UserName;
                userId = GameSessionData.UserId;
                return;
            }

            userName = string.Empty;
            userId = string.Empty;
        }

        private static void BuildStats(
            IReadOnlyList<WasteClassificationRecord> classifications,
            out int correct,
            out int incorrect,
            out int successPercent,
            out string overall)
        {
            correct = 0;
            incorrect = 0;

            for (int i = 0; i < classifications.Count; i++)
            {
                if (classifications[i].isCorrect)
                    correct++;
                else
                    incorrect++;
            }

            successPercent = classifications.Count > 0
                ? Mathf.RoundToInt(correct * 100f / classifications.Count)
                : 0;

            overall = successPercent switch
            {
                100 => "Mükemmel",
                >= 80 => "İyi",
                >= 50 => "Geliştirilmeli",
                _ => "Başlangıç",
            };
        }

        private static string ResolveSelectedBinName(WasteClassificationRecord record)
        {
            if (!string.IsNullOrWhiteSpace(record.selectedBinName))
                return record.selectedBinName;

            return WasteBinCatalog.GetBinName(record.selectedBinId);
        }

        private static string ResolveCorrectBinName(WasteClassificationRecord record)
        {
            if (!string.IsNullOrWhiteSpace(record.correctBinName))
                return record.correctBinName;

            return WasteBinCatalog.GetBinName(record.correctBinId);
        }

        private static string FormatWasteName(string wasteName)
        {
            return WasteNameCatalog.GetDisplayName(wasteName);
        }

        private static string EscapeField(string value)
        {
            if (value == null)
                return string.Empty;

            bool mustQuote = value.IndexOfAny(new[] { Delimiter, '"', '\r', '\n' }) >= 0;
            if (!mustQuote)
                return value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
