using System;
using FireExtinguisher.Core;
using TMPro;
using UnityEngine;
using WOI.Modules.SDK;
using Woi.UI.Popups.Localization;

namespace Woi.Game.Training.FireSelection
{
    /// <summary>
    /// LCD <see cref="TMP_Text"/> için parent <see cref="TrainingFireSelectionState"/> (veya <see cref="FireSource"/>)
    /// üzerinden sınıf okur. Maskede değilken <see cref="_unselectedText"/>.
    /// Seçiliyken önce <see cref="_textEn"/> / <see cref="_textTr"/> (aktif dile göre), ikisi de boşsa <see cref="_selectedFormat"/>.
    /// </summary>
    [AddComponentMenu("WOI/Training/Training Fire Source LCD Label")]
    public sealed class TrainingFireSourceLcdLabel : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField]
        private TMP_Text _label;

        [Tooltip("If null, resolved via GetComponentInParent (LCD should be under the fire root).")]
        [SerializeField]
        private TrainingFireSelectionState _selectionState;

        [Header("Copy")]
        [Tooltip("Shown when this fire is not in the active training mask (default: blank LCD).")]
        [SerializeField]
        private string _unselectedText = "";

        [Header("Selected — English / Turkish")]
        [Tooltip("LocalizationService / ILocalizationService dili en iken kullanılır. {0} = sınıf harfi (A, B, …). Boşsa TR veya Fallback devreye girer.")]
        [SerializeField]
        private string _textEn = "";

        [Tooltip("Dil tr iken kullanılır. {0} = sınıf harfi. Boşsa EN veya Fallback.")]
        [SerializeField]
        private string _textTr = "";

        [Header("Fallback format")]
        [Tooltip("EN ve TR metinleri boşken kullanılır. {0} = sınıf harfi.")]
        [SerializeField]
        private string _selectedFormat = "CLASS {0} FIRE";

        [SerializeField]
        private bool _forceUppercase = true;

        private string _lastObservedLanguageCode = "\u0001";

        private void Reset()
        {
            _label = GetComponent<TMP_Text>();
        }

        private void Awake()
        {
            if (_label == null)
                _label = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            ResolveSelectionState();
            if (_selectionState != null)
            {
                _selectionState.OnSelected.AddListener(OnSelectionOrMaskChanged);
                _selectionState.OnNotSelected.AddListener(OnSelectionOrMaskChanged);
            }

            CacheLanguage();
            RefreshLabel();
        }

        private void OnDisable()
        {
            if (_selectionState != null)
            {
                _selectionState.OnSelected.RemoveListener(OnSelectionOrMaskChanged);
                _selectionState.OnNotSelected.RemoveListener(OnSelectionOrMaskChanged);
            }
        }

        private void LateUpdate()
        {
            if (!UsesPerLanguageSelectedCopy)
                return;

            string now = ResolveCurrentLanguageCode();
            if (!string.Equals(now, _lastObservedLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                _lastObservedLanguageCode = now;
                RefreshLabel();
            }
        }

        bool UsesPerLanguageSelectedCopy =>
            !string.IsNullOrEmpty(_textEn) || !string.IsNullOrEmpty(_textTr);

        private void OnSelectionOrMaskChanged() => RefreshLabel();

        void CacheLanguage() => _lastObservedLanguageCode = ResolveCurrentLanguageCode();

        static string ResolveCurrentLanguageCode()
        {
            if (ServiceLocator.TryGet<ILocalizationService>(out ILocalizationService iloc) && iloc != null && !string.IsNullOrEmpty(iloc.CurrentLanguage))
                return iloc.CurrentLanguage.Trim().ToLowerInvariant();

            if (LocalizationService.Instance != null && !string.IsNullOrEmpty(LocalizationService.Instance.CurrentLanguage))
                return LocalizationService.Instance.CurrentLanguage.Trim().ToLowerInvariant();

            return string.Empty;
        }

        static bool IsTurkishLanguage()
        {
            string code = ResolveCurrentLanguageCode();
            if (string.IsNullOrEmpty(code))
                return true;

            return code == LocalizationService.Turkish || code.StartsWith("tr", StringComparison.Ordinal);
        }

        private void ResolveSelectionState()
        {
            if (_selectionState != null)
                return;

            _selectionState = GetComponentInParent<TrainingFireSelectionState>();
        }

        /// <summary>Re-reads selection, language, and fire class; call after changing mask or hierarchy at runtime.</summary>
        public void RefreshLabel()
        {
            if (_label == null)
                return;

            ResolveSelectionState();

            bool included = _selectionState == null || _selectionState.IsCurrentlySelected;
            string line = included ? BuildOpenLine() : _unselectedText;

            if (_forceUppercase && !string.IsNullOrEmpty(line))
                line = line.ToUpperInvariant();

            _label.text = line;
        }

        string BuildOpenLine()
        {
            FireClass fc = ResolveFireClass();
            string letter = fc.ToString().ToUpperInvariant();

            string format = PickSelectedFormat();
            return SafeFormat(format, letter);
        }

        string PickSelectedFormat()
        {
            bool turkish = IsTurkishLanguage();

            if (turkish && !string.IsNullOrEmpty(_textTr))
                return _textTr;

            if (!turkish && !string.IsNullOrEmpty(_textEn))
                return _textEn;

            if (!string.IsNullOrEmpty(_textEn))
                return _textEn;

            if (!string.IsNullOrEmpty(_textTr))
                return _textTr;

            return _selectedFormat;
        }

        static string SafeFormat(string format, string letterArg)
        {
            if (string.IsNullOrEmpty(format))
                return string.Empty;

            if (format.IndexOf("{0}", StringComparison.Ordinal) < 0)
                return format;

            try
            {
                return string.Format(format, letterArg);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        FireClass ResolveFireClass()
        {
            if (_selectionState != null)
                return _selectionState.TrainingFireClass;

            FireSource fs = GetComponentInParent<FireSource>();
            if (fs != null && fs.Data != null)
                return fs.Data.FireClass;

            return FireClass.A;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_label == null)
                _label = GetComponent<TMP_Text>();
        }
#endif
    }
}
