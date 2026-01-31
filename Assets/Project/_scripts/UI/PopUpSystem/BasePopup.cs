using System;
using UnityEngine;

namespace Woi.PopUpSystem
{
    public abstract class BasePopup : MonoBehaviour, IPopup
    {
        protected string title;
        protected string message;
        protected bool isHazard;
        protected Action onConfirmCallback;
        protected Action onCancelCallback;

        public abstract void Show();
        public abstract void Hide();
    
        public virtual void SetTitle(string title) => this.title = title;
        public virtual void SetMessage(string message) => this.message = message;
        public virtual void OnConfirm(Action callback) => onConfirmCallback = callback;
        public virtual void OnCancel(Action callback) => onCancelCallback = callback;
    }
}