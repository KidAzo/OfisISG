using System;
using UnityEngine;
using WoiUtils.Pooling;

namespace Woi.PopUpSystem
{
    public abstract class BasePopup : MonoBehaviour, IPopup, IPoolable
    {
        protected string title;
        protected string message;
        protected bool isHazard;
        protected float closeDuration;
        protected Action onConfirmCallback;
        protected Action onCancelCallback;

        public abstract void Show();
        public abstract void Hide();
    
        public virtual void SetTitle(string title) => this.title = title;
        public virtual void SetMessage(string message) => this.message = message;
        public virtual void OnConfirm(Action callback) => onConfirmCallback = callback;
        public virtual void OnCancel(Action callback) => onCancelCallback = callback;
        public virtual void SetCloseDuration(float closeDuration) => this.closeDuration = closeDuration;

        public void Get()
        {
            gameObject.SetActive(true);
        }

        public void Release()
        {
            gameObject.SetActive(false);    
        }
    }
}