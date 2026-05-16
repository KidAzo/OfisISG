using System;
using Woi.Events;

namespace Woi.UI.Navigation
{
    /// <summary>
    /// Shared contract for PC (screen-space UI Toolkit) and future VR (RenderTexture / world-space) login presentation.
    /// Business logic stays in <see cref="LoginScreenController"/>; implementations forward UI state only.
    /// </summary>
    public interface ILoginView
    {
        /// <summary>Reserved for credential-based flows (e.g. future Firebase email/password).</summary>
        event Action<string, string> OnLoginRequested;

        /// <summary>Fired when the user completes the existing fire-module login (mirrors data sent to <see cref="OnLogged"/>).</summary>
        event Action<OnLogged> OnFireModuleLoginCompleted;

        void SetVisible(bool visible);
        void SetLoading(bool loading);
        void ShowError(string message);
        void ClearError();
    }
}
