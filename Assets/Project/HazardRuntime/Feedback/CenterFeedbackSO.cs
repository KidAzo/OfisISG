using UnityEngine;

namespace Woi.Feedback
{
    [CreateAssetMenu(menuName = "SO/Feedback/Center Feedback", fileName = "CenterFeedback_")]
    public class CenterFeedbackSO : ScriptableObject {
        public string text = "Good";
        public Color color = Color.white;
        public Sprite icon;

        [Header("Optional weighting")]
        [Min(0f)] public float weight = 1f; // random seçimde ağırlık
    }
}

