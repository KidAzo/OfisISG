using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrimeTween;
using Woi.Feedback;

namespace Woi.Feedback
{
    public class CenterFeedbackPopup : MonoBehaviour {
        [Header("Refs")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image iconImage;

        [Header("Feedback Pool")]
        [SerializeField] private CenterFeedbackSO[] feedbacks;

        [Header("Timing")]
        [SerializeField] private float inDuration = 0.12f;
        [SerializeField] private float holdDuration = 0.10f;
        [SerializeField] private float outDuration = 0.12f;

        [Header("Scale")]
        [SerializeField] private float peakScale = 1f;

        private Sequence currentSeq;

        void Awake() {
            SetVisible(false);
            transform.localScale = Vector3.zero;
        }

        public void PlayRandom() {
            if (feedbacks == null || feedbacks.Length == 0)
                return;

            Play(SelectWeighted(feedbacks));
        }

        public void Play(CenterFeedbackSO data) {
            if (data == null) return;

            // cancel + restart
            currentSeq.Stop();

            // apply visuals
            label.text = data.text;
            label.color = data.color;

            if (iconImage != null) {
                bool hasIcon = data.icon != null;
                iconImage.gameObject.SetActive(hasIcon);
                iconImage.sprite = data.icon;
                iconImage.color = data.color; // istersen ayrı renk alanı ekleriz
            }

            SetVisible(true);
            transform.localScale = Vector3.zero;

            currentSeq = Sequence.Create()
                .Chain(Tween.Scale(transform, Vector3.one * peakScale, inDuration))
                .Chain(Tween.Delay(holdDuration))
                .Chain(Tween.Scale(transform, Vector3.zero, outDuration))
                .OnComplete(() => SetVisible(false));
        }

        void OnDisable() {
            currentSeq.Stop();
            transform.localScale = Vector3.zero;
            SetVisible(false);
        }

        void SetVisible(bool visible) {
            if (label != null) label.gameObject.SetActive(visible);
            if (iconImage != null) iconImage.transform.parent.gameObject.SetActive(visible); 
            // Eğer label+icon aynı parent altındaysa daha temiz:
            // gameObject.SetActive(visible);
        }

        static CenterFeedbackSO SelectWeighted(CenterFeedbackSO[] list) {
            float total = 0f;
            for (int i = 0; i < list.Length; i++)
                total += Mathf.Max(0f, list[i] ? list[i].weight : 0f);

            if (total <= 0f) return list[Random.Range(0, list.Length)];

            float roll = Random.value * total;
            for (int i = 0; i < list.Length; i++) {
                var it = list[i];
                float w = Mathf.Max(0f, it ? it.weight : 0f);
                roll -= w;
                if (roll <= 0f) return it;
            }
            return list[list.Length - 1];
        }
    }
}

