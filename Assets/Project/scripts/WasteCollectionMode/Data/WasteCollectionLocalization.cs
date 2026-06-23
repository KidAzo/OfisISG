using System;
using Woi.Events.Data;
using Woi.UI.Popups.Localization;
using WOI.Modules.SDK;

namespace Woi.WasteCollectionMode
{
    public static class WasteCollectionLocalization
    {
        public const string LangEnglish = "en";
        public const string LangTurkish = "tr";

        /// <summary>
        /// Session overlay choice wins; then networked session; then login scene; scene LocalizationService last.
        /// </summary>
        public static bool IsEnglish
        {
            get
            {
                if (SessionLanguageState.HasUserChoice)
                    return SessionLanguageState.IsEnglish;

                if (GameSessionData.IsSet)
                {
                    if (SessionLanguageState.HasUserChoice)
                        return SessionLanguageState.IsEnglish;

                    if (WasteLoginSession.IsSet)
                        return string.Equals(
                            WasteLoginSession.LanguageCode,
                            LangEnglish,
                            StringComparison.OrdinalIgnoreCase);
                }

                if (WasteLoginSession.IsSet)
                    return string.Equals(
                        WasteLoginSession.LanguageCode,
                        LangEnglish,
                        StringComparison.OrdinalIgnoreCase);

                return IsEnglishFromLocalizationService();
            }
        }

        public static bool IsEnglishFromDropdown(string dropdownLabel) =>
            string.Equals(dropdownLabel?.Trim(), "English", StringComparison.OrdinalIgnoreCase);

        public static string T(bool isEnglish, string turkish, string englishText) =>
            isEnglish ? englishText : turkish;

        public static string LoginTitleSub(bool english) =>
            T(english, "SIFIR ATIK", "ZERO WASTE");

        public static string LoginTitleMain(bool english) =>
            T(english, "EĞİTİM SİMÜLATÖRÜ", "TRAINING SIMULATOR");

        public static string ProfileSection(bool english) =>
            T(english, "KULLANICI PROFİLİ", "USER PROFILE");

        public static string LanguageSection(bool english) =>
            T(english, "DİL", "LANGUAGE");

        public static string UserNameLabel(bool english) =>
            T(english, "Ad Soyad", "Full Name");

        public static string UserIdLabel(bool english) =>
            T(english, "Kullanıcı ID", "User ID");

        public static string StartButton(bool english) =>
            T(english, "OYUNU BAŞLAT", "START GAME");

        public static string LeaderboardTitle(bool english) =>
            T(english, "BAŞARI TABLOSU", "LEADERBOARD");

        public static string ErrorNameRequired(bool english) =>
            T(english, "Lütfen ad soyad girin.", "Please enter your name.");

        public static string ErrorIdRequired(bool english) =>
            T(english, "Lütfen kullanıcı ID girin.", "Please enter your user ID.");

        public static string ErrorSceneLoaderMissing(bool english) =>
            T(english,
                "Scene loader bulunamadı. Waste Collection/Setup Login Scene menüsünü çalıştırın.",
                "Scene loader not found. Run Waste Collection/Setup Login Scene from the menu.");

        public static string ErrorSceneLoadFailed(bool english) =>
            T(english, "Sahne yüklenemedi.", "Failed to load scene.");

        public static string SelectionHeaderSubtitle(bool english) =>
            T(english, "ELDEKİ ATIK ONAYLANDI", "WASTE ITEM CONFIRMED");

        public static string SelectionHeaderDesc(bool english) =>
            T(english, "Hedef atık kutusunu seçin.", "Select the target waste bin.");

        public static string ExitTitle(bool english) =>
            T(english,
                "Oyunu bitirmek istediğinizden emin misiniz?",
                "Are you sure you want to finish the game?");

        public static string CancelButton(bool english) =>
            T(english, "İPTAL", "CANCEL");

        public static string ConfirmExitButton(bool english) =>
            T(english, "ÇIKIŞ YAP", "EXIT");

        public static string ResultTitle(bool english) =>
            T(english, "Simülasyon Tamamlandı", "Simulation Completed");

        public static string ResultSubtitle(bool english) =>
            T(english,
                "Atık Sınıflandırma Değerlendirme Raporu",
                "Waste Classification Evaluation Report");

        public static string CorrectStatLabel(bool english) =>
            T(english, "Doğru", "Correct");

        public static string IncorrectStatLabel(bool english) =>
            T(english, "Yanlış", "Wrong");

        public static string TableWasteHeader(bool english) =>
            T(english, "ATIK", "WASTE");

        public static string TableSelectedHeader(bool english) =>
            T(english, "SEÇİLEN KUTU", "SELECTED BIN");

        public static string TableCorrectHeader(bool english) =>
            T(english, "DOĞRU KUTU", "CORRECT BIN");

        public static string TableStatusHeader(bool english) =>
            T(english, "DURUM", "STATUS");

        public static string RestartButton(bool english) =>
            T(english, "TEKRAR BAŞLA", "RESTART");

        public static string QuitGameButton(bool english) =>
            T(english, "OYUNU KAPAT", "QUIT GAME");

        public static string StatusCorrect(bool english) =>
            T(english, "DOĞRU", "CORRECT");

        public static string StatusIncorrect(bool english) =>
            T(english, "HATALI", "INCORRECT");

        public static string StatusNotFound(bool english) =>
            T(english, "BULUNAMADI", "NOT FOUND");

        public static string EmptyClassification(bool english) =>
            T(english, "Henüz sınıflandırma yapılmadı.", "No classifications yet.");

        public static string UnknownWaste(bool english) =>
            T(english, "Bilinmeyen Atık", "Unknown Waste");

        private static bool IsEnglishFromLocalizationService()
        {
            if (ServiceLocator.TryGet(out ILocalizationService localization) && localization != null
                && !string.IsNullOrEmpty(localization.CurrentLanguage))
            {
                return localization.CurrentLanguage.Trim().ToLowerInvariant() == LangEnglish;
            }

            if (LocalizationService.Instance != null
                && !string.IsNullOrEmpty(LocalizationService.Instance.CurrentLanguage))
            {
                return LocalizationService.Instance.CurrentLanguage.Trim().ToLowerInvariant() == LangEnglish;
            }

            return false;
        }
    }
}
