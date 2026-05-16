using UnityEngine;
using UnityEngine.UIElements;

namespace Woi.Game.Training.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class ControlsHUDController : MonoBehaviour
    {
        UIDocument _uiDocument;
        
        private Label _titleLbl;
        private Label _locomotionLbl;
        private Label _equipLbl;
        private Label _pinLbl;
        private Label _dropLbl;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            var root = _uiDocument.rootVisualElement;

            if (root != null)
            {
                _titleLbl = root.Q<Label>("title-lbl");
                _locomotionLbl = root.Q<Label>("locomotion-lbl");
                _equipLbl = root.Q<Label>("equip-lbl");
                _pinLbl = root.Q<Label>("pin-lbl");
                _dropLbl = root.Q<Label>("drop-lbl");
            }
        }

        private void Start()
        {
            RefreshLanguage();
        }

        /// <summary>Oturum bittiğinde (PC sonuç ekranı) gizlemek için; yeni oturumda tekrar <c>true</c>.</summary>
        public void SetHudVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (visible)
                RefreshLanguage();
        }

        public void RefreshLanguage()
        {
            bool isTurkish = TrainingResultUiLanguage.IsTurkish();

            if (_titleLbl != null) _titleLbl.text = isTurkish ? "TUŞ ATAMALARI" : "KEY BINDINGS";
            if (_locomotionLbl != null) _locomotionLbl.text = isTurkish ? "Hareket (WASD)" : "Locomotion (WASD)";
            if (_equipLbl != null) _equipLbl.text = isTurkish ? "Kuşan (E)" : "Equip (E)";
            if (_pinLbl != null) _pinLbl.text = isTurkish ? "Pimi Çek (R)" : "Pin Pulling (R)";
            if (_dropLbl != null) _dropLbl.text = isTurkish ? "Bırak (G)" : "Drop (G)";
        }
    }
}
