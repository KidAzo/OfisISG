namespace Woi.Game.Training
{
    /// <summary>
    /// Coarse session timeline marker (not per-evaluation tick — use metrics for aggregates).
    /// </summary>
    public enum TrainingTimelineEventKind
    {
        SessionStarted = 0,
        SessionEnded   = 1,
        SprayStarted   = 2,
        SprayStopped   = 3,
    }

    /// <summary>
    /// Single point on the training session timeline for debrief charts or JSON export.
    /// </summary>
    public readonly struct TrainingTimelineEvent
    {
        /// <summary>Seconds after session start.</summary>
        public float ElapsedSeconds { get; }

        public TrainingTimelineEventKind Kind { get; }

        /// <summary>Optional payload (e.g. equipment id).</summary>
        public string Detail { get; }

        public TrainingTimelineEvent(float elapsedSeconds, TrainingTimelineEventKind kind, string detail = null)
        {
            ElapsedSeconds = elapsedSeconds;
            Kind           = kind;
            Detail         = detail ?? string.Empty;
        }
    }
}
