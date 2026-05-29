using System.Collections.Generic;

namespace Woi.OfficeFire
{
    public sealed class OfficeFireResultScreenModel
    {
        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public string StatusLabel { get; set; } = string.Empty;

        public bool Passed { get; set; }

        public string ReactionTimeLabel { get; set; } = string.Empty;

        public string ReactionTimeValue { get; set; } = string.Empty;

        public string FireControlledLabel { get; set; } = string.Empty;

        public string FireControlledValue { get; set; } = string.Empty;

        public string EvacuatedLabel { get; set; } = string.Empty;

        public string EvacuatedValue { get; set; } = string.Empty;

        public string CorrectSectionTitle { get; set; } = string.Empty;

        public string MissingSectionTitle { get; set; } = string.Empty;

        public string MistakesSectionTitle { get; set; } = string.Empty;

        public string EmptyCorrectText { get; set; } = string.Empty;

        public string EmptyMissingText { get; set; } = string.Empty;

        public string EmptyMistakesText { get; set; } = string.Empty;

        public string ContinueButtonText { get; set; } = string.Empty;

        public List<string> CompletedObjectives { get; } = new List<string>();

        public List<string> MissingObjectives { get; } = new List<string>();

        public List<string> Mistakes { get; } = new List<string>();
    }
}
