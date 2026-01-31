using System;
using TMPro;
using UnityEngine;

namespace Woi.PopUpSystem
{
    public class Popup2D : BasePopup
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        //private NotificationManager notificationManager;

        public override void Show()
        {
            titleText.text = title;
            messageText.text = message;
            
            //notificationManager = GetComponent<NotificationManager>();
            //notificationManager.Open();
        }

        public override void Hide()
        {
            //notificationManager.Close();
        }
    }
}