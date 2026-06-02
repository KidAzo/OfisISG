#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using WoiUtils.AudioSystem;

namespace Woi.WasteCollectionMode.Editor
{
    /// <summary>
    /// Builds the Waste Collection audio assets from the mp3 files under
    /// Assets/Project/Sounds/WasteSounds and the explanation texts from the Excel sheet.
    ///
    /// Sound numbering (1..12) matches the active Excel sheet order:
    ///   1 Kağıt, 2 Plastik, 3 Cam, 4 Metal, 5 Bio-bozunur, 6 Atık Pil, 7 Toner/Kartuş,
    ///   8 Elektronik, 9 Kompozit, 10 Geri Kazanılabilir, 11 Tıbbi, 12 Plastik Kapak.
    ///   {n}_   = first-selection (per-waste) sound       {n}_a_ = explanation sound
    /// Selection/ holds the correct (Doğru/Harika/Mükemmel) and wrong (Yanlış) sounds.
    ///
    /// Each WasteDefinition is matched to a sound number through its correct bin
    /// (<see cref="WasteBinCatalog.GetCorrectBinId"/>); see <see cref="BinIdToIndex"/>.
    /// </summary>
    public static class WasteAudioBuildSetup
    {
        private const string SoundsRoot = "Assets/Project/Sounds/WasteSounds";
        private const string OutRoot = "Assets/Project/WasteCollection/Audio";

        private readonly struct CategoryEntry
        {
            public CategoryEntry(int index, string key, string binId, string tr, string en)
            {
                Index = index;
                Key = key;
                BinId = binId;
                Tr = tr;
                En = en;
            }

            public int Index { get; }
            public string Key { get; }
            public string BinId { get; }
            public string Tr { get; }
            public string En { get; }
        }

        // Order + texts taken from the active Excel sheet ("Sayfa1 (2)", 12 rows).
        private static readonly CategoryEntry[] Entries =
        {
            new CategoryEntry(1, "Kagit", "1",
                "Gazeteler, dergiler, yazı ve çizim kâğıtları kağıt-karton atıkları 'Kağıt Atıklar' atık kutusuna atılmalıdır.",
                "Newspapers, magazines, writing and drawing papers should be placed in the 'Paper Waste' bin."),
            new CategoryEntry(2, "Plastik", "3",
                "Plastik şişeler, plastik kutular 'Plastik Atıklar' atık kutusuna atılmalıdır.",
                "Plastic bottles and containers should be placed in the 'Plastic Waste' bin."),
            new CategoryEntry(3, "Cam", "4",
                "Cam içecek ve gıda şişeleri 'Cam Atıklar' atık kutusuna atılmalıdır.",
                "Glass beverage and food bottles should be placed in the 'Glass Waste' bin."),
            new CategoryEntry(4, "Metal", "10",
                "Gazoz kutuları 'Gazoz Kutusu' atık kutusuna atılmalıdır.",
                "Soda cans should be placed in the 'Soda Can' bin."),
            new CategoryEntry(5, "BioBozunur", "5",
                "Mutfak, park bahçe, pazar atıkları ve gıda atıkları 'Bio-Bozunur Atıklar' atık kutusuna atılmalıdır.",
                "Kitchen, park and garden, market, and food waste should be placed in the 'Biodegradable Waste' bin."),
            new CategoryEntry(6, "AtikPil", "6",
                "Atık piller 'Atık Pil' atık kutusuna atılmalıdır.",
                "Waste batteries should be placed in the 'Waste Battery' bin."),
            new CategoryEntry(7, "TonerKartus", "7",
                "Kullanılmış yazıcı tonerleri ve kartuşları 'Toner - Kartuş' atık kutusuna atılmalıdır.",
                "Used printer toner and ink cartridges should be placed in the 'Toner & Cartridge Waste' bin."),
            new CategoryEntry(8, "Elektronik", "8",
                "Bilişim ve telekomünikasyon ekipmanları vb. atıkları 'Atık Elektrikli ve Elektronik Eşyalar' atık kutusuna atılmalıdır.",
                "IT and telecommunications equipment waste should be placed in the 'Waste Electrical and Electronic Equipment' bin."),
            new CategoryEntry(9, "Kompozit", "15",
                "Meyve suyu kutusu, cips paketi gibi kompozit ambalajlar 'Kompozit Atıklar' atık kutusuna atılmalıdır.",
                "Juice boxes, chip packets, and similar composite packaging should be placed in the 'Composite Waste' bin."),
            new CategoryEntry(10, "GeriKazanilabilir", "12",
                "Kağıt, cam, plastik ve metal gibi karışık ambalajlar 'Geri Kazanılabilir Atıklar' atık kutusuna atılmalıdır.",
                "Paper, glass, plastic, and metal packaging should be placed in the 'Recyclable Waste' bin."),
            new CategoryEntry(11, "Tibbi", "14",
                "Enfeksiyon yapıcı atıklar, patolojik atıklar ve kesici-delici atıklar 'Tıbbi Atık' atık kutusuna atılmalıdır.",
                "Infectious, pathological, and sharps waste should be placed in the 'Medical Waste' bin."),
            new CategoryEntry(12, "PlastikKapak", "9",
                "Şişe ve ambalaj kapakları 'Plastik Kapak' atık kutusuna atılmalıdır.",
                "Bottle and packaging caps should be placed in the 'Plastic Cap Waste' bin."),
        };

        // Maps a WasteDefinition's correct bin id to the Excel/sound number above.
        // Final 12-type scheme: Toner(7) merged into Electronic, Sigara(11) removed,
        // bin 12 is now Geri Kazanılabilir (sound 10), bin 15 is now Kompozit (sound 9).
        // Only bin 2 (Karton) has no recorded voice.
        private static readonly Dictionary<string, int> BinIdToIndex = new()
        {
            ["1"] = 1,   // Kağıt
            ["3"] = 2,   // Plastik
            ["4"] = 3,   // Cam
            ["10"] = 4,  // Metal
            ["5"] = 5,   // Bio-Bozunur (Cigarette de buraya düşer)
            ["6"] = 6,   // Pil
            ["8"] = 8,   // Elektronik (Cartridge de buraya düşer)
            ["9"] = 12,  // Plastik Kapak
            ["12"] = 10, // Geri Kazanılabilir
            ["14"] = 11, // Tıbbi
            ["15"] = 9,  // Kompozit
        };

        // Bins with no recorded voice. Only Karton (2) lacks a sound; it still gets the
        // explanation text from the Excel so the popup has something to show.
        private static readonly Dictionary<string, (string Tr, string En)> TextOnlyByBin = new()
        {
            // 2 = Karton
            ["2"] = (
                "Karton kutular, mukavva ambalajlar ve karton paketler 'Karton Atıklar' atık kutusuna atılmalıdır.",
                "Cardboard boxes, corrugated packaging and carton packets should be placed in the 'Cardboard Waste' bin."),
        };

        [MenuItem("Waste Collection/Build Waste Audio (Excel + Sounds)")]
        public static void BuildWasteAudio()
        {
            EnsureFolders();

            var selectByIndex = new Dictionary<int, LocalizedWasteSound>();
            var explanationByIndex = new Dictionary<int, LocalizedWasteSound>();

            foreach (CategoryEntry entry in Entries)
            {
                string prefix = $"{entry.Index:00}_{entry.Key}";

                SoundDefinition selTr = BuildSound(
                    $"{OutRoot}/Selection/{prefix}_Select_TR",
                    new[] { $"{SoundsRoot}/TR/{entry.Index}_tr.mp3" });
                SoundDefinition selEn = BuildSound(
                    $"{OutRoot}/Selection/{prefix}_Select_EN",
                    new[] { $"{SoundsRoot}/EN/{entry.Index}_en.mp3" });
                selectByIndex[entry.Index] = BuildLocalized(
                    $"{OutRoot}/Selection/{prefix}_Select", selTr, selEn);

                SoundDefinition expTr = BuildSound(
                    $"{OutRoot}/Explanation/{prefix}_Exp_TR",
                    new[] { $"{SoundsRoot}/TR/{entry.Index}_a_tr.mp3" });
                SoundDefinition expEn = BuildSound(
                    $"{OutRoot}/Explanation/{prefix}_Exp_EN",
                    new[] { $"{SoundsRoot}/EN/{entry.Index}_a_en.mp3" });
                explanationByIndex[entry.Index] = BuildLocalized(
                    $"{OutRoot}/Explanation/{prefix}_Exp", expTr, expEn);
            }

            SoundDefinition correctTr = BuildSound(
                $"{OutRoot}/Result/Correct/Correct_TR",
                new[]
                {
                    $"{SoundsRoot}/Selection/TR/Dogru_tr.mp3",
                    $"{SoundsRoot}/Selection/TR/harika_tr.mp3",
                    $"{SoundsRoot}/Selection/TR/mukemmel_tr.mp3",
                },
                ClipSelectionMode.RandomWeighted);
            SoundDefinition correctEn = BuildSound(
                $"{OutRoot}/Result/Correct/Correct_EN",
                new[]
                {
                    $"{SoundsRoot}/Selection/EN/dogru_en.mp3",
                    $"{SoundsRoot}/Selection/EN/harika_en.mp3",
                    $"{SoundsRoot}/Selection/EN/Mukemmel_en.mp3",
                },
                ClipSelectionMode.RandomWeighted);
            LocalizedWasteSound correct = BuildLocalized($"{OutRoot}/Result/Correct/Correct", correctTr, correctEn);

            SoundDefinition wrongTr = BuildSound(
                $"{OutRoot}/Result/Wrong/Wrong_TR",
                new[] { $"{SoundsRoot}/Selection/TR/yanlis_tr.mp3" });
            SoundDefinition wrongEn = BuildSound(
                $"{OutRoot}/Result/Wrong/Wrong_EN",
                new[] { $"{SoundsRoot}/Selection/EN/yanlis_en.mp3" });
            LocalizedWasteSound wrong = BuildLocalized($"{OutRoot}/Result/Wrong/Wrong", wrongTr, wrongEn);

            AssetDatabase.SaveAssets();

            string feedbackReport = AssignClassificationFeedback(correct, wrong);
            string definitionReport = AssignToWasteDefinitions(selectByIndex, explanationByIndex);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[WasteAudioBuildSetup] Waste audio built.\n{feedbackReport}\n{definitionReport}");
        }

        private static void EnsureFolders()
        {
            EnsureFolder($"{OutRoot}/Selection");
            EnsureFolder($"{OutRoot}/Explanation");
            EnsureFolder($"{OutRoot}/Result");
            EnsureFolder($"{OutRoot}/Result/Correct");
            EnsureFolder($"{OutRoot}/Result/Wrong");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int lastSlash = path.LastIndexOf('/');
            string parent = path.Substring(0, lastSlash);
            string leaf = path.Substring(lastSlash + 1);

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static SoundDefinition BuildSound(
            string assetPathNoExt,
            string[] clipPaths,
            ClipSelectionMode mode = ClipSelectionMode.Single)
        {
            string path = assetPathNoExt + ".asset";
            SoundDefinition sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
            bool isNew = sound == null;
            if (isNew)
                sound = ScriptableObject.CreateInstance<SoundDefinition>();

            sound.selectionMode = mode;
            sound.noImmediateRepeat = true;
            sound.spatialBlend = 0f; // 2D voice/UI line
            sound.volume = 1f;
            sound.clips = new List<ClipEntry>();

            foreach (string clipPath in clipPaths)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip == null)
                {
                    Debug.LogWarning($"[WasteAudioBuildSetup] Missing audio clip: {clipPath}");
                    continue;
                }

                sound.clips.Add(new ClipEntry { clip = clip, weight = 1f, delay = 0f });
            }

            if (isNew)
                AssetDatabase.CreateAsset(sound, path);
            else
                EditorUtility.SetDirty(sound);

            return sound;
        }

        private static LocalizedWasteSound BuildLocalized(
            string assetPathNoExt,
            SoundDefinition turkish,
            SoundDefinition english)
        {
            string path = assetPathNoExt + ".asset";
            LocalizedWasteSound localized = AssetDatabase.LoadAssetAtPath<LocalizedWasteSound>(path);
            bool isNew = localized == null;
            if (isNew)
                localized = ScriptableObject.CreateInstance<LocalizedWasteSound>();

            if (isNew)
                AssetDatabase.CreateAsset(localized, path);

            SerializedObject serialized = new SerializedObject(localized);
            serialized.FindProperty("turkish").objectReferenceValue = turkish;
            serialized.FindProperty("english").objectReferenceValue = english;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(localized);

            return localized;
        }

        private static string AssignClassificationFeedback(LocalizedWasteSound correct, LocalizedWasteSound wrong)
        {
            WasteAudioFeedback feedback = Object.FindFirstObjectByType<WasteAudioFeedback>();
            if (feedback == null)
            {
                WasteSelectionMenu menu = FindSelectionMenu();
                if (menu != null)
                    feedback = Undo.AddComponent<WasteAudioFeedback>(menu.gameObject);
            }

            if (feedback == null)
            {
                return "WasteAudioFeedback not found in scene — add the component and assign Correct/Wrong manually.";
            }

            SerializedObject serialized = new SerializedObject(feedback);
            serialized.FindProperty("correctSound").objectReferenceValue = correct;
            serialized.FindProperty("wrongSound").objectReferenceValue = wrong;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feedback);

            return $"Assigned Correct/Wrong to WasteAudioFeedback on '{feedback.gameObject.name}'.";
        }

        private static string AssignToWasteDefinitions(
            Dictionary<int, LocalizedWasteSound> selectByIndex,
            Dictionary<int, LocalizedWasteSound> explanationByIndex)
        {
            var entryByIndex = new Dictionary<int, CategoryEntry>();
            foreach (CategoryEntry entry in Entries)
                entryByIndex[entry.Index] = entry;

            var assigned = new List<string>();
            var textOnly = new List<string>();
            var skipped = new List<string>();

            string[] guids = AssetDatabase.FindAssets("t:WasteDefinition");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                WasteDefinition definition = AssetDatabase.LoadAssetAtPath<WasteDefinition>(path);
                if (definition == null)
                    continue;

                string binId = WasteBinCatalog.GetCorrectBinId(definition.Name, definition.Type);
                if (!BinIdToIndex.TryGetValue(binId, out int index))
                {
                    // No recorded voice for this bin; still fill the explanation text from Excel.
                    if (TextOnlyByBin.TryGetValue(binId, out (string Tr, string En) text))
                    {
                        SerializedObject textSerialized = new SerializedObject(definition);
                        textSerialized.FindProperty("explanationTurkish").stringValue = text.Tr;
                        textSerialized.FindProperty("explanationEnglish").stringValue = text.En;
                        textSerialized.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(definition);
                        textOnly.Add($"{definition.Name} (bin {binId}, text only — no voice)");
                    }
                    else
                    {
                        skipped.Add($"{definition.Name} (bin {binId})");
                    }

                    continue;
                }

                CategoryEntry entry = entryByIndex[index];
                SerializedObject serialized = new SerializedObject(definition);
                serialized.FindProperty("selectSound").objectReferenceValue = selectByIndex[index];
                serialized.FindProperty("explanationSound").objectReferenceValue = explanationByIndex[index];
                serialized.FindProperty("explanationTurkish").stringValue = entry.Tr;
                serialized.FindProperty("explanationEnglish").stringValue = entry.En;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);

                assigned.Add($"{definition.Name} → {index:00}_{entry.Key}");
            }

            var report = new StringBuilder();
            report.AppendLine($"WasteDefinitions assigned ({assigned.Count}): {string.Join(", ", assigned)}");
            if (textOnly.Count > 0)
                report.AppendLine($"Text only, no voice ({textOnly.Count}): {string.Join(", ", textOnly)}");
            if (skipped.Count > 0)
                report.AppendLine($"Skipped (no matching sound/text) ({skipped.Count}): {string.Join(", ", skipped)}");

            return report.ToString();
        }

        private static WasteSelectionMenu FindSelectionMenu()
        {
            WasteSelectionMenu[] menus = Resources.FindObjectsOfTypeAll<WasteSelectionMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                WasteSelectionMenu menu = menus[i];
                if (menu == null || EditorUtility.IsPersistent(menu))
                    continue;

                if (menu.gameObject.scene.IsValid())
                    return menu;
            }

            return null;
        }
    }
}
#endif
