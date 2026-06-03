using UnityEngine;
using Woi.DataHandler;
using Woi.Events.Data;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// VR entry: skip login session defaults. Selection uses <see cref="SelectionSystemManager"/> (trigger + right-controller ray).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteCollectionVrBootstrap : MonoBehaviour
    {
        [Header("VR identity (no login scene)")]
        [SerializeField] private string defaultUserName = "VR Trainee";
        [SerializeField] private string defaultUserId = "vr-trainee";
        [SerializeField] private string defaultLanguageCode = "tr";

        private void Awake()
        {
            if (!WasteCollectionPlatform.ShouldUseVrPresentation())
                return;

            EnsureVrSession();
        }

        private void EnsureVrSession()
        {
            // Office Fire VR: identity + language come from session overlay / UDP — do not seed Turkish here.
            if (FindSessionManager() != null)
                return;

            string languageCode = SessionLanguageState.HasUserChoice
                ? SessionLanguageState.LanguageCode
                : defaultLanguageCode;

            if (WasteLoginSession.IsSet)
            {
                if (SessionLanguageState.HasUserChoice)
                    WasteLoginSession.Set(WasteLoginSession.UserName, WasteLoginSession.UserId, languageCode);

                return;
            }

            WasteLoginSession.Set(defaultUserName, defaultUserId, languageCode);
        }

        private static SessionManager FindSessionManager()
        {
            if (SessionManager.Instance != null)
                return SessionManager.Instance;

            return FindFirstObjectByType<SessionManager>(FindObjectsInactive.Include);
        }
    }
}
