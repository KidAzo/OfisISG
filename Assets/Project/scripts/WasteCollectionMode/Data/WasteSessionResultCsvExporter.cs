using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Woi.DataHandler;
using Woi.Events.Data;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Appends Excel-friendly flat session rows to Desktop/AtıkToplamaOyuncuSonucları (PC login)
    /// and, when <see cref="SessionManager"/> is present (VR), POSTs the same CSV block to the PC server
    /// via <see cref="SessionManager.SendResultToPC"/> so the host Excel file matches the local export.
    /// </summary>
    public static class WasteSessionResultCsvExporter
    {
        private const char Delimiter = ';';
        private const string StatusCorrect = "🟢 DOĞRU";
        private const string StatusIncorrect = "🔴 HATALI";
        private const string DesktopFolderName = "AtıkToplamaOyuncuSonucları";
        private const string FileName = "atik_toplama_oyuncu_sonuclari.csv";

        /// <summary>True after column headers were included in a PC <c>save-result</c> upload this app run.</summary>
        private static bool pcServerWasteHeaderSent;

        /// <summary>Writes the session to disk and uploads to PC when a session manager exists.</summary>
        public static string ExportSession(IReadOnlyList<WasteClassificationRecord> classifications) =>
            ExportSession(classifications, ShouldUploadToPcServer());

        public static string ExportSession(
            IReadOnlyList<WasteClassificationRecord> classifications,
            bool uploadToPcServer)
        {
            if (classifications == null || classifications.Count == 0)
            {
                Debug.LogWarning("[WasteSessionResultCsvExporter] No classifications to export.");
                return null;
            }

            string localPath = null;
            string payload = BuildSessionAppendPayload(classifications, includeLeadingSessionSeparator: true);

            try
            {
                localPath = AppendSessionToLocalFile(classifications, payload);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WasteSessionResultCsvExporter] Local export failed: {ex.Message}");
            }

            if (uploadToPcServer)
                TryUploadToPcServer(classifications);

            return localPath;
        }

        /// <summary>
        /// Payload for PC server: column header once per app run, then the same block as the local CSV append.
        /// </summary>
        public static string BuildPcServerUploadPayload(IReadOnlyList<WasteClassificationRecord> classifications)
        {
            if (classifications == null || classifications.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();

            if (!pcServerWasteHeaderSent)
            {
                using (var writer = new StringWriter(builder))
                    WriteHeaderRow(writer);

                pcServerWasteHeaderSent = true;
            }

            builder.Append(BuildSessionAppendPayload(classifications, includeLeadingSessionSeparator: true));
            return builder.ToString();
        }

        /// <summary>Allows the next VR upload to prepend column headers again (e.g. after clearing the server CSV).</summary>
        public static void ResetPcServerUploadHeaderState() => pcServerWasteHeaderSent = false;

        /// <summary>
        /// CSV rows appended for one session (optional leading blank line), identical to the local file block.
        /// </summary>
        public static string BuildSessionAppendPayload(
            IReadOnlyList<WasteClassificationRecord> classifications,
            bool includeLeadingSessionSeparator = true)
        {
            if (classifications == null || classifications.Count == 0)
                return string.Empty;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            ResolveIdentity(out string userName, out string userId);
            BuildStats(classifications, out int correct, out int incorrect, out int successPercent, out string overall);

            var builder = new StringBuilder();
            if (includeLeadingSessionSeparator)
                builder.AppendLine();

            using (var writer = new StringWriter(builder))
            {
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
            }

            return builder.ToString();
        }

        /// <inheritdoc cref="ExportSession"/>
        public static string AppendSession(IReadOnlyList<WasteClassificationRecord> classifications) =>
            ExportSession(classifications);

        public static void ResolveIdentity(out string userName, out string userId)
        {
            if (GameSessionData.IsSet)
            {
                userName = GameSessionData.UserName ?? string.Empty;
                userId = GameSessionData.UserId ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(userName) || !string.IsNullOrWhiteSpace(userId))
                    return;
            }

            PlayerSession session = SessionManager.Instance != null
                ? SessionManager.Instance.CurrentSession
                : null;

            if (session != null && session.IsActive)
            {
                userName = session.PlayerName ?? string.Empty;
                userId = session.PlayerID > 0
                    ? session.PlayerID.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
                return;
            }

            if (WasteLoginSession.IsSet)
            {
                userName = WasteLoginSession.UserName;
                userId = WasteLoginSession.UserId;
                return;
            }

            userName = string.Empty;
            userId = string.Empty;
        }

        public static string GetExportDirectory()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (string.IsNullOrWhiteSpace(desktop))
                desktop = Application.persistentDataPath;

            return Path.Combine(desktop, DesktopFolderName);
        }

        private static string AppendSessionToLocalFile(
            IReadOnlyList<WasteClassificationRecord> classifications,
            string payload)
        {
            string directory = GetExportDirectory();
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string filePath = Path.Combine(directory, FileName);
            bool fileExists = File.Exists(filePath);

            using var writer = new StreamWriter(
                filePath,
                append: true,
                encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: !fileExists));

            if (!fileExists)
                WriteHeaderRow(writer);

            writer.Write(payload);

            Debug.Log($"[WasteSessionResultCsvExporter] Session exported → {filePath} ({classifications.Count} row(s))");
            return filePath;
        }

        private static bool ShouldUploadToPcServer() => FindSessionManager() != null;

        private static void TryUploadToPcServer(IReadOnlyList<WasteClassificationRecord> classifications)
        {
            bool includesHeader = !pcServerWasteHeaderSent;
            string payload = BuildPcServerUploadPayload(classifications);
            if (string.IsNullOrWhiteSpace(payload))
                return;

            SessionManager manager = FindSessionManager();
            if (manager == null)
            {
                Debug.LogWarning(
                    "[WasteSessionResultCsvExporter] SessionManager not found; CSV not sent to PC server.");
                return;
            }

            manager.SendResultToPC(payload);
            Debug.Log(includesHeader
                ? "[WasteSessionResultCsvExporter] Waste session CSV sent to PC server (save-result, with column headers)."
                : "[WasteSessionResultCsvExporter] Waste session CSV sent to PC server (save-result).");
        }

        private static SessionManager FindSessionManager()
        {
            if (SessionManager.Instance != null)
                return SessionManager.Instance;

            return UnityEngine.Object.FindFirstObjectByType<SessionManager>(FindObjectsInactive.Include);
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

        private static string FormatStatus(bool isCorrect) =>
            isCorrect ? StatusCorrect : StatusIncorrect;

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
            if (!string.IsNullOrWhiteSpace(record.selectedBinId))
                return WasteBinCatalog.GetBinName(record.selectedBinId);

            return string.IsNullOrWhiteSpace(record.selectedBinName)
                ? "-"
                : record.selectedBinName;
        }

        private static string ResolveCorrectBinName(WasteClassificationRecord record)
        {
            if (!string.IsNullOrWhiteSpace(record.correctBinId))
                return WasteBinCatalog.GetBinName(record.correctBinId);

            return string.IsNullOrWhiteSpace(record.correctBinName)
                ? "-"
                : record.correctBinName;
        }

        private static string FormatWasteName(string wasteName) =>
            WasteNameCatalog.GetDisplayName(wasteName);

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
