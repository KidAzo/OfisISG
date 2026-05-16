using System;
using UnityEngine;
using Woi.Events;

namespace Woi.UI.Navigation
{
    /// <summary>
    /// Adapter that exposes <see cref="LoginScreenController"/> as <see cref="ILoginView"/> for consumers that resolve the view by interface.
    /// </summary>
    [RequireComponent(typeof(LoginScreenController))]
    [DisallowMultipleComponent]
    public sealed class PCLoginView : MonoBehaviour, ILoginView
    {
        LoginScreenController _host;

        void Awake()
        {
            _host = GetComponent<LoginScreenController>();
        }

        LoginScreenController Host
        {
            get
            {
                if (_host == null)
                    _host = GetComponent<LoginScreenController>();
                return _host;
            }
        }

        public event Action<string, string> OnLoginRequested
        {
            add => Host.OnLoginRequested += value;
            remove => Host.OnLoginRequested -= value;
        }

        public event Action<OnLogged> OnFireModuleLoginCompleted
        {
            add => Host.OnFireModuleLoginCompleted += value;
            remove => Host.OnFireModuleLoginCompleted -= value;
        }

        public void SetVisible(bool visible) => Host.SetVisible(visible);

        public void SetLoading(bool loading) => Host.SetLoading(loading);

        public void ShowError(string message) => Host.ShowError(message);

        public void ClearError() => Host.ClearError();
    }
}
