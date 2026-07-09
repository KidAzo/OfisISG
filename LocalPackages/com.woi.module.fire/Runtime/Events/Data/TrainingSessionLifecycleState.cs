using System;

namespace Woi.Events.Data
{
    /// <summary>
    /// Cross-assembly training session lifecycle hooks (no training assembly reference required).
    /// </summary>
    public static class TrainingSessionLifecycleState
    {
        public static event Action SessionStarted;

        public static void NotifySessionStarted() => SessionStarted?.Invoke();
    }
}
