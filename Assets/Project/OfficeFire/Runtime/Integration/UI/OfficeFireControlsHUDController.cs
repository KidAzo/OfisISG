using UnityEngine;
using UnityEngine.UIElements;
using Woi.InputSystem;
using WOI.Modules.SDK;

namespace Woi.OfficeFire
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Controls HUD Controller")]
    public sealed class OfficeFireControlsHUDController : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private Label _titleLbl;
        private Label _locomotionLbl;
        private Label _equipLbl;
        private Label _pinLbl;
        private Label _dropLbl;
        private Label _leanLbl;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            BindLabels();
        }

        private void OnEnable()
        {
            if (!IsPcPlatform())
            {
                gameObject.SetActive(false);
                return;
            }

            RefreshLanguage();
        }

        private void Start()
        {
            RefreshLanguage();
        }

        public void SetHudVisible(bool visible)
        {
            if (!IsPcPlatform())
            {
                if (gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            gameObject.SetActive(visible);
            if (visible)
            {
                RefreshLanguage();
            }
        }

        public void RefreshLanguage()
        {
            bool turkish = ResolveTurkish();

            if (_titleLbl != null)
            {
                _titleLbl.text = turkish ? "TUŞ ATAMALARI" : "KEY BINDINGS";
            }

            if (_locomotionLbl != null)
            {
                _locomotionLbl.text = turkish ? "Hareket (WASD)" : "Locomotion (WASD)";
            }

            if (_equipLbl != null)
            {
                _equipLbl.text = turkish ? "Kuşan (E)" : "Equip (E)";
            }

            if (_pinLbl != null)
            {
                _pinLbl.text = turkish ? "Pimi Çek (R)" : "Pin Pulling (R)";
            }

            if (_dropLbl != null)
            {
                _dropLbl.text = turkish ? "Bırak (G)" : "Drop (G)";
            }

            if (_leanLbl != null)
            {
                _leanLbl.text = turkish ? "Eğilme (CTRL)" : "Crouch (CTRL)";
            }
        }

        public static void SetVisibleForPc(bool visible)
        {
            if (!IsPcPlatform())
            {
                return;
            }

            OfficeFireControlsHUDController hud = FindAnyObjectByType<OfficeFireControlsHUDController>(
                FindObjectsInactive.Include);
            if (hud != null)
            {
                hud.SetHudVisible(visible);
            }
        }

        private void BindLabels()
        {
            VisualElement root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
            if (root == null)
            {
                return;
            }

            _titleLbl = root.Q<Label>("title-lbl");
            _locomotionLbl = root.Q<Label>("locomotion-lbl");
            _equipLbl = root.Q<Label>("equip-lbl");
            _pinLbl = root.Q<Label>("pin-lbl");
            _dropLbl = root.Q<Label>("drop-lbl");
            _leanLbl = root.Q<Label>("lean-lbl");
        }

        private static bool IsPcPlatform()
        {
            return FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsPC;
        }

        private static bool ResolveTurkish() => OfficeFireSessionLanguage.UseTurkish();
    }
}
