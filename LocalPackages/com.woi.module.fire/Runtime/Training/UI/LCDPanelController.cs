using UnityEngine;
using TMPro;
using Woi.Game.Training.UI;

namespace Woi.Training.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public class LCDPanelController : MonoBehaviour
    {
        [Tooltip("The text component to display the welcome message.")]
        [SerializeField] private TMP_Text _lcdText;

        private bool _wasTurkish;
        private const string TurkishText = "YANGIN EGITIMINE HOSGELDINIZ";
        private const string EnglishText = "WELCOME FIRE TRAINING SIMULATOR";

        private void Awake()
        {
            if (_lcdText == null)
            {
                _lcdText = GetComponent<TMP_Text>();
            }
        }

        private void Start()
        {
            RefreshText();
        }

        public void RefreshText()
        {
            if (_lcdText == null)
                return;

            _wasTurkish = TrainingResultUiLanguage.IsTurkish();
            _lcdText.text = _wasTurkish ? TurkishText : EnglishText;
        }
    }
}
