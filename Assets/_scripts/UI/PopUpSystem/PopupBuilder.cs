
using UnityEngine;

namespace Woi.PopUpSystem
{
    public class PopupBuilder
    {
        BasePopup popup;

        public BasePopup BuildPopup(BasePopup popup, Vector3 position, string title, string message, System.Action onConfirm, System.Action onCancel)
        {
            return WithPopup(popup)
                .WithPosition(position)
                .WithTitle(title)
                .WithMessage(message)
                .OnConfirm(onConfirm)
                .OnCancel(onCancel)
                .Build();
        }
        
        public PopupBuilder WithPosition(Vector3 position)
        {
            var rect = popup.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            return this;
        }
        
        public PopupBuilder WithPopup(BasePopup popup)
        {
            this.popup = popup;
            return this;
        }
        
        public PopupBuilder WithTitle(string title)
        {
            popup.SetTitle(title);
            return this;
        }

        public PopupBuilder WithMessage(string message)
        {
            popup.SetMessage(message);
            return this;
        }

        public PopupBuilder OnConfirm(System.Action callback)
        {
            popup.OnConfirm(callback);
            return this;
        }

        public PopupBuilder OnCancel(System.Action callback)
        {
            popup.OnCancel(callback);
            return this;
        }

        public BasePopup Build()
        {
            return popup;
        }
    }
}