using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WoiUtils.AudioSystem;

namespace Woi.OfficeFire.Editor
{
    public static class OfficeFireArchiveSoundAssetBuilder
    {
        const string ClipsRoot =
            "Assets/Project/OfficeFire/ScriptableObjects/ArchiveRoom/Content/Sounds/Clips";

        const string SoRoot =
            "Assets/Project/OfficeFire/ScriptableObjects/ArchiveRoom/Content/Sounds/SO";

        const string DatabasePath =
            "Assets/Project/OfficeFire/ScriptableObjects/ArchiveRoom/Content/ArchiveRoomScenarioContentDatabase.asset";

        sealed class SoundSetDefinition
        {
            public string FolderName;
            public string ClipBaseName;
            public OfficeFireVoiceLineId? VoiceLineId;
        }

        static readonly SoundSetDefinition[] Definitions =
        {
            new SoundSetDefinition { FolderName = "AlarmInstruction", ClipBaseName = "AlarmInstruction", VoiceLineId = OfficeFireVoiceLineId.AlarmInstruction },
            new SoundSetDefinition { FolderName = "AlarmPressInstruction", ClipBaseName = "AlarmInstruction", VoiceLineId = OfficeFireVoiceLineId.ArchivePressAlarmInstruction },
            new SoundSetDefinition { FolderName = "SmokeWarning", ClipBaseName = "SmokeWarning", VoiceLineId = OfficeFireVoiceLineId.SmokeWarning },
            new SoundSetDefinition { FolderName = "Lean", ClipBaseName = "Lean", VoiceLineId = OfficeFireVoiceLineId.LeanCorrectly },
            new SoundSetDefinition { FolderName = "DoNotUseElevator", ClipBaseName = "DontUseElevator", VoiceLineId = OfficeFireVoiceLineId.DoNotUseElevator },
            new SoundSetDefinition { FolderName = "DownStairs", ClipBaseName = "DontPanicWhenDown", VoiceLineId = null },
            new SoundSetDefinition { FolderName = "ElektrikCarpması", ClipBaseName = "ElektrikCarpması", VoiceLineId = OfficeFireVoiceLineId.ArchiveElectricalFireWarning },
            new SoundSetDefinition { FolderName = "EstinguisherHandled", ClipBaseName = "EstinguisherHandled", VoiceLineId = OfficeFireVoiceLineId.EstinguisherHandled },
            new SoundSetDefinition { FolderName = "EstinguishingStarted", ClipBaseName = "EstinguishingStarted", VoiceLineId = OfficeFireVoiceLineId.ArchiveUseExtinguisherInstruction },
            new SoundSetDefinition { FolderName = "FireCannotFixed", ClipBaseName = "FireCannotFixed", VoiceLineId = OfficeFireVoiceLineId.ArchiveFireNotControlledEvacuate },
            new SoundSetDefinition { FolderName = "FireFixed", ClipBaseName = "FireFixed", VoiceLineId = OfficeFireVoiceLineId.ArchiveFireControlled },
            new SoundSetDefinition { FolderName = "LeaveTheArea", ClipBaseName = "LeaveTheArea", VoiceLineId = OfficeFireVoiceLineId.EvacuationInstruction },
            new SoundSetDefinition { FolderName = "ReachedAssembly", ClipBaseName = "AssemblyEntered", VoiceLineId = OfficeFireVoiceLineId.GoToAssemblyArea },
        };

        [MenuItem("Woi/Office Fire/Archive/Create Archive Sound SOs And Wire Database")]
        public static void CreateAndWire()
        {
            Dictionary<string, AudioClip> clipsByBaseName = LoadTurkishClips();
            int created = 0;
            int updated = 0;
            var localizedByFolder = new Dictionary<string, LocalizedSoundDefinition>();

            for (int i = 0; i < Definitions.Length; i++)
            {
                SoundSetDefinition definition = Definitions[i];
                if (!clipsByBaseName.TryGetValue(definition.ClipBaseName, out AudioClip clip))
                {
                    Debug.LogWarning(
                        $"[ArchiveSoundBuilder] Clip not found for '{definition.FolderName}' " +
                        $"(expected '{definition.ClipBaseName}-TR').",
                        clip);
                    continue;
                }

                string folderPath = $"{SoRoot}/{definition.FolderName}";
                EnsureAssetFolder(folderPath);

                SoundDefinition turkish = CreateOrUpdateSoundDefinition(
                    $"{folderPath}/{definition.FolderName}-TR.asset",
                    clip,
                    isTurkish: true,
                    ref created,
                    ref updated);
                SoundDefinition english = CreateOrUpdateSoundDefinition(
                    $"{folderPath}/{definition.FolderName}-EN.asset",
                    clip,
                    isTurkish: false,
                    ref created,
                    ref updated);
                LocalizedSoundDefinition localized = CreateOrUpdateLocalizedDefinition(
                    $"{folderPath}/{definition.FolderName}LC-Archive.asset",
                    english,
                    turkish,
                    ref created,
                    ref updated);

                localizedByFolder[definition.FolderName] = localized;
            }

            WireContentDatabase(localizedByFolder);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArchiveSoundBuilder] Done. Created={created}, Updated={updated}.");
        }

        static Dictionary<string, AudioClip> LoadTurkishClips()
        {
            var map = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { ClipsRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (!fileName.EndsWith("-TR", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string baseName = fileName.Substring(0, fileName.Length - 3);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    continue;
                }

                map[baseName] = clip;
            }

            return map;
        }

        static SoundDefinition CreateOrUpdateSoundDefinition(
            string assetPath,
            AudioClip clip,
            bool isTurkish,
            ref int created,
            ref int updated)
        {
            SoundDefinition sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(assetPath);
            bool isNew = sound == null;
            if (isNew)
            {
                sound = ScriptableObject.CreateInstance<SoundDefinition>();
                AssetDatabase.CreateAsset(sound, assetPath);
                created++;
            }
            else
            {
                updated++;
            }

            sound.selectionMode = ClipSelectionMode.Single;
            sound.clips = new List<ClipEntry>
            {
                new ClipEntry { clip = clip, weight = 0f, delay = 0f },
            };
            sound.noImmediateRepeat = true;
            sound.instanceMode = isTurkish ? InstanceMode.SingleGlobal : InstanceMode.Multiple;
            sound.spatialBlend = 0f;
            sound.volume = 1f;
            sound.pitch = 1f;
            EditorUtility.SetDirty(sound);
            return sound;
        }

        static LocalizedSoundDefinition CreateOrUpdateLocalizedDefinition(
            string assetPath,
            SoundDefinition english,
            SoundDefinition turkish,
            ref int created,
            ref int updated)
        {
            LocalizedSoundDefinition localized = AssetDatabase.LoadAssetAtPath<LocalizedSoundDefinition>(assetPath);
            bool isNew = localized == null;
            if (isNew)
            {
                localized = ScriptableObject.CreateInstance<LocalizedSoundDefinition>();
                AssetDatabase.CreateAsset(localized, assetPath);
                created++;
            }
            else
            {
                updated++;
            }

            localized.english = english;
            localized.turkish = turkish;
            EditorUtility.SetDirty(localized);
            return localized;
        }

        static void WireContentDatabase(Dictionary<string, LocalizedSoundDefinition> localizedByFolder)
        {
            OfficeFireVoiceLineContentDatabase database =
                AssetDatabase.LoadAssetAtPath<OfficeFireVoiceLineContentDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogError($"[ArchiveSoundBuilder] Database not found at {DatabasePath}");
                return;
            }

            SerializedObject so = new SerializedObject(database);
            SerializedProperty entries = so.FindProperty("entries");
            if (entries == null || !entries.isArray)
            {
                Debug.LogError("[ArchiveSoundBuilder] Database entries array not found.");
                return;
            }

            for (int i = 0; i < Definitions.Length; i++)
            {
                SoundSetDefinition definition = Definitions[i];
                if (!definition.VoiceLineId.HasValue)
                {
                    continue;
                }

                if (!localizedByFolder.TryGetValue(definition.FolderName, out LocalizedSoundDefinition localized) ||
                    localized == null)
                {
                    continue;
                }

                SetVoiceReference(entries, definition.VoiceLineId.Value, localized);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        static void SetVoiceReference(
            SerializedProperty entries,
            OfficeFireVoiceLineId voiceLineId,
            LocalizedSoundDefinition localized)
        {
            int voiceId = (int)voiceLineId;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                SerializedProperty idProp = entry.FindPropertyRelative("Id");
                if (idProp == null || idProp.intValue != voiceId)
                {
                    continue;
                }

                SerializedProperty voiceProp = entry.FindPropertyRelative("Voice");
                if (voiceProp != null)
                {
                    voiceProp.objectReferenceValue = localized;
                }

                return;
            }

            Debug.LogWarning($"[ArchiveSoundBuilder] Database entry not found for voice id {voiceId} ({voiceLineId}).");
        }

        static void EnsureAssetFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(assetFolderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureAssetFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
