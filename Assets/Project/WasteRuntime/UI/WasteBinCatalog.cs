using System;
using System.Collections.Generic;

namespace Woi.WasteCollectionMode
{
    public static class WasteBinCatalog
    {
        private static readonly Dictionary<string, string> BinNamesEn = new(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = "Paper & Cardboard Waste",
            ["2"] = "Cardboard Waste",
            ["3"] = "Plastic Waste",
            ["4"] = "Glass Waste",
            ["5"] = "Biodegradable Waste",
            ["6"] = "Used Battery",
            ["7"] = "Toner & Cartridge",
            ["8"] = "Electronic Waste",
            ["9"] = "Plastic Cap",
            ["10"] = "Metal Waste",
            ["11"] = "Cigarette Butt",
            ["12"] = "Recyclable Waste",
            ["13"] = "Hazardous Waste",
            ["14"] = "Medical Waste",
            ["15"] = "Composite Waste",
            ["16"] = "Domestic Waste",
        };

        private static readonly Dictionary<string, string> BinNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = "Kağıt-Karton Atıklar",
            ["2"] = "Karton Atıklar",
            ["3"] = "Plastik Atıklar",
            ["4"] = "Cam Atıklar",
            ["5"] = "Bio-Bozunur Atıklar",
            ["6"] = "Kullanılmış Pil",
            ["7"] = "Toner & Kartuş",
            ["8"] = "Elektronik Atık",
            ["9"] = "Plastik Kapak",
            ["10"] = "Metal Atıklar",
            ["11"] = "Sigara İzmariti",
            ["12"] = "Geri Kazanılabilir",
            ["13"] = "Tehlikeli Atık",
            ["14"] = "Tıbbi Atık",
            ["15"] = "Kompozit Atık",
            ["16"] = "Evsel Atık",
        };

        private static readonly Dictionary<string, string> WasteNameToBinId = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Paper"] = "1",
            ["CardboardBox"] = "1",   // Kağıt-Karton birleşik kutu (Sıfır Atık standardı)
            ["WaterBottle"] = "3",
            ["BabyWipe"] = "16",      // Evsel Atık (ıslak mendil)
            ["WBCap"] = "9",
            ["GlassBottle"] = "4",
            ["Chicken_Pile"] = "5",
            ["Battery"] = "6",
            ["Cartridge"] = "8",      // Toner kaldırıldı → Elektronik kapsamında
            ["Keyboard"] = "8",
            ["Soda"] = "10",
            ["Juice"] = "15",
            ["sut"] = "15",
            ["Mask"] = "14",
            ["Bulp"] = "8",           // Elektronik
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
