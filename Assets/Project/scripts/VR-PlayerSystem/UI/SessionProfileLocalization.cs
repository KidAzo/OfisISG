using System;

namespace Woi.DataHandler
{
    public static class SessionProfileLocalization
    {
        public static bool IsEnglishFromDropdown(string dropdownLabel) =>
            string.Equals(dropdownLabel?.Trim(), "English", StringComparison.OrdinalIgnoreCase);

        public static string DropdownLabelFromLanguageCode(string languageCode)
        {
            if (string.Equals(languageCode?.Trim(), "en", StringComparison.OrdinalIgnoreCase)
                || string.Equals(languageCode, "english", StringComparison.OrdinalIgnoreCase))
            {
                return "English";
            }

            return "Türkçe";
        }

        public static string LanguageCodeFromDropdownLabel(string dropdownLabel) =>
            IsEnglishFromDropdown(dropdownLabel) ? "en" : "tr";

        public static bool IsEnglishLanguageCode(string languageCode) =>
            string.Equals(languageCode?.Trim(), "en", StringComparison.OrdinalIgnoreCase);

        private static string T(bool english, string turkish, string englishText) =>
            english ? englishText : turkish;

        public static string TitleSub(bool english) =>
            T(english, "OFİS İSG", "OFFICE OHS");

        public static string TitleMain(bool english) =>
            T(english, "OTURUM", "SESSION");

        public static string ProfileSection(bool english) =>
            T(english, "KULLANICI PROFİLİ", "USER PROFILE");

        public static string LanguageSection(bool english) =>
            T(english, "DİL", "LANGUAGE");

        public static string NameFieldLabel(bool english) =>
            T(english, "Ad Soyad", "Full Name");

        public static string IdFieldLabel(bool english) =>
            T(english, "Kullanıcı ID", "User ID");

        public static string StatusWaiting(bool english) =>
            T(english, "Sunucudan veri bekleniyor…", "Waiting for server data…");

        public static string StatusReady(bool english) =>
            T(english, "Oturum alındı. Oyun başlıyor…", "Session received. Starting game…");
    }
}
