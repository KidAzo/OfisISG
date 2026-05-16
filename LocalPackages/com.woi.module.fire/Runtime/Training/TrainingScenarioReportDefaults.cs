using System;
using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.Game.Training
{
    /// <summary>
    /// Inspector-friendly defaults merged into <see cref="TrainingSessionBeginContext"/> when you call
    /// <see cref="ExtinguisherSessionRecorder.BeginSession(string)"/> or pass a partial begin context.
    /// </summary>
    [Serializable]
    public sealed class TrainingScenarioReportDefaults
    {
        [Tooltip("CSV / debrief title when the begin context leaves ScenarioDisplayName empty.")]
        public string ScenarioDisplayName = string.Empty;

        [Tooltip("Scenario id when BeginSession only passes a blank id or you rely on defaults.")]
        public string DefaultScenarioId = string.Empty;

        [Tooltip("Trainee id when the begin context leaves TraineeId empty.")]
        public string DefaultTraineeId = string.Empty;

        [Tooltip("Sets HasFireClass when the begin context does not.")]
        public bool SpecifyFireClass;

        public FireClass FireClass;

        [Tooltip("Sets required extinguisher when the begin context does not.")]
        public bool SpecifyRequiredExtinguisherType;

        public ExtinguisherType RequiredExtinguisherType;
    }
}
