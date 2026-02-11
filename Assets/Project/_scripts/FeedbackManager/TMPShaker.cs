using TMPro;
using UnityEngine;
using PrimeTween;

namespace Woi.Feedback
{
  using TMPro;
using UnityEngine;
using PrimeTween;

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

    void Awake()
    {
        initialScale = text.rectTransform.localScale;
    }

    public void Shake()
    {
        if (shakeTween.isAlive)
            return;

        var rt = text.rectTransform;

        shakeTween = Tween.ShakeLocalPosition(
            rt,
            strength: new Vector3(shakeStrength, 0, 0),
            duration: shakeDuration
        );

        scaleTween = Tween.Scale(
            rt,
            initialScale * scaleMultiplier,
            shakeDuration * 0.5f
        )
        .OnComplete(() =>
        {
            Tween.Scale(rt, initialScale, shakeDuration * 0.5f);
        });
    }

    void OnDisable()
    {
        shakeTween.Stop();
        scaleTween.Stop();
        text.rectTransform.localScale = initialScale;
    }
}

}
