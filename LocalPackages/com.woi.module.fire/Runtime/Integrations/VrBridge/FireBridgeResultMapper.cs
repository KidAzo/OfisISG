#if WOI_VRBRIDGE_CLIENT
using System;
using System.Collections.Generic;
using Woi.VrBridge.Client.Protocol.Messages;

namespace Woi.Game.Training.VrBridge
{
    /// <summary>
    /// Maps Fire Training <see cref="SessionReport"/> into Protocol V2 result body.
    /// Fire-specific DTOs stay outside the generic Bridge package.
    /// </summary>
    public static class FireBridgeResultMapper
    {
        public static ResultBodyPayload Map(SessionReport report, string authorizationId = null)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var summary = report.Client;
            var outcome = "unknown";
            if (summary != null)
            {
                if (summary.OverallTrainingPassed == true)
                {
                    outcome = "pass";
                }
                else if (summary.OverallTrainingPassed == false)
                {
                    outcome = "fail";
                }
                else if (summary.FireFullyExtinguished)
                {
                    outcome = "completed";
                }
            }

            Dictionary<string, string> attributes = null;
            if (!string.IsNullOrWhiteSpace(authorizationId))
            {
                attributes = new Dictionary<string, string> { ["authorizationId"] = authorizationId };
            }

            return new ResultBodyPayload
            {
                Outcome = outcome,
                Score = summary != null ? summary.FinalScore.ToString("0.###") : null,
                DurationSeconds = summary != null ? (int)Math.Round(summary.SessionDurationSeconds) : (int?)null,
                Attributes = attributes
            };
        }

        public static string StableResultId(string bridgeSessionId, SessionReport report)
        {
            if (!string.IsNullOrWhiteSpace(bridgeSessionId))
            {
                return $"fire-result-{bridgeSessionId}";
            }

            var sessionId = report?.Client?.SessionId;
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                return $"fire-result-{sessionId}";
            }

            return $"fire-result-{Guid.NewGuid():N}";
        }
    }
}
#endif
