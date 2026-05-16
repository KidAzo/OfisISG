using System;
using System.Collections.Generic;

namespace Woi.Game.Training.UI
{
    /// <summary>
    /// View-model for the training results UI. All values are display-ready strings/flags so the screen
    /// does not depend on domain enums or raw <see cref="SessionReport"/> shape.
    /// </summary>
    public sealed class TrainingResultScreenModel
    {
        public TrainingResultHeaderModel Header { get; }

        public IReadOnlyList<TrainingResultFireCardModel> FireCards { get; }

        public IReadOnlyList<TrainingResultMetricRowModel> OverallEvaluation { get; }

        public IReadOnlyList<string> CriticalMistakes { get; }

        public TrainingResultAdvancedModel Advanced { get; }

        public TrainingResultScreenModel(
            TrainingResultHeaderModel header,
            IReadOnlyList<TrainingResultFireCardModel> fireCards,
            IReadOnlyList<TrainingResultMetricRowModel> overallEvaluation,
            IReadOnlyList<string> criticalMistakes,
            TrainingResultAdvancedModel advanced)
        {
            Header             = header ?? throw new ArgumentNullException(nameof(header));
            FireCards          = fireCards ?? Array.Empty<TrainingResultFireCardModel>();
            OverallEvaluation  = overallEvaluation ?? Array.Empty<TrainingResultMetricRowModel>();
            CriticalMistakes   = criticalMistakes ?? Array.Empty<string>();
            Advanced           = advanced ?? TrainingResultAdvancedModel.Empty;
        }
    }

    public sealed class TrainingResultHeaderModel
    {
        public string ScenarioTitle { get; }

        /// <summary>Localized display: e.g. Pass, Fail, Not evaluated.</summary>
        public string ResultLabel { get; }

        /// <summary>CSS modifier: pass | fail | pending</summary>
        public string ResultTone { get; }

        public int FinalScorePercent { get; }

        public string SessionDurationDisplay { get; }

        public string TimeToFirstResponseDisplay { get; }

        public TrainingResultHeaderModel(
            string scenarioTitle,
            string resultLabel,
            string resultTone,
            int finalScorePercent,
            string sessionDurationDisplay,
            string timeToFirstResponseDisplay)
        {
            ScenarioTitle               = scenarioTitle ?? string.Empty;
            ResultLabel                 = resultLabel ?? string.Empty;
            ResultTone                  = string.IsNullOrEmpty(resultTone) ? "pending" : resultTone;
            FinalScorePercent           = finalScorePercent;
            SessionDurationDisplay      = sessionDurationDisplay ?? string.Empty;
            TimeToFirstResponseDisplay  = timeToFirstResponseDisplay ?? string.Empty;
        }
    }

    public sealed class TrainingResultFireCardModel
    {
        public string CardTitle { get; }

        public string FireClassDisplay { get; }

        public string RequiredExtinguisherDisplay { get; }

        public string UsedExtinguisherDisplay { get; }

        public bool CorrectExtinguisherKnown { get; }

        public bool CorrectExtinguisherSelected { get; }

        public bool FireExtinguished { get; }

        /// <summary>
        /// When false, the "Erken tükendi" / depleted row is left blank (—); no spray was recorded on this fire.
        /// </summary>
        public bool DepletionKnown { get; }

        public bool DepletedBeforeCompletion { get; }

        public bool HasTimeToExtinguish { get; }

        public string TimeToExtinguishDisplay { get; }

        public IReadOnlyList<string> KeyMistakes { get; }

        public TrainingResultFireCardModel(
            string cardTitle,
            string fireClassDisplay,
            string requiredExtinguisherDisplay,
            string usedExtinguisherDisplay,
            bool correctExtinguisherKnown,
            bool correctExtinguisherSelected,
            bool fireExtinguished,
            bool depletionKnown,
            bool depletedBeforeCompletion,
            bool hasTimeToExtinguish,
            string timeToExtinguishDisplay,
            IReadOnlyList<string> keyMistakes)
        {
            CardTitle                   = cardTitle ?? string.Empty;
            FireClassDisplay            = fireClassDisplay ?? string.Empty;
            RequiredExtinguisherDisplay = requiredExtinguisherDisplay ?? string.Empty;
            UsedExtinguisherDisplay     = usedExtinguisherDisplay ?? string.Empty;
            CorrectExtinguisherKnown    = correctExtinguisherKnown;
            CorrectExtinguisherSelected = correctExtinguisherSelected;
            FireExtinguished            = fireExtinguished;
            DepletionKnown              = depletionKnown;
            DepletedBeforeCompletion    = depletedBeforeCompletion;
            HasTimeToExtinguish         = hasTimeToExtinguish;
            TimeToExtinguishDisplay     = timeToExtinguishDisplay ?? string.Empty;
            KeyMistakes                 = keyMistakes ?? Array.Empty<string>();
        }
    }

    /// <summary>One row in the overall evaluation panel (icon + label + optional detail).</summary>
    public sealed class TrainingResultMetricRowModel
    {
        public string Title { get; }

        /// <summary>pass | fail | unknown</summary>
        public string StatusTone { get; }

        public string DetailDisplay { get; }

        public TrainingResultMetricRowModel(string title, string statusTone, string detailDisplay)
        {
            Title         = title ?? string.Empty;
            StatusTone    = string.IsNullOrEmpty(statusTone) ? "unknown" : statusTone;
            DetailDisplay = detailDisplay ?? string.Empty;
        }
    }

    /// <summary>One row in the Advanced Details table (metric / recorded / target / status).</summary>
    public sealed class TrainingResultAdvancedTableRowModel
    {
        public string Metric { get; }

        public string RecordedValue { get; }

        public string TargetLimit { get; }

        public string StatusLabel { get; }

        /// <summary>pass | fail | neutral — drives status badge styling.</summary>
        public string StatusTone { get; }

        public TrainingResultAdvancedTableRowModel(
            string metric,
            string recordedValue,
            string targetLimit,
            string statusLabel,
            string statusTone)
        {
            Metric         = metric ?? string.Empty;
            RecordedValue  = recordedValue ?? string.Empty;
            TargetLimit    = targetLimit ?? string.Empty;
            StatusLabel    = statusLabel ?? string.Empty;
            StatusTone     = string.IsNullOrEmpty(statusTone) ? "neutral" : statusTone;
        }
    }

    public sealed class TrainingResultAdvancedModel
    {
        public static TrainingResultAdvancedModel Empty { get; } =
            new TrainingResultAdvancedModel(Array.Empty<TrainingResultAdvancedTableRowModel>());

        public IReadOnlyList<TrainingResultAdvancedTableRowModel> Rows { get; }

        public TrainingResultAdvancedModel(IReadOnlyList<TrainingResultAdvancedTableRowModel> rows)
        {
            Rows = rows ?? Array.Empty<TrainingResultAdvancedTableRowModel>();
        }
    }
}
