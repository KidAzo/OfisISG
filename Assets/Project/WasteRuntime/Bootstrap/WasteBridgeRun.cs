using System;
using System.Collections.Generic;
using FireExtinguisher.Core;
using UnityEngine;
using Woi.DataHandler;
using Woi.Events.Data;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// One Waste training started by WOI VR Bridge after the player pressed Start. Manual WasteLogin runs never
    /// set it, and while it is active the participant identity comes only from the authorized Bridge session.
    /// </summary>
    public static class WasteBridgeRun
    {
        public static bool IsActive { get; private set; }
        public static string AuthorizationId { get; private set; } = string.Empty;
        public static string SessionId { get; private set; } = string.Empty;
        public static string ModuleId { get; private set; } = string.Empty;
        public static string ParticipantName { get; private set; } = string.Empty;
        public static string PersonnelId { get; private set; } = string.Empty;

        /// <summary>Raised by the result screen once the final result of the active run is stable.</summary>
        public static event Action<WasteRunResult> ResultsFinalized;

        public static void Begin(
            string authorizationId,
            string sessionId,
            string moduleId,
            string participantName,
            string personnelId)
        {
            Clear();

            AuthorizationId = Normalize(authorizationId);
            SessionId = Normalize(sessionId);
            ModuleId = Normalize(moduleId);
            ParticipantName = Normalize(participantName);
            PersonnelId = Normalize(personnelId);
            IsActive = true;

            SessionManager.AutoTestSessionSuppressed = true;
            FindSessionManager()?.ClearSession();

            string languageCode = SessionLanguageState.HasUserChoice
                ? SessionLanguageState.LanguageCode
                : WasteLoginSession.LanguageCode;
            WasteLoginSession.Set(ParticipantName, PersonnelId, languageCode);
            GameSessionData.Set(new List<FireClass>(), ParticipantName, PersonnelId);
        }

        public static void PublishResults(WasteRunResult result)
        {
            if (!IsActive || result == null)
                return;

            ResultsFinalized?.Invoke(result);
        }

        /// <summary>
        /// Ends the active run and drops its identity. Does nothing for manual runs, so a manual login is never wiped.
        /// </summary>
        public static void Clear()
        {
            if (!IsActive)
                return;

            IsActive = false;
            AuthorizationId = string.Empty;
            SessionId = string.Empty;
            ModuleId = string.Empty;
            ParticipantName = string.Empty;
            PersonnelId = string.Empty;

            SessionManager.AutoTestSessionSuppressed = false;
            WasteLoginSession.Clear();
            GameSessionData.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsActive = false;
            AuthorizationId = string.Empty;
            SessionId = string.Empty;
            ModuleId = string.Empty;
            ParticipantName = string.Empty;
            PersonnelId = string.Empty;
            ResultsFinalized = null;
        }

        private static SessionManager FindSessionManager()
        {
            // SessionManager.Instance creates a new manager when none exists; a Bridge run must not.
            SessionManager manager = SessionManager.TryGetInstance();
            return manager != null
                ? manager
                : UnityEngine.Object.FindFirstObjectByType<SessionManager>(FindObjectsInactive.Include);
        }

        private static string Normalize(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
