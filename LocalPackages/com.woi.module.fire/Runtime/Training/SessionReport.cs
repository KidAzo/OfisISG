using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FireExtinguisher.Core;

namespace Woi.Game.Training
{
    /// <summary>
    /// Result of training rule checks (pass/fail and debrief). Populated by your scenario layer
    /// via <see cref="TrainingSessionEndContext"/>; the recorder defaults to pending until you supply it.
    /// </summary>
    public sealed class TrainingRuleOutcome
    {
        public bool WasEvaluated { get; }

        public bool Passed { get; }

        public IReadOnlyList<string> FailureReasons { get; }

        public TrainingRuleOutcome(bool wasEvaluated, bool passed, IReadOnlyList<string> failureReasons)
        {
            WasEvaluated   = wasEvaluated;
            Passed         = passed;
            FailureReasons = failureReasons ?? Array.Empty<string>();
        }

        public static TrainingRuleOutcome Pending()
            => new TrainingRuleOutcome(wasEvaluated: false, passed: true, failureReasons: Array.Empty<string>());

        public static TrainingRuleOutcome Succeeded(IReadOnlyList<string> notes = null)
            => new TrainingRuleOutcome(wasEvaluated: true, passed: true, failureReasons: notes ?? Array.Empty<string>());

        public static TrainingRuleOutcome Failed(IReadOnlyList<string> failureReasons)
            => new TrainingRuleOutcome(wasEvaluated: true, passed: false, failureReasons: failureReasons ?? Array.Empty<string>());
    }

    /// <summary>
    /// Scenario data at session start. For full company reporting, prefer <see cref="ForCompanySession"/> or set
    /// fire class and required extinguisher flags explicitly; <see cref="ExtinguisherSessionRecorder"/> can merge inspector defaults.
    /// </summary>
    public sealed class TrainingSessionBeginContext
    {
        public static TrainingSessionBeginContext Empty { get; } = new TrainingSessionBeginContext();

        public string TraineeId { get; }

        public string ScenarioId { get; }

        public string ScenarioDisplayName { get; }

        public bool HasFireClass { get; }

        public FireClass FireClass { get; }

        public bool HasRequiredExtinguisherType { get; }

        public ExtinguisherType RequiredExtinguisherType { get; }

        /// <summary>
        /// Full scenario identity for company CSV / debrief (sets fire class and required extinguisher as specified).
        /// </summary>
        public static TrainingSessionBeginContext ForCompanySession(
            string traineeId,
            string scenarioId,
            string scenarioDisplayName,
            FireClass fireClass,
            ExtinguisherType requiredExtinguisherType)
        {
            return new TrainingSessionBeginContext(
                traineeId: traineeId,
                scenarioId: scenarioId,
                scenarioDisplayName: scenarioDisplayName,
                hasFireClass: true,
                fireClass: fireClass,
                hasRequiredExtinguisherType: true,
                requiredExtinguisherType: requiredExtinguisherType);
        }

        public TrainingSessionBeginContext(
            string traineeId = null,
            string scenarioId = null,
            string scenarioDisplayName = null,
            bool hasFireClass = false,
            FireClass fireClass = default,
            bool hasRequiredExtinguisherType = false,
            ExtinguisherType requiredExtinguisherType = default)
        {
            TraineeId                   = traineeId ?? string.Empty;
            ScenarioId                  = scenarioId ?? string.Empty;
            ScenarioDisplayName         = scenarioDisplayName ?? string.Empty;
            HasFireClass                = hasFireClass;
            FireClass                   = fireClass;
            HasRequiredExtinguisherType = hasRequiredExtinguisherType;
            RequiredExtinguisherType    = requiredExtinguisherType;
        }
    }

    /// <summary>
    /// Outcome at session end. Supply a real <see cref="TrainingRuleOutcome"/> (Succeeded/Failed), not
    /// <see cref="TrainingRuleOutcome.Pending"/>, so <see cref="TrainingClientSummary.RulesEvaluated"/> and pass/fail are authoritative.
    /// </summary>
    public sealed class TrainingSessionEndContext
    {
        public static TrainingSessionEndContext Empty { get; } = new TrainingSessionEndContext();

        public TrainingRuleOutcome RuleOutcome { get; }

        public bool FireFullyExtinguished { get; }

        public bool? ExtinguisherDepletedBeforeCompletion { get; }

        public IReadOnlyList<string> ExtraCriticalMistakes { get; }

        public float? FinalScoreOverride { get; }

        /// <summary>Convenience: explicit rule result plus common outcome fields.</summary>
        public static TrainingSessionEndContext WithOutcome(
            TrainingRuleOutcome ruleOutcome,
            bool fireFullyExtinguished,
            bool? extinguisherDepletedBeforeCompletion = null,
            IReadOnlyList<string> extraCriticalMistakes = null,
            float? finalScoreOverride = null)
        {
            return new TrainingSessionEndContext(
                ruleOutcome,
                fireFullyExtinguished,
                extinguisherDepletedBeforeCompletion,
                extraCriticalMistakes,
                finalScoreOverride);
        }

        public TrainingSessionEndContext(
            TrainingRuleOutcome ruleOutcome = null,
            bool fireFullyExtinguished = false,
            bool? extinguisherDepletedBeforeCompletion = null,
            IReadOnlyList<string> extraCriticalMistakes = null,
            float? finalScoreOverride = null)
        {
            RuleOutcome                          = ruleOutcome ?? TrainingRuleOutcome.Pending();
            FireFullyExtinguished                = fireFullyExtinguished;
            ExtinguisherDepletedBeforeCompletion = extinguisherDepletedBeforeCompletion;
            ExtraCriticalMistakes                = extraCriticalMistakes ?? Array.Empty<string>();
            FinalScoreOverride                   = finalScoreOverride;
        }
    }

    /// <summary>
    /// Company-, trainer-, and trainee-facing outcome row: identity, scenario, fire/extinguisher choices,
    /// pass/fail context, timing, debrief mistakes, and final score. This is the only part that should
    /// drive CSV exports and end-of-session summaries; use <see cref="TrainingTechnicalMetrics"/> for analytics only.
    /// <see cref="RulesEvaluated"/> mirrors whether the session ended with a real <see cref="TrainingRuleOutcome"/> (not pending).
    /// </summary>
    public sealed class TrainingClientSummary
    {
        public string SessionId { get; }

        public string TraineeId { get; }

        public string ScenarioId { get; }

        public string ScenarioDisplayName { get; }

        public string StartedUtcIso8601 { get; }

        public string EndedUtcIso8601 { get; }

        public float SessionDurationSeconds { get; }

        public float TimeToFirstResponseSeconds { get; }

        public bool HasFireClass { get; }

        public FireClass FireClass { get; }

        public bool HasRequiredExtinguisherType { get; }

        public ExtinguisherType RequiredExtinguisherType { get; }

        public bool HasUsedExtinguisherType { get; }

        public ExtinguisherType UsedExtinguisherType { get; }

        public bool CorrectExtinguisherSelected { get; }

        public bool FireFullyExtinguished { get; }

        /// <summary>
        /// Results UI / CSV: when not supplied in <see cref="TrainingSessionEndContext"/>, the recorder sets this to
        /// whether the equipped extinguisher was <b>actually empty</b> at session end (<see cref="FireExtinguisher.Core.ExtinguisherController.IsDepleted"/>).
        /// “Erken tükendi” satırı: Evet = tüp bitmiş (kırmızı), Hayır = tüpte şarj kalmış (yeşil).
        /// The separate debrief line about running out before fires are out uses <see cref="CriticalMistakes"/> logic.
        /// </summary>
        public bool ExtinguisherDepletedBeforeCompletion { get; }

        /// <summary>True when the trainee placed the fire blanket on the assigned fire during this session.</summary>
        public bool FireBlanketUsed { get; }

        public bool? OverallTrainingPassed { get; }

        /// <summary>True when <see cref="TrainingRuleOutcome.WasEvaluated"/> (outcome was not <see cref="TrainingRuleOutcome.Pending"/>).</summary>
        public bool RulesEvaluated { get; }

        public float FinalScore { get; }

        /// <summary>0–1 base horizontal sweep performance (see <see cref="TrainingSweepMetrics.SweepPerformanceScore"/>).</summary>
        public float SweepPerformanceScore { get; }

        public IReadOnlyList<string> CriticalMistakes { get; }

        public TrainingClientSummary(
            string sessionId,
            string traineeId,
            string scenarioId,
            string scenarioDisplayName,
            string startedUtcIso8601,
            string endedUtcIso8601,
            float sessionDurationSeconds,
            float timeToFirstResponseSeconds,
            bool hasFireClass,
            FireClass fireClass,
            bool hasRequiredExtinguisherType,
            ExtinguisherType requiredExtinguisherType,
            bool hasUsedExtinguisherType,
            ExtinguisherType usedExtinguisherType,
            bool correctExtinguisherSelected,
            bool fireFullyExtinguished,
            bool extinguisherDepletedBeforeCompletion,
            bool fireBlanketUsed,
            bool? overallTrainingPassed,
            bool rulesEvaluated,
            float finalScore,
            float sweepPerformanceScore,
            IReadOnlyList<string> criticalMistakes)
        {
            SessionId              = sessionId ?? string.Empty;
            TraineeId              = traineeId ?? string.Empty;
            ScenarioId             = scenarioId ?? string.Empty;
            ScenarioDisplayName    = scenarioDisplayName ?? string.Empty;
            StartedUtcIso8601      = startedUtcIso8601 ?? string.Empty;
            EndedUtcIso8601        = endedUtcIso8601 ?? string.Empty;
            SessionDurationSeconds = Math.Max(0f, sessionDurationSeconds);
            TimeToFirstResponseSeconds = timeToFirstResponseSeconds;
            HasFireClass           = hasFireClass;
            FireClass              = fireClass;
            HasRequiredExtinguisherType = hasRequiredExtinguisherType;
            RequiredExtinguisherType    = requiredExtinguisherType;
            HasUsedExtinguisherType     = hasUsedExtinguisherType;
            UsedExtinguisherType        = usedExtinguisherType;
            CorrectExtinguisherSelected = correctExtinguisherSelected;
            FireFullyExtinguished       = fireFullyExtinguished;
            ExtinguisherDepletedBeforeCompletion = extinguisherDepletedBeforeCompletion;
            FireBlanketUsed             = fireBlanketUsed;
            OverallTrainingPassed       = overallTrainingPassed;
            RulesEvaluated              = rulesEvaluated;
            FinalScore                  = Clamp01(finalScore);
            SweepPerformanceScore       = Clamp01(sweepPerformanceScore);
            CriticalMistakes            = criticalMistakes ?? Array.Empty<string>();
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            return v > 1f ? 1f : v;
        }
    }

    /// <summary>
    /// One row of company-facing data per <see cref="FireSource"/> in the scene when the session ends.
    /// Session-level fields (trainee, pass/fail, score, duration) are repeated on each row.
    /// </summary>
    public sealed class TrainingFireInstanceReport
    {
        public string FireSourceKey { get; }

        public FireClass FireClass { get; }

        /// <summary>Joined human-readable allowed types from <see cref="FireData.AllowedExtinguisherTypes"/>.</summary>
        public string RequiredExtinguishersDisplay { get; }

        public bool HasAllowedExtinguisherTypes { get; }

        /// <summary>
        /// True when at least one spray tick registered a zone hit on this <see cref="FireSource"/> during the session.
        /// Used to separate “never engaged this fire” from “sprayed with the wrong agent”.
        /// </summary>
        public bool HadSprayContactOnThisFire { get; }

        /// <summary>
        /// True when <see cref="HadSprayContactOnThisFire"/> and the equipped extinguisher type was known for those hits
        /// (dominant type in <see cref="DominantUsedExtinguisherTypeOnThisFire"/>).
        /// </summary>
        public bool HasUsedExtinguisherTypeOnThisFire { get; }

        /// <summary>
        /// Dominant extinguisher type by suppression amount applied to this fire (meaningful when <see cref="HasUsedExtinguisherTypeOnThisFire"/>).
        /// </summary>
        public ExtinguisherType DominantUsedExtinguisherTypeOnThisFire { get; }

        /// <summary>
        /// True if there is no allowed-type constraint, or this fire was never hit by spray, or at least one compatible
        /// (<see cref="CompatibilityResult.Effective"/>) spray tick was recorded on this fire.
        /// </summary>
        public bool CorrectExtinguisherSelected { get; }

        public bool FireFullyExtinguished { get; }

        /// <summary>First entry in allowed types (for single-fire <see cref="TrainingClientSummary"/> mirror).</summary>
        public ExtinguisherType RepresentativeRequiredExtinguisherType { get; }

        public TrainingFireInstanceReport(
            string fireSourceKey,
            FireClass fireClass,
            string requiredExtinguishersDisplay,
            bool hasAllowedExtinguisherTypes,
            ExtinguisherType representativeRequiredExtinguisherType,
            bool hadSprayContactOnThisFire,
            bool hasUsedExtinguisherTypeOnThisFire,
            ExtinguisherType dominantUsedExtinguisherTypeOnThisFire,
            bool correctExtinguisherSelected,
            bool fireFullyExtinguished)
        {
            FireSourceKey                 = fireSourceKey ?? string.Empty;
            FireClass                     = fireClass;
            RequiredExtinguishersDisplay  = requiredExtinguishersDisplay ?? string.Empty;
            HasAllowedExtinguisherTypes   = hasAllowedExtinguisherTypes;
            RepresentativeRequiredExtinguisherType = representativeRequiredExtinguisherType;
            HadSprayContactOnThisFire     = hadSprayContactOnThisFire;
            HasUsedExtinguisherTypeOnThisFire = hasUsedExtinguisherTypeOnThisFire;
            DominantUsedExtinguisherTypeOnThisFire = dominantUsedExtinguisherTypeOnThisFire;
            CorrectExtinguisherSelected   = correctExtinguisherSelected;
            FireFullyExtinguished         = fireFullyExtinguished;
        }
    }

    /// <summary>Human-readable labels shared by the recorder and CSV exporter.</summary>
    public static class TrainingReportLabels
    {
        public static string FormatFireClass(FireClass fc) => FormatFireClass(fc, turkishDisplay: false);

        public static string FormatFireClass(FireClass fc, bool turkishDisplay) => turkishDisplay
            ? fc switch
            {
                FireClass.A => "Sınıf A (katı)",
                FireClass.B => "Sınıf B (sıvı)",
                FireClass.C => "Sınıf C (gaz)",
                FireClass.D => "Sınıf D (metal)",
                FireClass.F => "Sınıf F (pişirme yağları)",
                FireClass.E => "Sınıf E (elektrik)",
                _           => $"Sınıf {fc}",
            }
            : fc switch
            {
                FireClass.A => "Class A (solids)",
                FireClass.B => "Class B (liquids)",
                FireClass.C => "Class C (gases)",
                FireClass.D => "Class D (metals)",
                FireClass.F => "Class F (cooking oils)",
                FireClass.E => "Class E (electrical)",
                _           => $"Class {fc}",
            };

        /// <summary>Short card / note title (no material description).</summary>
        public static string FormatFireClassShort(FireClass fc, bool turkishDisplay) => turkishDisplay
            ? fc switch
            {
                FireClass.A => "Sınıf A",
                FireClass.B => "Sınıf B",
                FireClass.C => "Sınıf C",
                FireClass.D => "Sınıf D",
                FireClass.F => "Sınıf F",
                FireClass.E => "Sınıf E",
                _           => $"Sınıf {fc}",
            }
            : fc switch
            {
                FireClass.A => "Class A",
                FireClass.B => "Class B",
                FireClass.C => "Class C",
                FireClass.D => "Class D",
                FireClass.F => "Class F",
                FireClass.E => "Class E",
                _           => $"Class {fc}",
            };

        public static string FormatExtinguisherType(ExtinguisherType type) => FormatExtinguisherType(type, turkishDisplay: false);

        public static string FormatExtinguisherType(ExtinguisherType type, bool turkishDisplay) => turkishDisplay
            ? type switch
            {
                ExtinguisherType.Water       => "Su",
                ExtinguisherType.Foam        => "Köpük (AFFF)",
                ExtinguisherType.DryPowder   => "Kuru toz (ABC)",
                ExtinguisherType.CO2         => "CO₂",
                ExtinguisherType.WetChemical => "Yağlı ortam söndürücü",
                ExtinguisherType.MetalPowder => "Metal yangın söndürme tozu",
                _                            => type.ToString(),
            }
            : type switch
            {
                ExtinguisherType.Water       => "Water",
                ExtinguisherType.Foam        => "Foam (AFFF)",
                ExtinguisherType.DryPowder   => "Dry powder (ABC)",
                ExtinguisherType.CO2         => "CO₂",
                ExtinguisherType.WetChemical => "Wet chemical",
                ExtinguisherType.MetalPowder => "Metal powder",
                _                            => type.ToString(),
            };

        public static string JoinAllowedExtinguishers(ExtinguisherType[] allowed) =>
            JoinAllowedExtinguishers(allowed, turkishDisplay: false);

        public static string JoinAllowedExtinguishers(ExtinguisherType[] allowed, bool turkishDisplay)
        {
            if (allowed == null || allowed.Length == 0)
                return string.Empty;
            return string.Join(" | ", allowed.Select(t => FormatExtinguisherType(t, turkishDisplay)));
        }

        /// <summary>
        /// Maps a joined list that was stored using English <see cref="FormatExtinguisherType(ExtinguisherType)"/> labels
        /// into Turkish for UI when <paramref name="turkishDisplay"/> is true.
        /// </summary>
        public static string LocalizeRequiredExtinguishersDisplay(string joinedEnglish, bool turkishDisplay)
        {
            if (!turkishDisplay || string.IsNullOrEmpty(joinedEnglish))
                return joinedEnglish;

            const string sep = " | ";
            if (joinedEnglish.IndexOf(sep, StringComparison.Ordinal) < 0)
                return MapEnglishExtinguisherLabelToTurkish(joinedEnglish.Trim());

            string[] parts = joinedEnglish.Split(new[] { sep }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = MapEnglishExtinguisherLabelToTurkish(parts[i].Trim());
            return string.Join(sep, parts);
        }

        static string MapEnglishExtinguisherLabelToTurkish(string english) => english switch
        {
            "Water"             => FormatExtinguisherType(ExtinguisherType.Water, true),
            "Foam (AFFF)"       => FormatExtinguisherType(ExtinguisherType.Foam, true),
            "Dry powder (ABC)"  => FormatExtinguisherType(ExtinguisherType.DryPowder, true),
            "CO₂"               => FormatExtinguisherType(ExtinguisherType.CO2, true),
            "Wet chemical"      => FormatExtinguisherType(ExtinguisherType.WetChemical, true),
            "Metal powder"      => FormatExtinguisherType(ExtinguisherType.MetalPowder, true),
            "MetalPowder"       => FormatExtinguisherType(ExtinguisherType.MetalPowder, true),
            _                   => english,
        };
    }

    /// <summary>
    /// Internal-only spray analytics (ticks, aim, coverage, distance, suppression, capacity, misses, composite blend).
    /// Do not export to company CSV or trainee PDFs — use <see cref="TrainingClientSummary"/> and
    /// <see cref="SessionReport.ToCompanyDebriefString"/> for that. Use <see cref="SessionReport.ToTechnicalDiagnosticsString"/>
    /// when engineers need a text dump.
    /// </summary>
    public sealed class TrainingTechnicalMetrics
    {
        public float TotalSprayDurationSeconds { get; }

        public float TimeToFirstSpraySeconds { get; }

        public int TotalEvalTicks { get; }

        public int HitTicks { get; }

        public float AimAccuracy { get; }

        public float AvgCoverageScore { get; }

        public float AvgDistanceScore { get; }

        public float TotalExtinguishAmount { get; }

        public float NormalizedCapacityUsed { get; }

        public bool ForbiddenAgentUsed { get; }

        public TrainingMissBreakdown MissBreakdown { get; }

        public TrainingTechnicalMetrics(
            float totalSprayDurationSeconds,
            float timeToFirstSpraySeconds,
            int   totalEvalTicks,
            int   hitTicks,
            float aimAccuracy,
            float avgCoverageScore,
            float avgDistanceScore,
            float totalExtinguishAmount,
            float normalizedCapacityUsed,
            bool  forbiddenAgentUsed,
            TrainingMissBreakdown missBreakdown)
        {
            TotalSprayDurationSeconds = totalSprayDurationSeconds;
            TimeToFirstSpraySeconds   = timeToFirstSpraySeconds;
            TotalEvalTicks            = totalEvalTicks;
            HitTicks                  = hitTicks;
            AimAccuracy               = aimAccuracy;
            AvgCoverageScore          = avgCoverageScore;
            AvgDistanceScore          = avgDistanceScore;
            TotalExtinguishAmount     = totalExtinguishAmount;
            NormalizedCapacityUsed    = normalizedCapacityUsed;
            ForbiddenAgentUsed        = forbiddenAgentUsed;
            MissBreakdown             = missBreakdown ?? new TrainingMissBreakdown(null);
        }

        public float CompositeProficiencyScore
        {
            get
            {
                float agentComponent = ForbiddenAgentUsed ? 0f : 1f;
                return (AimAccuracy      * 0.40f)
                     + (AvgCoverageScore * 0.30f)
                     + (AvgDistanceScore * 0.20f)
                     + (agentComponent   * 0.10f);
            }
        }

        public void AppendTechnicalText(StringBuilder sb)
        {
            sb.AppendLine($"  Spray duration     : {TotalSprayDurationSeconds:F1}s");
            sb.AppendLine($"  Time to first spray: {(TimeToFirstSpraySeconds >= 0f ? $"{TimeToFirstSpraySeconds:F1}s" : "never")}");
            sb.AppendLine($"  Aim accuracy       : {AimAccuracy:P0}  ({HitTicks}/{TotalEvalTicks} ticks)");
            sb.AppendLine($"  Avg coverage       : {AvgCoverageScore:P0}");
            sb.AppendLine($"  Avg distance       : {AvgDistanceScore:P0}");
            sb.AppendLine($"  Total suppression  : {TotalExtinguishAmount:F3}");
            sb.AppendLine($"  Capacity used      : {NormalizedCapacityUsed:P0}");
            sb.AppendLine($"  Forbidden agent    : {ForbiddenAgentUsed}");
            sb.AppendLine($"  Composite (tech)   : {CompositeProficiencyScore:P0}");

            if (MissBreakdown.Counts.Count > 0)
            {
                sb.AppendLine("  Miss breakdown:");
                foreach (var kv in MissBreakdown.Counts)
                    sb.AppendLine($"    {kv.Key,-28}: {kv.Value}");
            }
        }
    }

    /// <summary>
    /// Full session snapshot: <see cref="Client"/> for company/trainee reporting,
    /// <see cref="Rules"/> for authoritative evaluation, <see cref="Technical"/> for internal analytics only.
    /// </summary>
    public sealed class SessionReport
    {
        public TrainingClientSummary Client { get; }

        public TrainingRuleOutcome Rules { get; }

        public TrainingTechnicalMetrics Technical { get; }

        /// <summary>Horizontal sweep-at-base heuristic (training feedback only).</summary>
        public TrainingSweepMetrics Sweep { get; }

        public IReadOnlyList<TrainingTimelineEvent> Timeline { get; }

        /// <summary>Per-fire breakdown; empty when no <see cref="FireSource"/> was found (CSV uses <see cref="Client"/> only).</summary>
        public IReadOnlyList<TrainingFireInstanceReport> FireInstances { get; }

        public SessionReport(
            TrainingClientSummary        client,
            TrainingRuleOutcome          rules,
            TrainingTechnicalMetrics     technical,
            TrainingSweepMetrics         sweep,
            IReadOnlyList<TrainingFireInstanceReport> fireInstances,
            IReadOnlyList<TrainingTimelineEvent> timeline)
        {
            Client        = client ?? throw new ArgumentNullException(nameof(client));
            Rules         = rules ?? TrainingRuleOutcome.Pending();
            Technical     = technical ?? throw new ArgumentNullException(nameof(technical));
            Sweep         = sweep ?? TrainingSweepMetrics.Empty;
            FireInstances = fireInstances ?? Array.Empty<TrainingFireInstanceReport>();
            Timeline      = timeline ?? Array.Empty<TrainingTimelineEvent>();
        }

        public float FinalScore => Client.FinalScore;

        public float CompositeProficiencyScore => Technical.CompositeProficiencyScore;

        public IReadOnlyDictionary<SprayMissReason, int> MissReasonCounts
            => Technical.MissBreakdown.Counts;

        /// <summary>
        /// Short company/trainee debrief (no ticks, aim, or miss analytics). For engineering detail use
        /// <see cref="ToTechnicalDiagnosticsString"/>.
        /// </summary>
        public override string ToString() => ToCompanyDebriefString();

        /// <summary>
        /// Same content as <see cref="ToString"/>: identity, scenario, outcomes, mistakes, score, and rule-engine status.
        /// </summary>
        public string ToCompanyDebriefString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== TRAINING RESULT ===");
            sb.AppendLine($"  Trainee id           : {Client.TraineeId}");
            sb.AppendLine($"  Session id           : {Client.SessionId}");
            if (!string.IsNullOrEmpty(Client.ScenarioDisplayName))
                sb.AppendLine($"  Scenario             : {Client.ScenarioDisplayName}");
            if (!string.IsNullOrEmpty(Client.ScenarioId))
                sb.AppendLine($"  Scenario id          : {Client.ScenarioId}");
            sb.AppendLine($"  Started (UTC)        : {Client.StartedUtcIso8601}");
            sb.AppendLine($"  Ended (UTC)          : {Client.EndedUtcIso8601}");
            if (FireInstances.Count > 0)
            {
                sb.AppendLine($"  Fires ({FireInstances.Count}):");
                foreach (TrainingFireInstanceReport f in FireInstances)
                {
                    sb.AppendLine($"    [{f.FireSourceKey}] {TrainingReportLabels.FormatFireClass(f.FireClass)} | allowed: {f.RequiredExtinguishersDisplay}");
                    string usedLine = f.HasUsedExtinguisherTypeOnThisFire
                        ? TrainingReportLabels.FormatExtinguisherType(f.DominantUsedExtinguisherTypeOnThisFire)
                        : "—";
                    sb.AppendLine(
                        $"      used (this fire): {usedLine} | correct type: {f.CorrectExtinguisherSelected} | extinguished: {f.FireFullyExtinguished}");
                }
            }
            else
            {
                if (Client.HasFireClass)
                    sb.AppendLine($"  Fire class           : {Client.FireClass}");
                if (Client.HasRequiredExtinguisherType)
                    sb.AppendLine($"  Required extinguisher: {Client.RequiredExtinguisherType}");
            }

            if (Client.HasUsedExtinguisherType)
                sb.AppendLine($"  Used extinguisher    : {TrainingReportLabels.FormatExtinguisherType(Client.UsedExtinguisherType)}");
            sb.AppendLine($"  Correct (session)    : {Client.CorrectExtinguisherSelected}");
            sb.AppendLine($"  All fires out        : {Client.FireFullyExtinguished}");
            sb.AppendLine($"  Depleted before done : {Client.ExtinguisherDepletedBeforeCompletion}");
            sb.AppendLine($"  Fire blanket used    : {Client.FireBlanketUsed}");
            sb.AppendLine($"  Pass / fail          : {FormatNullableBool(Client.OverallTrainingPassed)}");
            sb.AppendLine($"  Rules evaluated      : {Client.RulesEvaluated}");
            if (Client.RulesEvaluated && Rules.FailureReasons.Count > 0)
            {
                sb.AppendLine("  Rule failure reasons:");
                foreach (string r in Rules.FailureReasons)
                    sb.AppendLine($"    - {r}");
            }

            sb.AppendLine($"  Session duration     : {Client.SessionDurationSeconds:F1} s");
            sb.AppendLine($"  Time to 1st response : {(Client.TimeToFirstResponseSeconds >= 0f ? $"{Client.TimeToFirstResponseSeconds:F1} s" : "never")}");
            sb.AppendLine($"  Final score          : {Client.FinalScore:P0}");
            sb.AppendLine($"  Sweep performance    : {Client.SweepPerformanceScore:P0}");
            if (Client.CriticalMistakes.Count > 0)
            {
                sb.AppendLine("  Critical mistakes:");
                foreach (string m in Client.CriticalMistakes)
                    sb.AppendLine($"    - {m}");
            }

            sb.Append("=======================");
            return sb.ToString();
        }

        /// <summary>
        /// Internal diagnostics: spray analytics, evaluator ticks, miss breakdown, session timeline.
        /// </summary>
        public string ToTechnicalDiagnosticsString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== TECHNICAL DIAGNOSTICS (internal) ===");
            Technical.AppendTechnicalText(sb);
            Sweep.AppendSweepText(sb);
            if (Timeline.Count > 0)
            {
                sb.AppendLine("  Timeline:");
                foreach (TrainingTimelineEvent e in Timeline)
                    sb.AppendLine($"    {e.ElapsedSeconds,6:F1}s  {e.Kind,-16} {e.Detail}");
            }

            sb.Append("========================================");
            return sb.ToString();
        }

        private static string FormatNullableBool(bool? v)
            => v.HasValue ? (v.Value ? "Passed" : "Failed") : "Not evaluated";
    }
}
