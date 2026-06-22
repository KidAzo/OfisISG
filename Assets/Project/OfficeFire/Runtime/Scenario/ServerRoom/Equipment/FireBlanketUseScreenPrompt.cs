using UnityEngine;
using UnityEngine.UI;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Simple screen-space hint shown while the player can place a fire blanket.
    /// </summary>
    internal sealed class FireBlanketUseScreenPrompt
    {
        private readonly MonoBehaviour _host;
        private Canvas _canvas;
        private Text _label;
        private Shadow _shadow;
        private bool _isVisible;

        public FireBlanketUseScreenPrompt(MonoBehaviour host)
        {
            _host = host;
        }

        public void SetText(string english, string turkish, bool preferTurkish)
        {
            EnsureUi();
            if (_label == null)
            {
                return;
            }

            bool useTurkish = OfficeFireSessionLanguage.UseTurkish();
            if (useTurkish && !string.IsNullOrWhiteSpace(turkish))
            {
                _label.text = turkish;
                return;
            }

            _label.text = !string.IsNullOrWhiteSpace(english) ? english : turkish;
        }

        public void SetVisible(bool visible)
        {
            if (_isVisible == visible)
            {
                return;
            }

            _isVisible = visible;
            EnsureUi();
            if (_canvas != null)
            {
                _canvas.enabled = visible;
            }
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void EnsureUi()
        {
            if (_canvas != null)
            {
                return;
            }

            if (_host == null)
            {
                return;
            }

            GameObject root = new GameObject("FireBlanketUseScreenPrompt");
            if (_host != null)
            {
                root.transform.SetParent(_host.transform, false);
            }

            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;
            _canvas.enabled = false;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            root.AddComponent<GraphicRaycaster>();

            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);

            _label = labelObject.AddComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 28;
            _label.fontStyle = FontStyle.Bold;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.white;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;

            _shadow = labelObject.AddComponent<Shadow>();
            _shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            _shadow.effectDistance = new Vector2(1.5f, -1.5f);

            RectTransform rect = _label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.12f);
            rect.anchorMax = new Vector2(0.5f, 0.12f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 72f);
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
