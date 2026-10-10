using System;
using System.Collections.Generic;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Display names for waste item keys (GameObject / definition names). Internal keys stay English for logic.
    /// </summary>
    public static class WasteNameCatalog
    {
        private static readonly Dictionary<string, string> NamesTr = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Paper"] = "Kağıt",
            ["CardboardBox"] = "Karton Kutu",
            ["WaterBottle"] = "Plastik Su Şişeleri",
            ["BabyWipe"] = "Islak Mendil",
            ["WBCap"] = "Plastik Şişe Kapağı",
            ["GlassBottle"] = "Cam Şişe",
            ["Chicken_Pile"] = "Yemek Atıkları",
            ["Battery"] = "Pil",
            ["Cartridge"] = "Yazıcı Kartuşu",
            ["Keyboard"] = "Bozuk Bilgisayar Klavyesi",
            ["Soda"] = "Gazoz Kutusu",
            ["Juice"] = "Meyve Suyu Kutusu",
            ["Mask"] = "Cerrahi Maske",
            ["Bulp"] = "Ampul",
        };

        private static readonly Dictionary<string, string> NamesEn = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Paper"] = "Paper",
            ["CardboardBox"] = "Cardboard Box",
            ["WaterBottle"] = "Water Bottles",
            ["BabyWipe"] = "Baby Wipe",
            ["WBCap"] = "Plastic Bottle Cap",
            ["GlassBottle"] = "Glass Bottle",
            ["Chicken_Pile"] = "Food Wastes",
            ["Battery"] = "Battery",
            ["Cartridge"] = "Toner Cartridge",
            ["Keyboard"] = "Broken Keyboard",
            ["Soda"] = "Soda Can",
            ["Juice"] = "Juice Box",
            ["Mask"] = "Face Mask",
            ["Bulp"] = "Light Bulb",
        };

        public static string NormalizeKey(string wasteKey)
        {
            if (string.IsNullOrWhiteSpace(wasteKey))
                return string.Empty;

            if (wasteKey.StartsWith("WBCap", StringComparison.OrdinalIgnoreCase))
                return "WBCap";

            if (string.Equals(wasteKey, "sut", StringComparison.OrdinalIgnoreCase))
                return "Juice";

            return wasteKey.Trim();
        }

        public static string GetDisplayName(string wasteKey) =>
            GetDisplayName(wasteKey, WasteCollectionLocalization.IsEnglish);

        public static string GetDisplayName(string wasteKey, bool english)
        {
            string key = NormalizeKey(wasteKey);
            if (string.IsNullOrEmpty(key))
                return WasteCollectionLocalization.UnknownWaste(english);

            Dictionary<string, string> names = english ? NamesEn : NamesTr;
            if (names.TryGetValue(key, out string displayName))
                return displayName;

            return english ? PrettifyKey(key) : key;
        }

        private static string PrettifyKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return key;

            return key.Replace('_', ' ');
        }
    }
}
