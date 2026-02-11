using UnityEngine;
using UnityEngine.UI;
using PrimeTween;

namespace Woi.Feedback
{
    public class CrosshairFeedbacker : MonoBehaviour
    {
        [SerializeField] private Image crosshairImage;

        [Header("Feedback")]
        [SerializeField] private Color feedbackColor = Color.white;
        [SerializeField] private float feedbackDuration = 0.2f;
        [SerializeField] private float scaleMultiplier = 1.15f;

        private Color defaultColor;
        private Vector3 defaultScale;

        private Sequence currentSeq;

        void Awake()
        {
            defaultColor = crosshairImage.color;
            defaultScale = crosshairImage.rectTransform.localScale;
        }

        public void PlayFeedback()
        {
            currentSeq.Stop();

            crosshairImage.color = defaultColor;
            crosshairImage.rectTransform.localScale = defaultScale;

            float half = feedbackDuration * 0.5f;

            // color: default -> feedback -> default
            // scale: 1 -> 1.15 -> 1
            currentSeq = Sequence.Create()
                .Group(Tween.Color(crosshairImage, feedbackColor, half))
                .Group(Tween.Scale(crosshairImage.rectTransform, defaultScale * scaleMultiplier, half))
                .Chain(Tween.Delay(0f))
                .Group(Tween.Color(crosshairImage, defaultColor, half))
                .Group(Tween.Scale(crosshairImage.rectTransform, defaultScale, half));
        }

        void OnDisable()
        {
            currentSeq.Stop();

            if (crosshairImage != null)
            {
                crosshairImage.color = defaultColor;
                crosshairImage.rectTransform.localScale = defaultScale;
            }
        }
    }
}

