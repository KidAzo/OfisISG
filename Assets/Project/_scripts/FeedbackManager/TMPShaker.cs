using TMPro;
using UnityEngine;
using PrimeTween;

namespace Woi.Feedback
{
    public class TMPShaker : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;

        [Header("Shake")]
        [SerializeField] private float shakeDuration = 0.3f;
        [SerializeField] private float shakeStrength = 10f;

        [Header("Scale")]
        [SerializeField] private float scaleMultiplier = 1.1f;

        private Tween shakeTween;
        private Tween scaleTween;

        private Vector3 initialScale;
        private RectTransform cachedRT;

        void Awake()
        {
            if (text == null)
                text = GetComponent<TMP_Text>();

            if (text == null)
            {
                Debug.LogError("TMPShaker: TMP_Text reference missing.", this);
                enabled = false;
                return;
            }

            cachedRT = text.rectTransform;
            initialScale = cachedRT.localScale;
        }

        public void Shake()
        {
            if (!isActiveAndEnabled) return;
            if (shakeTween.isAlive) return;

            shakeTween = Tween.ShakeLocalPosition(
                cachedRT,
                strength: new Vector3(shakeStrength, 0, 0),
                duration: shakeDuration
            );

            scaleTween = Tween.Scale(
                cachedRT,
                initialScale * scaleMultiplier,
                shakeDuration * 0.5f
            )
            .OnComplete(() =>
            {
                Tween.Scale(cachedRT, initialScale, shakeDuration * 0.5f);
            });
        }

        void OnDisable()
        {
            if (shakeTween.isAlive)
                shakeTween.Stop();

            if (scaleTween.isAlive)
                scaleTween.Stop();

            if (cachedRT != null)
                cachedRT.localScale = initialScale;
        }
    }
}