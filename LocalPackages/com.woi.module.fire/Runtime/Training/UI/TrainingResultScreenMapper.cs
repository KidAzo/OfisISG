using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FireExtinguisher.Core;
using UnityEngine;
using Woi.Game.Training;
using Woi.UI.Popups.Localization;

namespace Woi.Game.Training.UI
{
    /// <summary>
    /// Maps <see cref="SessionReport"/> (and related domain types) into <see cref="TrainingResultScreenModel"/>.
    /// Keeps UI Toolkit layers free of FireExtinguisher / recorder details.
    /// </summary>
    public static class TrainingResultScreenMapper
    {
        static string L(string english, string turkish) => LocalizedUiPair.Resolve(english, turkish);

        public static TrainingResultScreenModel FromSessionReport(SessionReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            return FromSessionReportCore(report);
        }

        static TrainingResultScreenModel FromSessionReportCore(SessionReport report)
        {
            TrainingClientSummary c = report.Client;
            TrainingTechnicalMetrics t = report.Technical;

            var header = new TrainingResultHeaderModel(
                scenarioTitle: ResolveScenarioTitle(c),
                resultLabel: FormatOverallResult(c),
                resultTone: ResolveResultTone(c),
                finalScorePercent: Mathf.Clamp(Mathf.RoundToInt(c.FinalScore * 100f), 0, 100),
                sessionDurationDisplay: FormatDuration(c.SessionDurationSeconds),
                timeToFirstResponseDisplay: c.TimeToFirstResponseSeconds >= 0f
                    ? FormatDuration(c.TimeToFirstResponseSeconds)
                    : "—");

            IReadOnlyList<string> criticalRaw = c.CriticalMistakes.ToList();
            IReadOnlyList<TrainingResultFireCardModel> fires = BuildFireCards(report, c, criticalRaw);
            IReadOnlyList<TrainingResultMetricRowModel> eval = BuildOverallEvaluation(c, t, report.Sweep);
            List<string> mistakes = criticalRaw.Select(TrainingResultUiLanguage.LocalizeSessionMessageForDisplay).ToList();
            var advanced = new TrainingResultAdvancedModel(BuildAdvancedTableRows(report));

            return new TrainingResultScreenModel(header, fires, eval, mistakes, advanced);
        }

        private static string ResolveScenarioTitle(TrainingClientSummary c)
        {
            if (!string.IsNullOrEmpty(c.ScenarioDisplayName))
                return c.ScenarioDisplayName;
            if (!string.IsNullOrEmpty(c.ScenarioId))
                return c.ScenarioId;
            return L("Training session", "Eğitim oturumu");
        }

        private static string FormatOverallResult(TrainingClientSummary c)
        {
            if (!c.RulesEvaluated || !c.OverallTrainingPassed.HasValue)
                return L("Not evaluated", "Değerlendirilmedi");
            return c.OverallTrainingPassed.Value ? L("Pass", "Geçti") : L("Fail", "Başarısız");
        }

        private static string ResolveResultTone(TrainingClientSummary c)
        {
            if (!c.RulesEvaluated || !c.OverallTrainingPassed.HasValue)
                return "pending";
            return c.OverallTrainingPassed.Value ? "pass" : "fail";
        }

        private static IReadOnlyList<TrainingResultFireCardModel> BuildFireCards(
            SessionReport report,
            TrainingClientSummary c,
            IReadOnlyList<string> criticalForFireFilter)
        {
            var list = new List<TrainingResultFireCardModel>();

            if (report.FireInstances != null && report.FireInstances.Count > 0)
            {
                foreach (TrainingFireInstanceReport f in report.FireInstances)
                {
                    string used = f.HasUsedExtinguisherTypeOnThisFire
                        ? TrainingReportLabels.FormatExtinguisherType(
                            f.DominantUsedExtinguisherTypeOnThisFire,
                            TrainingResultUiLanguage.IsTurkish())
                        : "—";

                    bool correctKnown = f.HasAllowedExtinguisherTypes && f.HadSprayContactOnThisFire;

                    list.Add(new TrainingResultFireCardModel(
                        cardTitle: TrainingReportLabels.FormatFireClassShort(
                            f.FireClass,
                            TrainingResultUiLanguage.IsTurkish()),
                        fireClassDisplay: TrainingReportLabels.FormatFireClass(f.FireClass, TrainingResultUiLanguage.IsTurkish()),
                        requiredExtinguisherDisplay: f.HasAllowedExtinguisherTypes
                            ? TrainingReportLabels.LocalizeRequiredExtinguishersDisplay(
                                f.RequiredExtinguishersDisplay,
                                TrainingResultUiLanguage.IsTurkish())
                            : L("Any / not specified", "Belirtilmemiş / herhangi"),
                        usedExtinguisherDisplay: used,
                        correctExtinguisherKnown: correctKnown,
                        correctExtinguisherSelected: f.CorrectExtinguisherSelected,
                        fireExtinguished: f.FireFullyExtinguished,
                        depletionKnown: f.HadSprayContactOnThisFire,
                        depletedBeforeCompletion: c.ExtinguisherDepletedBeforeCompletion,
                        hasTimeToExtinguish: false,
                        timeToExtinguishDisplay: string.Empty,
                        keyMistakes: MistakesForFire(f.FireSourceKey, criticalForFireFilter)
                            .Select(TrainingResultUiLanguage.LocalizeSessionMessageForDisplay)
                            .ToList()));
                }

                return list;
            }

            // Aggregate single card when no per-fire breakdown
            string req = c.HasRequiredExtinguisherType
                ? TrainingReportLabels.FormatExtinguisherType(c.RequiredExtinguisherType, TrainingResultUiLanguage.IsTurkish())
                : "—";
            string usedAgg = c.HasUsedExtinguisherType
                ? TrainingReportLabels.FormatExtinguisherType(c.UsedExtinguisherType, TrainingResultUiLanguage.IsTurkish())
                : "—";
            bool hasClass = c.HasFireClass;

            list.Add(new TrainingResultFireCardModel(
                cardTitle: L("Fire scenario", "Yangın senaryosu"),
                fireClassDisplay: hasClass ? TrainingReportLabels.FormatFireClass(c.FireClass, TrainingResultUiLanguage.IsTurkish()) : "—",
                requiredExtinguisherDisplay: req,
                usedExtinguisherDisplay: usedAgg,
                correctExtinguisherKnown: c.HasRequiredExtinguisherType && c.HasUsedExtinguisherType,
                correctExtinguisherSelected: c.CorrectExtinguisherSelected,
                fireExtinguished: c.FireFullyExtinguished,
                depletionKnown: true,
                depletedBeforeCompletion: c.ExtinguisherDepletedBeforeCompletion,
                hasTimeToExtinguish: false,
                timeToExtinguishDisplay: string.Empty,
                keyMistakes: Array.Empty<string>()));

            return list;
        }

        private static List<string> MistakesForFire(string fireKey, IReadOnlyList<string> all)
        {
            if (all == null || all.Count == 0 || string.IsNullOrEmpty(fireKey))
                return new List<string>();

            // Match against the quoted token "'{fireKey}'" produced by the recorder:
            //   "Fire 'A' was not fully extinguished."
            // Using the bare key (e.g. "A") caused false positives against any sentence
            // containing that letter. Quoting makes the match specific to this fire.
            string token = $"'{fireKey}'";

            var matched = new List<string>();
            foreach (string m in all)
            {
                if (string.IsNullOrEmpty(m)) continue;
                if (m.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    matched.Add(m);
            }

            return matched;
        }

        private static IReadOnlyList<TrainingResultMetricRowModel> BuildOverallEvaluation(
            TrainingClientSummary c,
            TrainingTechnicalMetrics t,
            TrainingSweepMetrics sweep)
        {
            var rows = new List<TrainingResultMetricRowModel>();

            bool correctKnown = c.HasRequiredExtinguisherType && c.HasUsedExtinguisherType;
            rows.Add(new TrainingResultMetricRowModel(
                L("Correct extinguisher", "Doğru söndürücü"),
                !correctKnown ? "unknown" : (c.CorrectExtinguisherSelected ? "pass" : "fail"),
                !correctKnown
                    ? L("Requirement or used type was not recorded", "Gerekli veya kullanılan tip kaydedilmedi")
                    : string.Empty));

            rows.Add(new TrainingResultMetricRowModel(
                L("Fire blanket used", "Yangın battaniyesi kullanıldı"),
                c.FireBlanketUsed ? "pass" : "fail",
                c.FireBlanketUsed
                    ? L("Placed on fire during session", "Oturumda yangına yerleştirildi")
                    : L("Not placed on fire", "Yangına yerleştirilmedi")));

            rows.Add(new TrainingResultMetricRowModel(
                L("Distance / positioning", "Mesafe / konumlama"),
                ScoreTone(t.AvgDistanceScore),
                FormatPercentDetail(t.AvgDistanceScore, L("average distance score", "ortalama mesafe skoru"))));

            rows.Add(new TrainingResultMetricRowModel(
                L("Spray technique (coverage)", "Püskürtme tekniği (kapsama)"),
                ScoreTone(t.AvgCoverageScore),
                FormatPercentDetail(t.AvgCoverageScore, L("average coverage", "ortalama kapsama"))));

            if (t.TotalSprayDurationSeconds <= 0.01f && sweep.ValidHitsTotal <= 0)
            {
                rows.Add(new TrainingResultMetricRowModel(
                    L("Base sweep (horizontal)", "Taban süpürme (yatay)"),
                    "unknown",
                    L("No spray recorded", "Püskürtme kaydı yok")));
            }
            else
            {
                rows.Add(new TrainingResultMetricRowModel(
                    L("Base sweep (horizontal)", "Taban süpürme (yatay)"),
                    ScoreTone(sweep.SweepPerformanceScore),
                    $"{sweep.SweepPerformanceScore:P0}; {L("span", "aralık")} {sweep.SweepCoverageWidth.ToString("F2", CultureInfo.InvariantCulture)} m; {L("streak", "süre")} {sweep.SweepDurationSeconds.ToString("F2", CultureInfo.InvariantCulture)} s"));
            }

            rows.Add(new TrainingResultMetricRowModel(
                L("Capacity usage", "Kapasite kullanımı"),
                CapacityTone(t.NormalizedCapacityUsed, c.FireFullyExtinguished),
                $"{t.NormalizedCapacityUsed.ToString("P0", CultureInfo.InvariantCulture)} {L("of extinguisher capacity used", "söndürücü kapasitesi kullanımı")}"));

            return rows;
        }

        private static string ScoreTone(float score01)
        {
            if (float.IsNaN(score01)) return "unknown";
            if (score01 >= 0.65f) return "pass";
            if (score01 >= 0.35f) return "unknown";
            return "fail";
        }

        private static string CapacityTone(float normalizedUsed, bool fireOut)
        {
            if (float.IsNaN(normalizedUsed)) return "unknown";
            if (!fireOut && normalizedUsed >= 0.9f) return "fail";
            if (fireOut && normalizedUsed <= 0.85f) return "pass";
            return "unknown";
        }

        private static string FormatPercentDetail(float score01, string label)
        {
            if (float.IsNaN(score01)) return L("No data", "Veri yok");
            return $"{score01:P0} {label}";
        }

        private static IReadOnlyList<TrainingResultAdvancedTableRowModel> BuildAdvancedTableRows(SessionReport report)
        {
            var rows = new List<TrainingResultAdvancedTableRowModel>();
            TrainingTechnicalMetrics t = report.Technical;

            rows.Add(new TrainingResultAdvancedTableRowModel(
                L("Aim accuracy", "Nişan doğruluğu"),
                t.TotalEvalTicks > 0
                    ? $"{t.AimAccuracy:P0} ({t.HitTicks} / {t.TotalEvalTicks})"
                    : "—",
                L("≥ 65%", "≥ %65"),
                StatusLabelForScoreThresholds(t.AimAccuracy),
                StatusToneForScoreThresholds(t.AimAccuracy)));

            rows.Add(new TrainingResultAdvancedTableRowModel(
                L("Spray duration", "Püskürtme süresi"),
                FormatDuration(t.TotalSprayDurationSeconds),
                "—",
                L("N/A", "Yok"),
                "neutral"));

            rows.Add(new TrainingResultAdvancedTableRowModel(
                L("Forbidden agent used", "Yasak ajan kullanımı"),
                t.ForbiddenAgentUsed ? L("Yes", "Evet") : L("No", "Hayır"),
                L("No", "Hayır"),
                t.ForbiddenAgentUsed ? L("Fail", "Başarısız") : L("Pass", "Geçti"),
                t.ForbiddenAgentUsed ? "fail" : "pass"));

            rows.Add(new TrainingResultAdvancedTableRowModel(
                L("Composite (technical)", "Bileşik (teknik)"),
                $"{t.CompositeProficiencyScore:P0}",
                L("≥ 65%", "≥ %65"),
                StatusLabelForScoreThresholds(t.CompositeProficiencyScore),
                StatusToneForScoreThresholds(t.CompositeProficiencyScore)));

            AddSweepAdvancedRow(rows, report);

            if (t.MissBreakdown?.Counts != null && t.MissBreakdown.Counts.Count > 0)
            {
                foreach (var kv in t.MissBreakdown.Counts.OrderByDescending(k => k.Value))
                {
                    if (kv.Value <= 0 || kv.Key == SprayMissReason.None)
                        continue;
                    rows.Add(new TrainingResultAdvancedTableRowModel(
                        $"{L("Miss:", "Kaçan:")} {FormatMissReason(kv.Key)}",
                        kv.Value.ToString(CultureInfo.InvariantCulture),
                        L("0 ideal", "0 ideal"),
                        L("Logged", "Kayıtlı"),
                        "neutral"));
                }
            }

            return rows;
        }

        private static void AddSweepAdvancedRow(
            List<TrainingResultAdvancedTableRowModel> rows,
            SessionReport report)
        {
            TrainingSweepMetrics s = report.Sweep;
            TrainingTechnicalMetrics t = report.Technical;
            if (t.TotalSprayDurationSeconds <= 0.01f && s.ValidHitsTotal <= 0)
            {
                rows.Add(new TrainingResultAdvancedTableRowModel(
                    L("Sweep performance", "Süpürme performansı"),
                    "—",
                    "—",
                    L("N/A", "Yok"),
                    "neutral"));
                return;
            }

            float sc = s.SweepPerformanceScore;
            rows.Add(new TrainingResultAdvancedTableRowModel(
                L("Sweep performance", "Süpürme performansı"),
                $"{sc:P0}",
                L("≥ 55%", "≥ %55"),
                StatusLabelForSweepScore(sc),
                StatusToneForSweepScore(sc)));
        }

        private static string StatusLabelForScoreThresholds(float score01)
        {
            if (float.IsNaN(score01)) return L("N/A", "Yok");
            if (score01 >= 0.65f) return L("Pass", "Geçti");
            if (score01 >= 0.35f) return L("Marginal", "Sınırda");
            return L("Below target", "Hedefin altında");
        }

        private static string StatusToneForScoreThresholds(float score01)
        {
            if (float.IsNaN(score01)) return "neutral";
            if (score01 >= 0.65f) return "pass";
            if (score01 >= 0.35f) return "neutral";
            return "fail";
        }

        private static string StatusLabelForSweepScore(float score01)
        {
            if (float.IsNaN(score01)) return L("N/A", "Yok");
            if (score01 >= 0.55f) return L("Pass", "Geçti");
            if (score01 >= 0.35f) return L("Marginal", "Sınırda");
            return L("Below target", "Hedefin altında");
        }

        private static string StatusToneForSweepScore(float score01)
        {
            if (float.IsNaN(score01)) return "neutral";
            if (score01 >= 0.55f) return "pass";
            if (score01 >= 0.35f) return "neutral";
            return "fail";
        }

        private static string FormatMissReason(SprayMissReason reason)
        {
            return reason switch
            {
                SprayMissReason.None                  => L("None", "Yok"),
                SprayMissReason.OutOfRange            => L("Out of range", "Menzil dışı"),
                SprayMissReason.NoFireZoneHit         => L("No fire zone hit", "Yangın bölgesine isabet yok"),
                SprayMissReason.OutsideConeAngle      => L("Outside cone angle", "Koni açısı dışında"),
                SprayMissReason.ZoneAlreadyExtinguished => L("Zone already extinguished", "Bölge zaten sönmüş"),
                SprayMissReason.FireAlreadyExtinguished => L("Fire already extinguished", "Yangın zaten sönmüş"),
                _                                     => reason.ToString(),
            };
        }

        /// <summary>Oturum süresi / ilk tepki gibi rapor alanları için dk + sn (veya yalnızca sn).</summary>
        static string FormatDuration(float seconds)
        {
            if (seconds < 0f || float.IsNaN(seconds))
                return "—";

            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int minutes = totalSeconds / 60;
            int secs = totalSeconds % 60;
            bool isTr = TrainingResultUiLanguage.IsTurkish();

            if (minutes > 0)
            {
                if (secs > 0)
                    return isTr ? $"{minutes} dk {secs} sn" : $"{minutes} min {secs} s";
                return isTr ? $"{minutes} dk" : $"{minutes} min";
            }

            return isTr ? $"{secs} sn" : $"{secs} s";
        }
    }
}
