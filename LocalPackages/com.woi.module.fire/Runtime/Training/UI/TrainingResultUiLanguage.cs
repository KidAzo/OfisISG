using System;
using System.Text.RegularExpressions;
using WOI.Modules.SDK;
using Woi.UI.Popups.Localization;

namespace Woi.Game.Training.UI
{
    /// <summary>
    /// Sonuç ekranı için aktif dil (<see cref="ILocalizationService"/> / <see cref="LocalizationService"/>).
    /// Oturum kritik metinleri için <see cref="LocalizeSessionMessageForDisplay"/> kullanılır.
    /// </summary>
    public static class TrainingResultUiLanguage
    {
        static readonly Regex s_fireNotOutEn = new Regex(
            @"^Fire '(?<k>[^']+)' was not fully extinguished\.\s*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        static readonly Regex s_sweepPerfTr = new Regex(
            @"^Süpürme performansı:\s*(?<p>\d+)/100\s*—\s*(?<t>.*)\s*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        static readonly Regex s_sweepPerfEn = new Regex(
            @"^Sweep performance:\s*(?<p>\d+)/100\s*—\s*(?<t>.*)\s*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        static readonly Regex s_fireNotOutTr = new Regex(
            @"^'(?<k>[^']+)' yangını tamamen söndürülmedi\.\s*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static string ResolveCode()
        {
            if (ServiceLocator.TryGet<ILocalizationService>(out ILocalizationService loc) && loc != null && !string.IsNullOrEmpty(loc.CurrentLanguage))
                return loc.CurrentLanguage.Trim().ToLowerInvariant();

            if (LocalizationService.Instance != null && !string.IsNullOrEmpty(LocalizationService.Instance.CurrentLanguage))
                return LocalizationService.Instance.CurrentLanguage.Trim().ToLowerInvariant();

            return LocalizationService.Turkish;
        }

        public static bool IsTurkish()
        {
            string c = ResolveCode();
            return c == LocalizationService.Turkish || c.StartsWith("tr", StringComparison.Ordinal);
        }

        /// <summary>
        /// Maps <c>CriticalMistakes</c> lines to the active UI language (canonical storage is English;
        /// older sessions may contain Turkish sweep lines).
        /// </summary>
        public static string LocalizeSessionMessageForDisplay(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            string s = raw.Trim();
            return IsTurkish()
                ? SessionMessageToTurkish(s)
                : SessionMessageToEnglish(s);
        }

        static string SessionMessageToTurkish(string s)
        {
            if (TryMapExactEnglishToTurkish(s, out string tr))
                return tr;

            Match m = s_fireNotOutEn.Match(s);
            if (m.Success)
            {
                string k = m.Groups["k"].Value;
                return $"'{k}' yangını tamamen söndürülmedi.";
            }

            Match sweepEn = s_sweepPerfEn.Match(s);
            if (sweepEn.Success)
            {
                string p = sweepEn.Groups["p"].Value;
                string tail = sweepEn.Groups["t"].Value.Trim();
                return $"Süpürme performansı: {p}/100 — {MapSweepFeedbackEnToTr(tail)}";
            }

            return s;
        }

        static string SessionMessageToEnglish(string s)
        {
            if (TryMapExactTurkishToEnglish(s, out string en))
                return en;

            Match m = s_sweepPerfTr.Match(s);
            if (m.Success)
            {
                string p = m.Groups["p"].Value;
                string tail = m.Groups["t"].Value.Trim();
                return $"Sweep performance: {p}/100 — {MapSweepFeedbackTrToEn(tail)}";
            }

            Match ftr = s_fireNotOutTr.Match(s);
            if (ftr.Success)
                return $"Fire '{ftr.Groups["k"].Value}' was not fully extinguished.";

            return s;
        }

        static bool TryMapExactEnglishToTurkish(string s, out string tr)
        {
            tr = s switch
            {
                "Used extinguisher type was not recorded; equip an extinguisher before ending the session."
                    => "Kullanılan söndürücü tipi kaydedilmedi; oturumu bitirmeden önce bir söndürücü kuşanın.",
                "Incompatible extinguisher agent sprayed on fire zone."
                    => "Yangın bölgesine uyumsuz söndürücü ajanı püskürtüldü.",
                "Wrong extinguisher type for one or more fires."
                    => "Bir veya daha fazla yangın için yanlış söndürücü tipi.",
                "Extinguisher was depleted before the fire was fully extinguished."
                    => "Yangın tamamen söndürülmeden önce söndürücü tükendi.",
                "Fire was not fully extinguished."
                    => "Yangın tamamen söndürülmedi.",
                _ => null,
            };
            return tr != null;
        }

        static bool TryMapExactTurkishToEnglish(string s, out string en)
        {
            en = s switch
            {
                "Kullanılan söndürücü tipi kaydedilmedi; oturumu bitirmeden önce bir söndürücü kuşanın."
                    => "Used extinguisher type was not recorded; equip an extinguisher before ending the session.",
                "Yangın bölgesine uyumsuz söndürücü ajanı püskürtüldü."
                    => "Incompatible extinguisher agent sprayed on fire zone.",
                "Bir veya daha fazla yangın için yanlış söndürücü tipi."
                    => "Wrong extinguisher type for one or more fires.",
                "Yangın tamamen söndürülmeden önce söndürücü tükendi."
                    => "Extinguisher was depleted before the fire was fully extinguished.",
                "Yangın tamamen söndürülmedi."
                    => "Fire was not fully extinguished.",
                _ => null,
            };
            return en != null;
        }

        static string MapSweepFeedbackEnToTr(string tail)
        {
            if (string.IsNullOrEmpty(tail))
                return tail;

            return tail switch
            {
                "No spray hits recorded." => "Soluma isabeti kaydedilmedi.",
                "Fire base was not targeted." => "Yangının tabanı hedeflenmedi.",
                "Spray was held on a single point." => "Spray tek noktaya sabit tutuldu.",
                "Fire base was swept horizontally." => "Yangının tabanı yatay olarak tarandı.",
                "Sweep motion was too short." => "Tarama hareketi kısa sürdü.",
                "Sweep was too abrupt or samples were not spread enough in time."
                    => "Tarama çok ani oldu veya örnekler zamanda yeterince yayılmadı.",
                "Sweep did not fully meet training criteria." => "Tarama eğitim kriterlerini tam karşılamadı.",
                _ => tail,
            };
        }

        static string MapSweepFeedbackTrToEn(string tail)
        {
            if (string.IsNullOrEmpty(tail))
                return tail;

            return tail switch
            {
                "Soluma isabeti kaydedilmedi." => "No spray hits recorded.",
                "Yangının tabanı hedeflenmedi." => "Fire base was not targeted.",
                "Spray tek noktaya sabit tutuldu." => "Spray was held on a single point.",
                "Yangının tabanı yatay olarak tarandı." => "Fire base was swept horizontally.",
                "Tarama hareketi kısa sürdü." => "Sweep motion was too short.",
                "Tarama çok ani oldu veya örnekler zamanda yeterince yayılmadı."
                    => "Sweep was too abrupt or samples were not spread enough in time.",
                "Tarama eğitim kriterlerini tam karşılamadı." => "Sweep did not fully meet training criteria.",
                _ => tail,
            };
        }
    }
}
