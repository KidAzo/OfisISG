using System;

namespace Woi.UI.Popups
{
    /// <summary>Standalone popup API — no audio references.</summary>
    public interface IPopupService
    {
        event Action<PopupDefinition> OnPopupShown;
        event Action OnPopupHidden;
        event Action<PopupDefinition> OnPopupClicked;
        event Action<PopupDefinition> OnPopupCloseButtonClicked;

        bool IsVisible { get; }

        void Show(PopupDefinition definition);
        void Show(PopupDefinition definition, float durationOverride, bool? blockInputOverride = null);
        /// <summary>Use content variant slot <paramref name="contentEntryIndex"/> on <see cref="PopupDefinition.contentVariants"/> (queued clips).</summary>
        void Show(PopupDefinition definition, float durationOverride, int contentEntryIndex, bool? blockInputOverride = null);
        void ShowText(string title, string message, PopupType type);

        /// <summary>Tek dilli geçici popup; <paramref name="visibleSeconds"/> sonra otomatik kapanır.</summary>
        void ShowText(string title, string message, PopupType type, float visibleSeconds);

        /// <summary>Geçici popup (tr + en içerik); <paramref name="visibleSeconds"/> sonra otomatik kapanır.</summary>
        void ShowLocalizedText(
            string titleTr,
            string messageTr,
            string titleEn,
            string messageEn,
            PopupType type,
            float visibleSeconds);

        /// <summary>Shows title/message until <see cref="Hide"/> — non-modal, no auto-close (hover / tooltip).</summary>
        void ShowTextUntilHidden(string title, string message, PopupType type);

        void ShowTextUntilHidden(string title, string message, PopupType type, PopupAnchor anchor);

        void Hide();
        /// <summary>Hides the current popup and clears queued Show requests (e.g. level change).</summary>
        void DismissAllPopups();
        void Replace(PopupDefinition definition);
        void Replace(PopupDefinition definition, float durationOverride, bool? blockInputOverride = null);
        void Replace(PopupDefinition definition, float durationOverride, int contentEntryIndex, bool? blockInputOverride = null);
    }
}
