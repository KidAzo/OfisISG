using System;
using System.Collections.Generic;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Final counts of one Waste run, read from <see cref="WasteCollectTracker"/> when the result screen finalizes.
    /// Passed means at least one classified item and no item put in a wrong bin; uncollected items do not change it.
    /// </summary>
    public sealed class WasteRunResult
    {
        public WasteRunResult(int correct, int incorrect, int notFound)
        {
            Correct = Math.Max(0, correct);
            Incorrect = Math.Max(0, incorrect);
            NotFound = Math.Max(0, notFound);
        }

        public int Correct { get; }
        public int Incorrect { get; }
        public int NotFound { get; }
        public int Total => Correct + Incorrect;
        public int ScorePercent => WasteSessionResultCsvExporter.ComputeSuccessPercent(Correct, Total);
        public bool Passed => Total > 0 && Incorrect == 0;

        public static WasteRunResult Capture(WasteCollectTracker tracker)
        {
            if (tracker == null)
                return new WasteRunResult(0, 0, 0);

            int correct = 0;
            int incorrect = 0;
            IReadOnlyList<WasteClassificationRecord> classifications = tracker.Classifications;
            for (int i = 0; i < classifications.Count; i++)
            {
                WasteClassificationRecord record = classifications[i];
                if (record == null)
                    continue;

                if (record.isCorrect)
                    correct++;
                else
                    incorrect++;
            }

            var uncollected = new List<WasteUncollectedRecord>();
            tracker.GetUncollectedSceneWastes(uncollected);
            return new WasteRunResult(correct, incorrect, uncollected.Count);
        }
    }
}
