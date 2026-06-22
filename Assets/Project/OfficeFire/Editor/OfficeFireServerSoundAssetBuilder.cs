using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using WoiUtils.AudioSystem;

namespace Woi.OfficeFire.Editor
{
    public static class OfficeFireServerSoundAssetBuilder
    {
        const string ClipsTrRoot =
            "Assets/Project/OfficeFire/ScriptableObjects/ServerRoom/Sounds/TR";

        const string ClipsEnRoot =
            "Assets/Project/OfficeFire/ScriptableObjects/ServerRoom/Sounds/EN";

        const string SoRoot =
            "Assets/Project/OfficeFire/ScriptableObjects/ServerRoom/Sounds/SO";

        const string DatabasePath =
            "Assets/Project/OfficeFire/ScriptableObjects/ServerRoom/Content/ServerRoomScenarioContentDatabase.asset";

        sealed class SoundSetDefinition
        {
            public string FolderName;
            public int SoundIndex;
            public OfficeFireVoiceLineId[] VoiceLineIds;
            public string TurkishTitle;
            public string TurkishBody;
            public string EnglishTitle;
            public string EnglishBody;
        }

        static readonly SoundSetDefinition[] Definitions =
        {
            new SoundSetDefinition
            {
                FolderName = "IncidentDetected",
                SoundIndex = 1,
                VoiceLineIds = new[]
                {
                    OfficeFireVoiceLineId.ArchiveIncidentDetected,
                    OfficeFireVoiceLineId.ServerIncidentDetected,
                },
                TurkishTitle = "Olağan dışı durum",
                TurkishBody = "Sunucu odasını kontrol edin.",
                EnglishTitle = "Unusual situation",
                EnglishBody = "Inspect the server room.",
            },
            new SoundSetDefinition
            {
                FolderName = "ElectricalFireWarning",
                SoundIndex = 2,
                VoiceLineIds = new[]
                {
                    OfficeFireVoiceLineId.SmokeWarning,
                    OfficeFireVoiceLineId.ArchiveElectricalFireWarning,
                    OfficeFireVoiceLineId.ServerElectronicFireWarning,
                    OfficeFireVoiceLineId.ServerRoomEntered,
                },
                TurkishTitle = "Elektrik yangını",
                TurkishBody = "Ekipmanlara güvenli mesafede yaklaşın.",
                EnglishTitle = "Electrical fire",
                EnglishBody = "Keep a safe distance from equipment.",
            },
            new SoundSetDefinition
            {
                FolderName = "LeanInSmoke",
                SoundIndex = 3,
                VoiceLineIds = new[]
                {
                    OfficeFireVoiceLineId.LeanCorrectly,
                    OfficeFireVoiceLineId.CrouchInSmoke,
                },
                TurkishTitle = "Eğilin",
                TurkishBody = "Dumanın altında ilerleyin.",
                EnglishTitle = "Stay low",
                EnglishBody = "Move below the smoke.",
            },
            new SoundSetDefinition
            {
                FolderName = "ActivateAlarmSystem",
                SoundIndex = 4,
                VoiceLineIds = new[]
                {
                    OfficeFireVoiceLineId.ArchivePressAlarmInstruction,
                    OfficeFireVoiceLineId.ServerSuppressionInstruction,
                    OfficeFireVoiceLineId.ServerManualExtinguisherWarning,
                },
                TurkishTitle = "Alarmı devreye alın",
                TurkishBody = "Önce bina alarm sistemini aktive edin.",
                EnglishTitle = "Activate alarm",
                EnglishBody = "Activate the building fire alarm first.",
            },
            new SoundSetDefinition
            {
                FolderName = "WarnNearbyPeople",
                SoundIndex = 5,
                VoiceLineIds = new[] { OfficeFireVoiceLineId.AlarmInstruction },
                TurkishTitle = "Çevreyi uyarın",
                TurkishBody = "Alarmı aktive edip çevredekileri uyarın.",
                EnglishTitle = "Warn others",
                EnglishBody = "Activate the alarm and warn nearby people.",
            },
            new SoundSetDefinition
            {
                FolderName = "WaterOnElectricalFire",
                SoundIndex = 6,
                VoiceLineIds = new[]
                {
                    OfficeFireVoiceLineId.ArchiveWaterMistake,
                    OfficeFireVoiceLineId.ServerWaterMistake,
                },
                TurkishTitle = "Su kullanmayın",
                TurkishBody = "Elektrik yangınında su kullanılmaz.",
                EnglishTitle = "Do not use water",
                EnglishBody = "Do not use water on electrical fires.",
            },
            new SoundSetDefinition
            {
                FolderName = "UseCo2Extinguisher",
                SoundIndex = 7,
                VoiceLineIds = new[]
                {
                    OfficeFireVoiceLineId.ArchiveUseExtinguisherInstruction,
                    OfficeFireVoiceLineId.EstinguisherHandled,
                    OfficeFireVoiceLineId.ServerSuppressionCountdown,
                },
                TurkishTitle = "Söndürücü kullanın",
                TurkishBody = "Uygun yangın söndürücü ile güvenli müdahale edin.",
                EnglishTitle = "Use extinguisher",
                EnglishBody = "Respond safely with the appropriate extinguisher.",
            },
            new SoundSetDefinition
            {
                FolderName = "AimExtinguisher",
                SoundIndex = 8,
                VoiceLineIds = new[] { OfficeFireVoiceLineId.EstinguishingStarted },
                TurkishTitle = "Alevin kaynağına yönelin",
                TurkishBody = "Söndürücüyü kısa ve kontrollü uygulayın.",
                EnglishTitle = "Aim at fire source",
                EnglishBody = "Apply the extinguisher in short, controlled bursts.",
            },
            new SoundSetDefinition
            {
                FolderName = "CutPowerSource",
                SoundIndex = 9,
                VoiceLineIds = new[]
                {
                    OfficeFireVoiceLineId.ArchiveCutPowerInstruction,
                    OfficeFireVoiceLineId.ArchivePowerCutSuccess,
                },
                TurkishTitle = "Enerjiyi kesin",
                TurkishBody = "Güvenliyse enerji kaynağını kapatın.",
                EnglishTitle = "Cut power",
                EnglishBody = "Shut off the power source if it is safe.",
            },
            new SoundSetDefinition
            {
                FolderName = "FireSpreadingEvacuate",
                SoundIndex = 10,
                VoiceLineIds = new[]
                {
                    OfficeFireVoiceLineId.ServerFireGrowth,
                    OfficeFireVoiceLineId.ArchiveFireNotControlledEvacuate,
                    OfficeFireVoiceLineId.ArchiveFireGrowth,
                },
                TurkishTitle = "Derhal tahliye",
                TurkishBody = "Yangın büyüyorsa müdahaleyi bırakın.",
                EnglishTitle = "Evacuate now",
                EnglishBody = "Stop responding and evacuate immediately.",
            },
            new SoundSetDefinition
            {
                FolderName = "Co2ConcentrationWarning",
                SoundIndex = 11,
                VoiceLineIds = new[] { OfficeFireVoiceLineId.ServerGasActiveLeaveArea },
                TurkishTitle = "CO₂ uyarısı",
                TurkishBody = "Solunum riski nedeniyle alanı terk edin.",
                EnglishTitle = "CO₂ warning",
                EnglishBody = "Leave the area due to asphyxiation risk.",
            },
            new SoundSetDefinition
            {
                FolderName = "NearestEmergencyExit",
                SoundIndex = 12,
                VoiceLineIds = new[]
                {
                    OfficeFireVoiceLineId.EvacuationInstruction,
                    OfficeFireVoiceLineId.GoToAssemblyArea,
                },
                TurkishTitle = "Acil çıkış",
                TurkishBody = "En yakın acil çıkışa yönelin.",
                EnglishTitle = "Emergency exit",
                EnglishBody = "Proceed to the nearest emergency exit.",
            },
            new SoundSetDefinition
            {
                FolderName = "NearestEmergencyExitRepeat",
                SoundIndex = 13,
                VoiceLineIds = new[] { OfficeFireVoiceLineId.ExittedArchiveRoom },
                TurkishTitle = "Tahliye",
                TurkishBody = "En yakın acil çıkışa yönelin.",
                EnglishTitle = "Evacuate",
                EnglishBody = "Proceed to the nearest emergency exit.",
            },
            new SoundSetDefinition
            {
                FolderName = "UseHandrails",
                SoundIndex = 14,
                VoiceLineIds = new[] { OfficeFireVoiceLineId.ReachedExitDoor },
                TurkishTitle = "Korkulukları kullanın",
                TurkishBody = "Panik yapmadan sakin ilerleyin.",
                EnglishTitle = "Use handrails",
                EnglishBody = "Move calmly without panic.",
            },
            new SoundSetDefinition
            {
                FolderName = "AssemblyAreaSummary",
                SoundIndex = 15,
                VoiceLineIds = new[]
                {
                    OfficeFireVoiceLineId.ReachAssemblyArea,
                    OfficeFireVoiceLineId.ReachedAssemblyAreaDoor,
                },
                TurkishTitle = "Toplanma alanı",
                TurkishBody = "Toplanma alanına ulaştınız.",
                EnglishTitle = "Assembly area",
                EnglishBody = "You have reached the assembly area.",
            },
        };

        [MenuItem("Woi/Office Fire/Server/Create Server Sound SOs And Wire Database")]
        public static void CreateAndWire()
        {
            Dictionary<int, AudioClip> turkishClips = LoadClipsByIndex(ClipsTrRoot);
            Dictionary<int, AudioClip> englishClips = LoadClipsByIndex(ClipsEnRoot);
            int created = 0;
            int updated = 0;
            var localizedByFolder = new Dictionary<string, LocalizedSoundDefinition>();

            for (int i = 0; i < Definitions.Length; i++)
            {
                SoundSetDefinition definition = Definitions[i];
                if (!turkishClips.TryGetValue(definition.SoundIndex, out AudioClip turkishClip) ||
                    !englishClips.TryGetValue(definition.SoundIndex, out AudioClip englishClip))
                {
                    Debug.LogWarning(
                        $"[ServerSoundBuilder] Clip not found for index {definition.SoundIndex} " +
                        $"({definition.FolderName}).",
                        turkishClip);
                    continue;
                }

                string folderPath = $"{SoRoot}/{definition.FolderName}";
                EnsureAssetFolder(folderPath);

                SoundDefinition turkish = CreateOrUpdateSoundDefinition(
                    $"{folderPath}/{definition.FolderName}-TR.asset",
                    turkishClip,
                    isTurkish: true,
                    ref created,
                    ref updated);
                SoundDefinition english = CreateOrUpdateSoundDefinition(
                    $"{folderPath}/{definition.FolderName}-EN.asset",
                    englishClip,
                    isTurkish: false,
                    ref created,
                    ref updated);
                LocalizedSoundDefinition localized = CreateOrUpdateLocalizedDefinition(
                    $"{folderPath}/{definition.FolderName}LC-Server.asset",
                    english,
                    turkish,
                    ref created,
                    ref updated);

                localizedByFolder[definition.FolderName] = localized;
            }

            WireContentDatabase(localizedByFolder);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ServerSoundBuilder] Done. Created={created}, Updated={updated}.");
        }

        static Dictionary<int, AudioClip> LoadClipsByIndex(string root)
        {
            var map = new Dictionary<int, AudioClip>();
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { root });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileName = Path.GetFileNameWithoutExtension(path);
                Match match = Regex.Match(fileName, @"^(\d+)");
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out int index))
                {
                    continue;
                }

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    continue;
                }

                map[index] = clip;
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
                Debug.LogError($"[ServerSoundBuilder] Database not found at {DatabasePath}");
                return;
            }

            SerializedObject so = new SerializedObject(database);
            SerializedProperty entries = so.FindProperty("entries");
            if (entries == null || !entries.isArray)
            {
                Debug.LogError("[ServerSoundBuilder] Database entries array not found.");
                return;
            }

            for (int i = 0; i < Definitions.Length; i++)
            {
                SoundSetDefinition definition = Definitions[i];
                if (!localizedByFolder.TryGetValue(definition.FolderName, out LocalizedSoundDefinition localized) ||
                    localized == null)
                {
                    continue;
                }

                for (int v = 0; v < definition.VoiceLineIds.Length; v++)
                {
                    SetVoiceAndPopup(
                        entries,
                        definition.VoiceLineIds[v],
                        localized,
                        definition.TurkishTitle,
                        definition.TurkishBody,
                        definition.EnglishTitle,
                        definition.EnglishBody);
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        static void SetVoiceAndPopup(
            SerializedProperty entries,
            OfficeFireVoiceLineId voiceLineId,
            LocalizedSoundDefinition localized,
            string trTitle,
            string trBody,
            string enTitle,
            string enBody)
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

                SerializedProperty popupProp = entry.FindPropertyRelative("Popup");
                if (popupProp != null)
                {
                    popupProp.FindPropertyRelative("TurkishTitle").stringValue = trTitle;
                    popupProp.FindPropertyRelative("TurkishBody").stringValue = trBody;
                    popupProp.FindPropertyRelative("EnglishTitle").stringValue = enTitle;
                    popupProp.FindPropertyRelative("EnglishBody").stringValue = enBody;
                }

                return;
            }

            Debug.LogWarning($"[ServerSoundBuilder] Database entry not found for voice id {voiceId} ({voiceLineId}).");
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
