using UnityEngine;

namespace Woi.DataHandler
{
    /// <summary>
    /// Listens to <see cref="SessionManager"/> only. Does not hide, move, or configure the host object —
    /// scene/prefab and <see cref="SessionGameplayGate"/> / <see cref="SessionProfileOverlayController"/> own that.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SessionProfileWorldUiPresenter : MonoBehaviour
    {
        private SessionManager sessionManager;

        private void OnEnable()
        {
            BindSessionManager();
            if (sessionManager != null)
                sessionManager.OnSessionReady += OnSessionReady;
        }

        private void OnDisable()
        {
            if (sessionManager != null)
                sessionManager.OnSessionReady -= OnSessionReady;
        }

        private void BindSessionManager()
        {
            if (sessionManager != null)
                return;

            sessionManager = SessionManager.Instance;
            if (sessionManager == null)
                sessionManager = FindFirstObjectByType<SessionManager>();
        }

        private void OnSessionReady(PlayerSession session)
        {
            if (session == null || !session.IsActive)
                return;
        }

        // Legacy API — intentionally no-op so nothing drives this object from outside session flow.
        public void SetFollowActive(bool active, bool parkHiddenPanel = true) { }

        public void EnsureVrWorldSpaceConfigured() { }

        public void RefreshVrWorldSpaceLayout() { }

        public void InvalidateHeadTrackingCache() { }

        public void RequestSnapToHead() { }

        public bool IsSnappedToHead => false;

        public void SetScreenOverlayActive(bool active) { }

        public void NotifyContentLayoutChanged() { }
    }
}
