using System;
using System.Collections.Generic;

namespace Woi.WasteCollectionMode
{
    public static class WasteBinCatalog
    {
        private static readonly Dictionary<string, string> BinNamesEn = new(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = "Paper Waste",
            ["2"] = "Cardboard Waste",
            ["3"] = "Plastic Waste",
            ["4"] = "Glass Waste",
            ["5"] = "Organic Food",
            ["6"] = "Used Battery",
            ["7"] = "Toner & Cartridge",
            ["8"] = "Electronic Waste",
            ["9"] = "Plastic Cap",
            ["10"] = "Metal Can",
            ["11"] = "Cigarette Butt",
            ["12"] = "Non-Recyclable",
            ["13"] = "Hazardous Waste",
            ["14"] = "Medical Waste",
            ["15"] = "Bulb/Fluorescent",
        };

        private static readonly Dictionary<string, string> BinNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = "Kağıt Atıklar",
            ["2"] = "Karton Atıklar",
            ["3"] = "Plastik Atıklar",
            ["4"] = "Cam Atıklar",
            ["5"] = "Organik Yemek",
            ["6"] = "Kullanılmış Pil",
            ["7"] = "Toner & Kartuş",
            ["8"] = "Elektronik Atık",
            ["9"] = "Plastik Kapak",
            ["10"] = "Metal Kutu",
            ["11"] = "Sigara İzmariti",
            ["12"] = "Geri Dönüşmez",
            ["13"] = "Tehlikeli Atık",
            ["14"] = "Tıbbi Atık",
            ["15"] = "Ampul/Floresan",
        };

        private static readonly Dictionary<string, string> WasteNameToBinId = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Paper"] = "1",
            ["CardboardBox"] = "2",
            ["WaterBottle"] = "3",
            ["BabyWipe"] = "12",
            ["WBCap"] = "9",
            ["GlassBottle"] = "4",
            ["Chicken_Pile"] = "5",
            ["Battery"] = "6",
            ["Cartridge"] = "7",
            ["Keyboard"] = "8",
            ["Soda"] = "10",
            ["Cigarette"] = "11",
            ["Mask"] = "14",
            ["Bulp"] = "15",
        };

        public static string GetBinName(string binId)
        {
            if (string.IsNullOrWhiteSpace(binId))
                return "-";

            if (WasteCollectionLocalization.IsEnglish &&
                BinNamesEn.TryGetValue(binId, out string englishName))
                return englishName;

            return BinNames.TryGetValue(binId, out string name) ? name : binId;
        }

        public static string GetCorrectBinId(string wasteName, WasteType wasteType)
        {
            string key = WasteNameCatalog.NormalizeKey(wasteName);
            if (!string.IsNullOrWhiteSpace(key) && WasteNameToBinId.TryGetValue(key, out string binId))
                return binId;

            return wasteType switch
            {
                WasteType.Paper => "1",
                WasteType.Plastic => "3",
                WasteType.Glass => "4",
                WasteType.Metal => "10",
                WasteType.Organic => "5",
                _ => "12",
            };
        }
    }
}
