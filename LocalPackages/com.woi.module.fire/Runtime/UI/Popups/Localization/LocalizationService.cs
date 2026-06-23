using System;
using System.Collections.Generic;
using UnityEngine;
using WOI.Modules.SDK;

namespace Woi.UI.Popups.Localization
{
    /// <summary>
    /// Runtime language selection and text resolution for popups (and other UI).
    /// Place one instance in bootstrap / menu scene; optional DontDestroyOnLoad.
    ///
    /// Setup:
    /// - Add component to a GameObject.
    /// - Default codes: "en", "tr". Add more entries to supportedLanguageCodes if needed.
    /// - Call <see cref="SetLanguage"/> when the player changes language.
    ///
    /// Popup pipeline:
    /// - Works with <see cref="PopupService"/> when assigned or discovered on scene.
    /// - Author <see cref="PopupContentVariant"/> lines per popup (title + message per language).
    /// </summary>
    [DefaultExecutionOrder(-4980)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/UI/Localization Service")]
    public sealed class LocalizationService : MonoBehaviour, ILocalizationService
    {
        public const string English = "en";
        public const string Turkish = "tr";

        [SerializeField] private bool dontDestroyOnLoad = true;
        [Tooltip("Startup language before the player changes it (e.g. en or tr). Must match a row Language Code on popups.")]
        [SerializeField] private string defaultLanguageCode = Turkish;

        [Header("Service locator")]
        [Tooltip("Registers on ServiceLocator in Start when not already registered.")]
        [SerializeField]
        private bool registerWithServiceLocator = true;

        private bool _registeredWithServiceLocator;

        [Tooltip("Optional allow-list for inspector dropdowns / validation.")]
        [SerializeField]
        private List<string> supportedLanguageCodes = new List<string> { English, Turkish };

        private string _currentLanguage;

        /// <summary>First scene instance found (lazy).</summary>
        public static LocalizationService Instance { get; private set; }

        /// <summary>Active ISO-style language code (e.g. en, tr).</summary>
        public string CurrentLanguage
        {
            get => _currentLanguage;
            private set => _currentLanguage = value;
        }

        public IReadOnlyList<string> SupportedLanguageCodes => supportedLanguageCodes;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            _currentLanguage = string.IsNullOrEmpty(defaultLanguageCode)
                ? Turkish
                : defaultLanguageCode.Trim().ToLowerInvariant();
        }

        private void Start()
        {
            TryRegisterWithServiceLocator();
        }

        private void OnDestroy()
        {
            TryUnregisterWithServiceLocator();

            if (Instance == this)
                Instance = null;
        }

        private void TryRegisterWithServiceLocator()
        {
            if (!registerWithServiceLocator)
                return;

            if (ServiceLocator.IsRegistered<ILocalizationService>())
                return;

            ServiceLocator.Register<ILocalizationService>(this);
            ServiceLocator.Register<LocalizationService>(this);
            _registeredWithServiceLocator = true;
        }

        private void TryUnregisterWithServiceLocator()
        {
            if (!_registeredWithServiceLocator)
                return;

            ServiceLocator.Unregister<ILocalizationService>();
            ServiceLocator.Unregister<LocalizationService>();
            _registeredWithServiceLocator = false;
        }

        /// <summary>Switch active language at runtime.</summary>
        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return;

            languageCode = languageCode.Trim().ToLowerInvariant();
            CurrentLanguage = languageCode;
        }

        /// <summary>
        /// When <paramref name="entryIndex"/> is &gt;= 0 and in range, returns that entry's text first (same index for title/message lists — pairs with queued clips).
        /// Otherwise falls through to language-based resolution.
        /// </summary>
        public string GetText(LocalizedText localized, int entryIndex)
        {
            if (localized == null || localized.entries == null || localized.entries.Count == 0)
                return string.Empty;

            if (entryIndex >= 0 && entryIndex < localized.entries.Count)
            {
                LocalizedStringEntry e = localized.entries[entryIndex];
                if (e != null && !string.IsNullOrEmpty(e.text))
                    return e.text;
            }

            return GetText(localized);
        }

        /// <summary>
        /// Resolves text: current language → English → first non-empty entry.
        /// </summary>
        public string GetText(LocalizedText localized)
        {
            if (localized == null || localized.entries == null || localized.entries.Count == 0)
                return string.Empty;

            string current = CurrentLanguage?.ToLowerInvariant() ?? English;

            string byCurrent = FindEntryText(localized.entries, current);
            if (!string.IsNullOrEmpty(byCurrent))
                return byCurrent;

            string byEn = FindEntryText(localized.entries, English);
            if (!string.IsNullOrEmpty(byEn))
                return byEn;

            for (int i = 0; i < localized.entries.Count; i++)
            {
                LocalizedStringEntry e = localized.entries[i];
                if (e != null && !string.IsNullOrEmpty(e.text))
                    return e.text;
            }

            return string.Empty;
        }

        private static string FindEntryText(List<LocalizedStringEntry> list, string code)
        {
            if (list == null || string.IsNullOrEmpty(code))
                return null;

            for (int i = 0; i < list.Count; i++)
            {
                LocalizedStringEntry e = list[i];
                if (e == null || string.IsNullOrEmpty(e.languageCode))
                    continue;
                if (string.Equals(e.languageCode.Trim(), code, StringComparison.OrdinalIgnoreCase))
                    return e.text;
            }

            return null;
        }

        /// <inheritdoc />
        public void GetPopupVariantText(PopupContentVariant variant, out string title, out string message)
        {
            title = string.Empty;
            message = string.Empty;

            if (variant?.lines == null || variant.lines.Count == 0)
                return;

            string current = CurrentLanguage?.ToLowerInvariant() ?? English;

            PopupLocalizedLine pick = FindLineForLanguage(variant.lines, current);
            if (pick != null && (!string.IsNullOrWhiteSpace(pick.title) || !string.IsNullOrWhiteSpace(pick.message)))
            {
                title = pick.title ?? string.Empty;
                message = pick.message ?? string.Empty;
                return;
            }

            pick = FindLineForLanguage(variant.lines, English);
            if (pick != null && (!string.IsNullOrWhiteSpace(pick.title) || !string.IsNullOrWhiteSpace(pick.message)))
            {
                title = pick.title ?? string.Empty;
                message = pick.message ?? string.Empty;
                return;
            }

            for (int i = 0; i < variant.lines.Count; i++)
            {
                PopupLocalizedLine line = variant.lines[i];
                if (line == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(line.title) || !string.IsNullOrWhiteSpace(line.message))
                {
                    title = line.title ?? string.Empty;
                    message = line.message ?? string.Empty;
                    return;
                }
            }
        }

        private static PopupLocalizedLine FindLineForLanguage(List<PopupLocalizedLine> lines, string code)
        {
            if (lines == null || string.IsNullOrEmpty(code))
                return null;

            for (int i = 0; i < lines.Count; i++)
            {
                PopupLocalizedLine line = lines[i];
                if (line == null || string.IsNullOrEmpty(line.languageCode))
                    continue;
                if (string.Equals(line.languageCode.Trim(), code, StringComparison.OrdinalIgnoreCase))
                    return line;
            }

            return null;
        }
    }
}
