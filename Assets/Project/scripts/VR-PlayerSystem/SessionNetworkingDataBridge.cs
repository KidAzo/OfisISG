using System.Collections.Generic;
using FireExtinguisher.Core;
using UnityEngine;
using Woi.Events.Data;
using Woi.WasteCollectionMode;

namespace Woi.DataHandler
{
    /// <summary>
    /// Writes VR/PC server session (Name|ID) into <see cref="SessionDataSO"/> and <see cref="GameSessionData"/>
    /// so gameplay systems (e.g. waste intro audio) can react via <see cref="SessionDataSO.OnSessionUpdated"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SessionNetworkingDataBridge : MonoBehaviour
    {
        public const string DefaultSessionDataAssetPath =
            "Packages/com.woi.module.fire/Runtime/Events/Data/SessionData.asset";

        [SerializeField] private SessionDataSO sessionData;
        [SerializeField] private SessionManager sessionManager;

        private void Awake()
        {
            if (sessionManager == null)
                sessionManager = GetComponent<SessionManager>();
        }

        private void OnEnable()
        {
            if (sessionManager != null)
                sessionManager.OnSessionReady += OnSessionReady;
        }

        private void OnDisable()
        {
            if (sessionManager != null)
                sessionManager.OnSessionReady -= OnSessionReady;
        }

        private void OnSessionReady(PlayerSession session)
        {
            if (session == null || !session.IsActive)
                return;

            PushSession(session);
        }

        private void PushSession(PlayerSession session)
        {
            string name = string.IsNullOrWhiteSpace(session.PlayerName)
                ? string.Empty
                : session.PlayerName.Trim();
            string id = session.PlayerID > 0
                ? session.PlayerID.ToString()
                : string.Empty;

            GameSessionData.Set(new List<FireClass>(), name, id, SessionLanguageState.LanguageCode);
            SessionProfileLanguagePreference.ReapplyToGame();

            string languageCode = SessionLanguageState.HasUserChoice
                ? SessionLanguageState.LanguageCode
                : WasteLoginSession.LanguageCode;
            WasteLoginSession.Set(name, id, languageCode);

            if (sessionData == null)
                return;

            sessionData.UserName = name;
            sessionData.UserId = id;
            sessionData.NotifyUpdated();
        }
    }
}
