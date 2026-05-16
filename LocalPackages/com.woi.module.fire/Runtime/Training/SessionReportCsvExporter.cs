using System;
using System.IO;
using System.Linq;
using System.Text;
using FireExtinguisher.Core;
using UnityEngine;
using Woi.Events.Data;

namespace Woi.Game.Training
{
    /// <summary>
    /// Appends company-facing CSV rows: one row per <see cref="SessionReport.FireInstances"/> when present,
    /// otherwise a single aggregate row from <see cref="SessionReport.Client"/>. For multi-fire sessions,
    /// session-wide fields (identity, depletion, pass/fail, rules, score, duration, time-to-response,
    /// critical mistakes) are written only on the first row. <c>UsedExtinguisherType</c> is repeated on every
    /// fire row (same session-wide value). Continuation rows otherwise carry per-fire columns
    /// (<c>FireSourceKey</c> through <c>FireFullyExtinguished</c>). Column order is in <see cref="CompanyColumnNames"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SessionReportCsvExporter : MonoBehaviour
    {
        /// <summary>Single source of truth for header and row cell count.</summary>
        private static readonly string[] CompanyColumnNames =
        {
            "ExportTimestamp_Local",   // 0
            "TraineeId",               // 1
            "FireSourceKey",           // 2
            "FireClass",               // 3
            "RequiredExtinguisherType",// 4
            "UsedExtinguisherType",    // 5
            "CorrectExtinguisherSelected", // 6
            "FireFullyExtinguished",   // 7
            "CompletedBeforeDepletion",// 8
            "OverallTrainingPassed",   // 9
            "RulesEvaluated",          // 10
            "FinalScore",              // 11
            "SessionDuration_s",       // 12
            "TimeToFirstResponse_s",   // 13
            "CriticalMistakes",        // 14
            "SweepPerformed",          // 15
            "SweepRulePassed",         // 16
            "SweepCoverageWidth_m",    // 17
            "SweepDuration_s",         // 18
            "SweepPerformance_pct",    // 19
            "SweepFeedback",           // 20
        };

        [Header("Recorder")]
        [SerializeField] private ExtinguisherSessionRecorder _recorder;

        [Header("Login / Identity")]
        [Tooltip("Fallback TraineeId written to CSV when no login data is available " +
                 "(GameSessionData.UserId is always preferred automatically).")]
        [SerializeField] private string _playerId = "unknown";

        [Header("File Settings")]
        [Tooltip("Sub-folder on Desktop (e.g. Desktop/SessionReports). Leave empty for Desktop root.")]
        [SerializeField] private string _subFolder = "SessionReports";

        [Tooltip("CSV file name; rows accumulate across runs. Delete the file if you change column layout.")]
        [SerializeField] private string _fileName = "fire_training_sessions.csv";

        public event Action<string /*playerId*/, SessionReport> OnReportReadyForUpload;

        private void OnEnable()
        {
            if (_recorder == null)
            {
                Debug.LogWarning($"[{nameof(SessionReportCsvExporter)}] SessionRecorder not assigned on {gameObject.name}.", this);
                return;
            }

            _recorder.OnSessionEnded += HandleSessionEnded;
        }

        private void OnDisable()
        {
            if (_recorder != null)
                _recorder.OnSessionEnded -= HandleSessionEnded;
        }

        public void SetPlayerId(string playerId) => _playerId = playerId;

        private void HandleSessionEnded(SessionReport report)
        {
            WriteRow(report);
            OnReportReadyForUpload?.Invoke(ResolveTraineeId(report), report);
        }

        private string ResolveTraineeId(SessionReport report)
            => ResolveTraineeId(report?.Client);

        /// <summary>
        /// Resolution order:
        /// 1. TraineeId baked into the session begin context (set programmatically via BeginSession).
        /// 2. GameSessionData.UserId — static, written by GameInitializer at login; no Inspector wiring needed.
        /// 3. Inspector fallback _playerId.
        /// Logs a warning and returns empty string when all three are absent.
        /// </summary>
        private string ResolveTraineeId(TrainingClientSummary c)
        {
            if (c != null && !string.IsNullOrEmpty(c.TraineeId))
                return c.TraineeId;

            if (GameSessionData.IsSet && !string.IsNullOrEmpty(GameSessionData.UserId))
                return GameSessionData.UserId;

            if (!string.IsNullOrEmpty(_playerId))
                return _playerId;

            Debug.LogWarning(
                $"[{nameof(SessionReportCsvExporter)}] TraineeId could not be resolved from session context, " +
                "GameSessionData, or fallback playerId. Writing empty string.", this);
            return string.Empty;
        }

        private string GetExportDirectory()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (string.IsNullOrEmpty(desktop))
                desktop = Application.persistentDataPath;

            return string.IsNullOrEmpty(_subFolder)
                ? desktop
                : Path.Combine(desktop, _subFolder);
        }

        private void WriteRow(SessionReport report)
        {
            try
            {
                string directory = GetExportDirectory();
                Directory.CreateDirectory(directory);

                string filePath = Path.Combine(directory, _fileName);
                bool fileExists = File.Exists(filePath);

                using var writer = new StreamWriter(filePath, append: true, encoding: Encoding.UTF8);

                if (!fileExists)
                    writer.WriteLine(BuildHeader());

                string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                int rowCount = 0;
                if (report.FireInstances != null && report.FireInstances.Count > 0)
                {
                    bool writeSessionWide = true;
                    foreach (TrainingFireInstanceReport fire in report.FireInstances)
                    {
                        writer.WriteLine(BuildPerFireRow(report, fire, stamp, writeSessionWide));
                        writeSessionWide = false;
                        rowCount++;
                    }
                }
                else
                {
                    writer.WriteLine(BuildAggregateRow(report, stamp));
                    rowCount = 1;
                }

                Debug.Log($"[{nameof(SessionReportCsvExporter)}] Company CSV: {rowCount} row(s) → {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{nameof(SessionReportCsvExporter)}] Failed to write CSV: {ex.Message}", this);
            }
        }

        private static string BuildHeader()
            => string.Join(",", CompanyColumnNames);

        private string BuildAggregateRow(SessionReport r, string exportTimestampLocal)
        {
            TrainingClientSummary c = r.Client;
            string trainee = ResolveTraineeId(c);
            string mistakesJoined = JoinCriticalMistakes(c);

            var cells = new string[CompanyColumnNames.Length];
            cells[0]  = exportTimestampLocal;
            cells[1]  = trainee;
            cells[2]  = string.Empty;                                              // FireSourceKey — empty for aggregate
            cells[3]  = FormatFireClass(c.HasFireClass, c.FireClass);
            cells[4]  = FormatExtinguisherType(c.HasRequiredExtinguisherType, c.RequiredExtinguisherType);
            cells[5]  = FormatExtinguisherType(c.HasUsedExtinguisherType, c.UsedExtinguisherType);
            cells[6]  = FormatYesNoUnknown(
                !c.HasRequiredExtinguisherType || !c.HasUsedExtinguisherType,
                c.CorrectExtinguisherSelected);
            cells[7]  = c.FireFullyExtinguished ? "Yes" : "No";
            cells[8]  = c.ExtinguisherDepletedBeforeCompletion ? "Yes" : "No";
            cells[9]  = FormatPassFail(c.OverallTrainingPassed);
            cells[10] = c.RulesEvaluated ? "Yes" : "No";
            cells[11] = FormatFinalScorePercent(c.FinalScore);
            cells[12] = c.SessionDurationSeconds.ToString("F1");
            cells[13] = c.TimeToFirstResponseSeconds >= 0f
                ? c.TimeToFirstResponseSeconds.ToString("F1")
                : "—";
            cells[14] = mistakesJoined;
            AppendSweepCells(r, cells, writeSessionWide: true);

            return string.Join(",", cells.Select(EscapeCsvField));
        }

        private string BuildPerFireRow(
            SessionReport r,
            TrainingFireInstanceReport fire,
            string exportTimestampLocal,
            bool writeSessionWideColumns)
        {
            TrainingClientSummary c = r.Client;
            string trainee = ResolveTraineeId(c);
            string mistakesJoined = JoinCriticalMistakes(c);

            bool correctUnknown = !fire.HasAllowedExtinguisherTypes
                || !fire.HadSprayContactOnThisFire
                || !fire.HasUsedExtinguisherTypeOnThisFire;

            var cells = new string[CompanyColumnNames.Length];
            cells[0]  = writeSessionWideColumns ? exportTimestampLocal : string.Empty;
            cells[1]  = writeSessionWideColumns ? trainee : string.Empty;
            cells[2]  = fire.FireSourceKey;                                        // FireSourceKey: unique per FireSource
            cells[3]  = FormatFireClass(true, fire.FireClass);
            cells[4]  = fire.HasAllowedExtinguisherTypes ? fire.RequiredExtinguishersDisplay : string.Empty;
            cells[5]  = FormatExtinguisherType(fire.HasUsedExtinguisherTypeOnThisFire, fire.DominantUsedExtinguisherTypeOnThisFire);
            cells[6]  = FormatYesNoUnknown(correctUnknown, fire.CorrectExtinguisherSelected);
            cells[7]  = fire.FireFullyExtinguished ? "Yes" : "No";
            cells[8]  = writeSessionWideColumns
                ? (c.ExtinguisherDepletedBeforeCompletion ? "Yes" : "No")
                : string.Empty;
            cells[9]  = writeSessionWideColumns ? FormatPassFail(c.OverallTrainingPassed) : string.Empty;
            cells[10] = writeSessionWideColumns ? (c.RulesEvaluated ? "Yes" : "No") : string.Empty;
            cells[11] = writeSessionWideColumns ? FormatFinalScorePercent(c.FinalScore) : string.Empty;
            cells[12] = writeSessionWideColumns ? c.SessionDurationSeconds.ToString("F1") : string.Empty;
            cells[13] = writeSessionWideColumns
                ? (c.TimeToFirstResponseSeconds >= 0f
                    ? c.TimeToFirstResponseSeconds.ToString("F1")
                    : "—")
                : string.Empty;
            cells[14] = writeSessionWideColumns ? mistakesJoined : string.Empty;
            AppendSweepCells(r, cells, writeSessionWideColumns);

            return string.Join(",", cells.Select(EscapeCsvField));
        }

        private static void AppendSweepCells(SessionReport report, string[] cells, bool writeSessionWide)
        {
            if (report?.Sweep == null || !writeSessionWide)
            {
                cells[15] = string.Empty;
                cells[16] = string.Empty;
                cells[17] = string.Empty;
                cells[18] = string.Empty;
                cells[19] = string.Empty;
                cells[20] = string.Empty;
                return;
            }

            TrainingSweepMetrics s = report.Sweep;
            cells[15] = s.SweepPerformed ? "Yes" : "No";
            cells[16] = s.SweepRulePassed ? "Yes" : "No";
            cells[17] = s.SweepCoverageWidth.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            cells[18] = s.SweepDurationSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            int perfPct = Mathf.Clamp(Mathf.RoundToInt(s.SweepPerformanceScore * 100f), 0, 100);
            cells[19] = perfPct.ToString();
            cells[20] = s.SweepFeedbackText ?? string.Empty;
        }

        private string JoinCriticalMistakes(TrainingClientSummary c)
        {
            return c.CriticalMistakes.Count > 0
                ? string.Join(" | ", c.CriticalMistakes.Select(EscapeCsvFragment))
                : string.Empty;
        }

        private static string FormatFireClass(bool hasFireClass, FireClass fireClass)
        {
            if (!hasFireClass) return string.Empty;
            return fireClass switch
            {
                FireClass.A => "Class A (solids)",
                FireClass.B => "Class B (liquids)",
                FireClass.C => "Class C (gases)",
                FireClass.D => "Class D (metals)",
                FireClass.F => "Class F (cooking oils)",
                FireClass.E => "Class E (electrical)",
                _           => $"Class {fireClass}",
            };
        }

        private static string FormatExtinguisherType(bool known, ExtinguisherType type)
        {
            if (!known) return string.Empty;
            return type switch
            {
                ExtinguisherType.Water       => "Water",
                ExtinguisherType.Foam        => "Foam (AFFF)",
                ExtinguisherType.DryPowder   => "Dry powder (ABC)",
                ExtinguisherType.CO2         => "CO₂",
                ExtinguisherType.WetChemical => "Wet chemical",
                _                            => type.ToString(),
            };
        }

        /// <summary>Yes / No, or Unknown when required/used type was not recorded.</summary>
        private static string FormatYesNoUnknown(bool unknown, bool value)
        {
            if (unknown) return "Unknown";
            return value ? "Yes" : "No";
        }

        private static string FormatPassFail(bool? passed)
        {
            if (!passed.HasValue) return "Not evaluated";
            return passed.Value ? "Pass" : "Fail";
        }

        /// <summary>Business-friendly 0–100 score (integer percent).</summary>
        private static string FormatFinalScorePercent(float score01)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt(score01 * 100f), 0, 100);
            return pct.ToString();
        }

        private static string EscapeCsvFragment(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("|", "/");
        }

        /// <summary>Quotes a field if it contains comma, quote, CR, or LF.</summary>
        private static string EscapeCsvField(string value)
        {
            if (value == null) return string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
