using Obvious.Soap;
using UnityEngine;
using UnityEngine.UI;

namespace Woi.HazardSystem.UI
{
    public class HazardCounterUIController : MonoBehaviour
    {
        [SerializeField] TMPro.TextMeshProUGUI hazardCountText;
        [SerializeField] Slider hazardProgressSlider;
        int foundHazards;
        const int totalHazards = 40;

        void Start()
        {
            foundHazards = 0;
            hazardCountText.text = $"{foundHazards}";
            SetProgress();  
        }

        public void SetCounter()
        {
            hazardCountText.text = $"{++foundHazards}";
            SetProgress();
        }

        void SetProgress()
        {
            float progress = foundHazards / (float)totalHazards;
            hazardProgressSlider.value = progress;
        }
    }
}

