using System;
using System.Collections.Generic;
using System.Globalization;
using Woi.VrBridge.Client.Protocol.Messages;

namespace Woi.WasteCollectionMode.VrBridge
{
    /// <summary>
    /// Maps a finalized Waste run onto the generic Protocol V2 result body. The score is the Waste success
    /// percentage (0–100) already used by the CSV and the leaderboard: the V2 score string carries no scale of its
    /// own, and the Hub submitModuleRunResult score is 0–100. "scoreScale" states the scale for readers.
    /// </summary>
    public static class WasteBridgeResultMapper
    {
        public const string ModuleId = WasteModuleBootstrap.CanonicalModuleId;
        public const string OutcomePass = "pass";
        public const string OutcomeFail = "fail";
        public const string ScoreScale = "percent";

        public static string StableResultId(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("A Bridge sessionId is required.", nameof(sessionId));

            return "waste-result-" + sessionId.Trim();
        }

        public static ResultBodyPayload Map(WasteRunResult result, string authorizationId, int? durationSeconds)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            var attributes = new Dictionary<string, string>
            {
                ["correct"] = Invariant(result.Correct),
                ["incorrect"] = Invariant(result.Incorrect),
                ["total"] = Invariant(result.Total),
                ["notFound"] = Invariant(result.NotFound),
                ["scoreScale"] = ScoreScale
            };

            if (!string.IsNullOrWhiteSpace(authorizationId))
                attributes["authorizationId"] = authorizationId.Trim();

            return new ResultBodyPayload
            {
                Outcome = result.Passed ? OutcomePass : OutcomeFail,
                Score = Invariant(result.ScorePercent),
                DurationSeconds = durationSeconds.HasValue ? Math.Max(0, durationSeconds.Value) : (int?)null,
                Attributes = attributes
            };
        }

        static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
