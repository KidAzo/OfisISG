using System;
using System.Collections.Generic;
using UnityEngine;
using Woi.UI.Popups.Localization;

namespace Woi.UI.Popups
{
    [CreateAssetMenu(
        fileName = "PopupDefinition",
        menuName = "Woi/UI/Popup Definition",
        order = 0)]
    public sealed class PopupDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Optional stable id for analytics or announcements.")]
        public string id;

        [Header("Content")]
        [Tooltip("Variant [0] is the default popup text. Extra variants are only for Queue All (clip 1, 2, …). Put all languages (en, tr, …) as separate Lines inside the same variant — not one variant per language.")]
        public List<PopupContentVariant> contentVariants = new List<PopupContentVariant>();

        [HideInInspector]
        [SerializeField]
        private LocalizedText title = new LocalizedText();

        [HideInInspector]
        [SerializeField]
        private LocalizedText message = new LocalizedText();

        [Tooltip("Optional image in the left tile. Leave empty for text-only cards.")]
        public Sprite icon;

        [Header("Layout & style")]
        public PopupType type = PopupType.Info;
        public PopupAnchor anchor = PopupAnchor.TopRight;

        [HideInInspector]
        [Tooltip("Unused — close control is not shown (PopupService hides it for all popups).")]
        public bool hasCloseButton;

        [Tooltip("When off, the popup stays until closed manually or Hide(); Default Duration is ignored and the queue does not advance until this popup closes.")]
        public bool autoClose = true;

        [Tooltip("When true, a full-screen backdrop captures pointer input (modal). When false, only the popup card receives clicks. Announcements can override via Announcement Definition → Popup Blocks Input.")]
        public bool blockInput = false;

        [Min(0f)]
        [Tooltip("Seconds to show when autoClose is on and no duration override is passed to Show/Replace.")]
        public float defaultDuration = 4f;

        [Tooltip("Appended to the panel; use in USS for custom borders, fonts, etc.")]
        public string customUssClass;
        [Tooltip("Optional tint / future use. Card border uses USS frosted style.")]
        public Color accentColor = new Color(0.2f, 0.65f, 0.95f, 1f);
        [Tooltip("When true, a new show replaces the visible popup immediately. When false and PopupService overflow is Queue Next, the request waits until the current popup finishes its duration and closes.")]
        public bool replaceCurrentPopup = false;

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureContentMigrated();
            WarnDuplicateLanguageCodesInVariants();
            WarnIfLanguagesSplitAcrossVariants();
        }

        private void WarnIfLanguagesSplitAcrossVariants()
        {
            if (contentVariants == null || contentVariants.Count < 2)
                return;

            for (int i = 0; i < contentVariants.Count; i++)
            {
                List<PopupLocalizedLine> lines = contentVariants[i]?.lines;
                if (lines == null || lines.Count != 1)
                    return;
                if (lines[0] == null || string.IsNullOrWhiteSpace(lines[0].languageCode))
                    return;
            }

            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < contentVariants.Count; i++)
            {
                string key = contentVariants[i].lines[0].languageCode.Trim().ToLowerInvariant();
                if (!codes.Add(key))
                    return;
            }

            Debug.LogWarning(
                $"[PopupDefinition] '{name}': each Content Variant has a single Line with a different Language Code. The UI only uses variant [0] for a normal popup (later variants are for the next clip in Queue All). Merge into one variant: remove extra variants, then under Lines add one row for en and one for tr.",
                this);
        }

        private void WarnDuplicateLanguageCodesInVariants()
        {
            if (contentVariants == null)
                return;

            for (int v = 0; v < contentVariants.Count; v++)
            {
                List<PopupLocalizedLine> lines = contentVariants[v]?.lines;
                if (lines == null)
                    continue;

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (PopupLocalizedLine line in lines)
                {
                    if (line == null || string.IsNullOrWhiteSpace(line.languageCode))
                        continue;

                    string key = line.languageCode.Trim().ToLowerInvariant();
                    if (seen.Add(key))
                        continue;

                    Debug.LogWarning(
                        $"[PopupDefinition] '{name}' — variant [{v}] repeats Language Code '{line.languageCode}'. Only the first row for each language is shown. Use one row per language per variant; add another Content Variant for the next queued clip.",
                        this);
                    break;
                }
            }
        }
#endif

        /// <summary>Migrates legacy title/message lists into <see cref="contentVariants"/> once; safe to call from runtime UI.</summary>
        internal void EnsureContentMigrated()
        {
            if (contentVariants == null)
                contentVariants = new List<PopupContentVariant>();

            if (contentVariants.Count > 0)
                return;

            int tCount = title?.entries?.Count ?? 0;
            int mCount = message?.entries?.Count ?? 0;

            if (tCount == 0 && mCount == 0)
            {
                contentVariants.Add(NewVariantWithBlankLine());
                return;
            }

            int maxCount = Math.Max(tCount, mCount);
            bool multilingual = DetectMultilingualTitleRows(tCount);

            if (multilingual)
            {
                var variant = new PopupContentVariant();
                for (int i = 0; i < maxCount; i++)
                {
                    string lang = i < tCount && title.entries[i] != null
                        ? NormalizeLang(title.entries[i].languageCode)
                        : LocalizationService.English;
                    string t = i < tCount && title.entries[i] != null ? title.entries[i].text ?? string.Empty : string.Empty;
                    string msg = i < mCount && message.entries[i] != null ? message.entries[i].text ?? string.Empty : string.Empty;
                    variant.lines.Add(new PopupLocalizedLine { languageCode = lang, title = t, message = msg });
                }

                contentVariants.Add(variant);
            }
            else
            {
                for (int i = 0; i < maxCount; i++)
                {
                    string lang = i < tCount && title.entries[i] != null
                        ? NormalizeLang(title.entries[i].languageCode)
                        : LocalizationService.English;
                    string t = i < tCount && title.entries[i] != null ? title.entries[i].text ?? string.Empty : string.Empty;
                    string msg = i < mCount && message.entries[i] != null ? message.entries[i].text ?? string.Empty : string.Empty;

                    var variant = new PopupContentVariant();
                    variant.lines.Add(new PopupLocalizedLine { languageCode = lang, title = t, message = msg });
                    contentVariants.Add(variant);
                }
            }
        }

        private bool DetectMultilingualTitleRows(int tCount)
        {
            if (tCount <= 1 || title?.entries == null)
                return false;

            string first = NormalizeLang(title.entries[0].languageCode);
            for (int i = 1; i < tCount; i++)
            {
                if (title.entries[i] == null)
                    continue;
                string next = NormalizeLang(title.entries[i].languageCode);
                if (!string.Equals(first, next, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string NormalizeLang(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return LocalizationService.English;
            return code.Trim().ToLowerInvariant();
        }

        private static PopupContentVariant NewVariantWithBlankLine()
        {
            return new PopupContentVariant
            {
                lines = new List<PopupLocalizedLine>
                {
                    new PopupLocalizedLine { languageCode = LocalizationService.English }
                }
            };
        }

        /// <summary>In-memory helper for <see cref="PopupService.ShowText"/>.</summary>
        internal static PopupDefinition CreateTransient(
            string titleText,
            string messageText,
            PopupType popupType,
            PopupAnchor popupAnchor = PopupAnchor.TopRight)
        {
            PopupDefinition d = CreateInstance<PopupDefinition>();
            d.hideFlags = HideFlags.HideAndDontSave;
            d.contentVariants = new List<PopupContentVariant>
            {
                new PopupContentVariant
                {
                    lines = new List<PopupLocalizedLine>
                    {
                        new PopupLocalizedLine
                        {
                            languageCode = LocalizationService.English,
                            title = titleText,
                            message = messageText
                        }
                    }
                }
            };
            d.type = popupType;
            d.anchor = popupAnchor;
            d.replaceCurrentPopup = true;
            return d;
        }

        /// <summary>In-memory helper for <see cref="PopupService.ShowLocalizedText"/> — tek variant, tr + en satırları.</summary>
        internal static PopupDefinition CreateTransientBilingual(
            string titleTr,
            string messageTr,
            string titleEn,
            string messageEn,
            PopupType popupType,
            float defaultDurationSeconds,
            PopupAnchor popupAnchor = PopupAnchor.TopRight)
        {
            PopupDefinition d = CreateInstance<PopupDefinition>();
            d.hideFlags = HideFlags.HideAndDontSave;
            d.contentVariants = new List<PopupContentVariant>
            {
                new PopupContentVariant
                {
                    lines = new List<PopupLocalizedLine>
                    {
                        new PopupLocalizedLine
                        {
                            languageCode = LocalizationService.Turkish,
                            title = titleTr ?? string.Empty,
                            message = messageTr ?? string.Empty
                        },
                        new PopupLocalizedLine
                        {
                            languageCode = LocalizationService.English,
                            title = titleEn ?? string.Empty,
                            message = messageEn ?? string.Empty
                        }
                    }
                }
            };
            d.type = popupType;
            d.anchor = popupAnchor;
            d.replaceCurrentPopup = true;
            d.autoClose = true;
            d.blockInput = false;
            d.defaultDuration = Mathf.Max(0f, defaultDurationSeconds);
            return d;
        }

        /// <summary>In-memory popup that stays until <see cref="PopupService.Hide"/> (hover cards).</summary>
        internal static PopupDefinition CreateHoverTransient(
            string titleText,
            string messageText,
            PopupType popupType,
            PopupAnchor popupAnchor = PopupAnchor.TopRight)
        {
            PopupDefinition d = CreateTransient(titleText, messageText, popupType, popupAnchor);
            d.autoClose = false;
            d.blockInput = false;
            d.replaceCurrentPopup = true;
            return d;
        }
    }
}
