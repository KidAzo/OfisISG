using System;

namespace Woi.PopUpSystem
{
    public interface IPopup
    {
        void Show();
        void Hide();
        void SetTitle(string title);
        void SetMessage(string message);
        void OnConfirm(Action callback);
        void OnCancel(Action callback);
    }
}