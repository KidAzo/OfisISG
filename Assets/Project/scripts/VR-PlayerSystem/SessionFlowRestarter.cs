using UnityEngine;
using Woi.WasteCollectionMode;

namespace Woi.DataHandler
{
    /// <summary>
    /// Resets persistent session gate state when gameplay restarts (e.g. "Tekrar Başla").
    /// </summary>
    public static class SessionFlowRestarter
    {
        public static void PrepareForNewSession()
        {
            WasteVrLocomotionGate locomotionGate =
                Object.FindFirstObjectByType<WasteVrLocomotionGate>(FindObjectsInactive.Include);
            locomotionGate?.RefreshCachedXrRigRoot();

            SessionGameplayGate gate = Object.FindFirstObjectByType<SessionGameplayGate>(FindObjectsInactive.Include);
            gate?.ResetForNewSession();

            SessionManager manager = SessionManager.Instance;
            if (manager == null)
                manager = Object.FindFirstObjectByType<SessionManager>(FindObjectsInactive.Include);

            manager?.PrepareForRestart();
        }
    }
}
