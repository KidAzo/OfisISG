using System;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using WoiUtils.Pooling;

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
            gameObject.SetActive(true); 
            
            //notificationManager = GetComponent<NotificationManager>();
            //notificationManager.Open();
        }

        public override void Hide()
        {
            gameObject.SetActive(false);  
            //notificationManager.Close();
        }
    }
}