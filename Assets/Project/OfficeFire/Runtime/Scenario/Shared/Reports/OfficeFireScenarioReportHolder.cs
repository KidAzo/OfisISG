using System.Collections.Generic;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Keeps the scenario report alive across outdoor scene load.
    /// </summary>
    public static class OfficeFireScenarioReportHolder
    {
        private static OfficeFireScenarioReport _stashedReport;

        public static void Stash(OfficeFireScenarioReport report)
        {
            _stashedReport = report == null ? null : Copy(report);
        }

        public static bool TryConsume(out OfficeFireScenarioReport report)
        {
            report = _stashedReport;
            _stashedReport = null;
            return report != null;
        }

        public static void Clear()
        {
            _stashedReport = null;
        }

        private static OfficeFireScenarioReport Copy(OfficeFireScenarioReport source)
        {
            return new OfficeFireScenarioReport
            {
                scenarioId = source.scenarioId,
                reactionTime = source.reactionTime,
                fireControlled = source.fireControlled,
                evacuated = source.evacuated,
                completed = source.completed,
                correctActions = new List<OfficeFireCorrectActionId>(source.correctActions),
                mistakes = new List<OfficeFireMistakeId>(source.mistakes),
            };
        }
    }
}
