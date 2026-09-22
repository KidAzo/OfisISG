using TMPro;
using UnityEngine;
using PrimeTween;

namespace Woi.PopUpSystem
{
    public class Popup2D : BasePopup
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;

        Tween _scaleTween;

        float openDuration = 0.1f;

        public override void Show()
        {
            if (titleText != null)
                titleText.text = title;
            if (messageText != null)
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
        }

        public override void Hide()
        {
            _scaleTween.Stop();

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
