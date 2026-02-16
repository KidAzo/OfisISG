using System;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using WoiUtils.Pooling;
using PrimeTween;

namespace Woi.PopUpSystem
{
    public class Popup2D : BasePopup
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;

        //private NotificationManager notificationManager;

        Tween _scaleTween;

        float openDuration = 0.1f;
    
        public override void Show()
        {
            titleText.text = title;
            messageText.text = message;
            gameObject.SetActive(true);

            _scaleTween.Stop();

            transform.localScale = Vector3.zero;

                _scaleTween = Tween.Scale(
                    transform,
                    Vector3.one,
                    openDuration,
                    Ease.OutBack
                );

            //notificationManager = GetComponent<NotificationManager>();
            //notificationManager.Open();
        }

        public override void Hide()
        {
                _scaleTween = Tween.Scale(
                transform,
                Vector3.zero,
                closeDuration,
                Ease.InBack
            ).OnComplete(() =>
            {
                gameObject.SetActive(false);
            });
        }
    }
}