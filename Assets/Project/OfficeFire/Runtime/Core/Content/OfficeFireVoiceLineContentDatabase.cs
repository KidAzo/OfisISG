using System;
using System.Collections.Generic;
using UnityEngine;
using WoiUtils.AudioSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Woi.OfficeFire
{
    [CreateAssetMenu(
        fileName = "OfficeFireVoiceLineContentDatabase",
        menuName = "Woi/Office Fire/Voice Line Content Database")]
    public sealed class OfficeFireVoiceLineContentDatabase : ScriptableObject
    {
        [SerializeField]
        private OfficeFireScenarioId scenarioId;

        [SerializeField]
        private List<OfficeFireVoiceLineEntry> entries = new List<OfficeFireVoiceLineEntry>();

        public OfficeFireScenarioId ScenarioId => scenarioId;

        public bool TryGetPopupTurkish(OfficeFireVoiceLineId id, out string title, out string body)
        {
            title = string.Empty;
            body = string.Empty;
            if (!TryFindEntry(id, out OfficeFireVoiceLineEntry entry) || entry.Popup == null)
            {
                return false;
            }

            title = entry.Popup.TurkishTitle ?? string.Empty;
            body = entry.Popup.TurkishBody ?? string.Empty;
            return !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(body);
        }

        public bool TryGetPopupEnglish(OfficeFireVoiceLineId id, out string title, out string body)
        {
            title = string.Empty;
            body = string.Empty;
            if (!TryFindEntry(id, out OfficeFireVoiceLineEntry entry) || entry.Popup == null)
            {
                return false;
            }

            title = entry.Popup.EnglishTitle ?? string.Empty;
            body = entry.Popup.EnglishBody ?? string.Empty;
            return !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(body);
        }

        public bool TryGetLocalizedSound(OfficeFireVoiceLineId id, out LocalizedSoundDefinition sound)
        {
            sound = null;
            if (!TryFindEntry(id, out OfficeFireVoiceLineEntry entry) || entry.Voice == null)
            {
                return false;
            }

            sound = entry.Voice;
            return true;
        }

        private bool TryFindEntry(OfficeFireVoiceLineId id, out OfficeFireVoiceLineEntry entry)
        {
            entry = null;
            if (id == OfficeFireVoiceLineId.None || entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                OfficeFireVoiceLineEntry e = entries[i];
                if (e != null && e.Id == id)
                {
                    entry = e;
                    return true;
                }
            }

            return false;
        }

        public static bool BelongsToScenario(OfficeFireVoiceLineId id, OfficeFireScenarioId scenario)
        {
            if (id == OfficeFireVoiceLineId.None)
            {
                return false;
            }

            int value = (int)id;
            switch (scenario)
            {
                case OfficeFireScenarioId.ArchiveRoom:
                    return value >= 100 && value < 200 || IsSharedVoiceLine(id);
                case OfficeFireScenarioId.ServerRoom:
                    return value >= 200 && value < 300 || IsSharedVoiceLine(id);
                case OfficeFireScenarioId.KitchenCafe:
                    return value >= 300 && value < 400 || IsSharedVoiceLine(id);
                default:
                    return false;
            }
        }

        private static bool IsSharedVoiceLine(OfficeFireVoiceLineId id)
        {
            int value = (int)id;
            return value >= 10 && value < 100 || value >= 309 && value <= 316;
        }

#if UNITY_EDITOR
        public void EditorSetScenario(OfficeFireScenarioId scenario)
        {
            scenarioId = scenario;
            EditorUtility.SetDirty(this);
        }

        public bool EditorTryGetEntry(OfficeFireVoiceLineId id, out OfficeFireVoiceLineEntry entry)
        {
            return TryFindEntry(id, out entry);
        }

        public void EditorUpsertEntry(OfficeFireVoiceLineEntry source)
        {
            if (source == null || source.Id == OfficeFireVoiceLineId.None)
            {
                return;
            }

            if (entries == null)
            {
                entries = new List<OfficeFireVoiceLineEntry>();
            }

            if (TryFindEntry(source.Id, out OfficeFireVoiceLineEntry existing))
            {
                existing.Popup = ClonePopup(source.Popup);
                existing.Voice = source.Voice;
            }
            else
            {
                entries.Add(
                    new OfficeFireVoiceLineEntry
                    {
                        Id = source.Id,
                        Popup = ClonePopup(source.Popup),
                        Voice = source.Voice,
                    });
            }

            EditorUtility.SetDirty(this);
        }

        public void EditorRemoveDuplicateAndEmptyEntries()
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            var bestById = new Dictionary<OfficeFireVoiceLineId, OfficeFireVoiceLineEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                OfficeFireVoiceLineEntry entry = entries[i];
                if (entry == null || entry.Id == OfficeFireVoiceLineId.None)
                {
                    continue;
                }

                if (!bestById.TryGetValue(entry.Id, out OfficeFireVoiceLineEntry current) ||
                    EntryQuality(entry) > EntryQuality(current))
                {
                    bestById[entry.Id] = entry;
                }
            }

            entries.Clear();
            foreach (OfficeFireVoiceLineEntry entry in bestById.Values)
            {
                entries.Add(entry);
            }

            EditorUtility.SetDirty(this);
        }

        [ContextMenu("Fill Voice Lines For Assigned Scenario")]
        public void EditorFillForAssignedScenario()
        {
            if (entries == null)
            {
                entries = new List<OfficeFireVoiceLineEntry>();
            }

            EditorRemoveDuplicateAndEmptyEntries();

            OfficeFireVoiceLineId[] ids = (OfficeFireVoiceLineId[])Enum.GetValues(typeof(OfficeFireVoiceLineId));
            for (int i = 0; i < ids.Length; i++)
            {
                OfficeFireVoiceLineId id = ids[i];
                if (!BelongsToScenario(id, scenarioId))
                {
                    continue;
                }

                if (TryFindEntry(id, out _))
                {
                    continue;
                }

                entries.Add(
                    new OfficeFireVoiceLineEntry
                    {
                        Id = id,
                        Popup = BuildDefaultPopupText(id),
                    });
            }

            EditorUtility.SetDirty(this);
        }

        private static int EntryQuality(OfficeFireVoiceLineEntry entry)
        {
            if (entry == null)
            {
                return 0;
            }

            int score = 0;
            if (entry.Voice != null)
            {
                score += 4;
            }

            if (entry.Popup != null)
            {
                if (!string.IsNullOrWhiteSpace(entry.Popup.TurkishTitle))
                {
                    score += 1;
                }

                if (!string.IsNullOrWhiteSpace(entry.Popup.TurkishBody))
                {
                    score += 1;
                }

                if (!string.IsNullOrWhiteSpace(entry.Popup.EnglishTitle))
                {
                    score += 1;
                }

                if (!string.IsNullOrWhiteSpace(entry.Popup.EnglishBody))
                {
                    score += 1;
                }
            }

            return score;
        }

        private static OfficeFireVoiceLinePopupText ClonePopup(OfficeFireVoiceLinePopupText source)
        {
            if (source == null)
            {
                return BuildDefaultPopupText(OfficeFireVoiceLineId.None);
            }

            return new OfficeFireVoiceLinePopupText
            {
                TurkishTitle = source.TurkishTitle,
                TurkishBody = source.TurkishBody,
                EnglishTitle = source.EnglishTitle,
                EnglishBody = source.EnglishBody,
            };
        }

        private static OfficeFireVoiceLinePopupText BuildDefaultPopupText(OfficeFireVoiceLineId id)
        {
            string enTitle = id.ToString();
            string trTitle = enTitle;
            string enBody = "Assign a Localized Sound Definition and edit this text in the content database.";
            string trBody = "Localized Sound Definition atayıp bu metni content database üzerinden düzenleyin.";

            switch (id)
            {
                case OfficeFireVoiceLineId.SmokeWarning:
                    enTitle = "Smoke detected";
                    trTitle = "Duman algılandı";
                    enBody = "Check the area and follow safety instructions.";
                    trBody = "Alanı kontrol edin ve güvenlik talimatlarını uygulayın.";
                    break;
                case OfficeFireVoiceLineId.EvacuationInstruction:
                    enTitle = "Evacuate";
                    trTitle = "Tahliye";
                    enBody = "Leave the area using the nearest safe exit.";
                    trBody = "En yakın güvenli çıkışı kullanarak alanı terk edin.";
                    break;
                case OfficeFireVoiceLineId.ArchiveIncidentDetected:
                    enTitle = "Archive incident";
                    trTitle = "Arşiv olayı";
                    enBody = "Inspect the archive room.";
                    trBody = "Arşiv odasını kontrol edin.";
                    break;
                case OfficeFireVoiceLineId.ArchivePressAlarmInstruction:
                    enTitle = "Press the alarm";
                    trTitle = "Alarmı çalıştırın";
                    enBody = "Activate the fire alarm before other actions.";
                    trBody = "Diğer işlemlerden önce yangın alarmını devreye alın.";
                    break;
                case OfficeFireVoiceLineId.ServerWaterMistake:
                    enTitle = "Do not use water";
                    trTitle = "Su kullanmayın";
                    enBody = "Water is not safe on this type of fire.";
                    trBody = "Bu yangın türünde su kullanmak güvenli değildir.";
                    break;
                case OfficeFireVoiceLineId.ServerManualExtinguisherWarning:
                    enTitle = "Do not use extinguisher yet";
                    trTitle = "Henüz söndürücü kullanmayın";
                    enBody = "Activate the suppression system first.";
                    trBody = "Önce baskılama sistemini devreye alın.";
                    break;
                case OfficeFireVoiceLineId.LeanCorrectly:
                    enTitle = "Crouch";
                    trTitle = "Eğilin";
                    enBody = "Crouch to reduce smoke exposure.";
                    trBody = "Dumandan daha az etkilenmek için eğilmeniz gerekli.";
                    break;
                case OfficeFireVoiceLineId.EstinguisherHandled:
                    enTitle = "Extinguisher picked up";
                    trTitle = "Tüp alındı";
                    enBody = "Extinguisher picked up.";
                    trBody = "Söndürücü alındı.";
                    break;
                case OfficeFireVoiceLineId.EstinguishingStarted:
                    enTitle = "Extinguishing started";
                    trTitle = "Söndürme başladı";
                    enBody = "Use the extinguisher on the fire source.";
                    trBody = "Söndürücüyü yangın kaynağına yönelterek kullanın.";
                    break;
                case OfficeFireVoiceLineId.ReachAssemblyArea:
                    enTitle = "Go to assembly area";
                    trTitle = "Toplanma alanına gidin";
                    enBody = "Proceed to the designated assembly area.";
                    trBody = "Belirlenen toplanma alanına ilerleyin.";
                    break;
                case OfficeFireVoiceLineId.ReachedExitDoor:
                    enTitle = "Exit door reached";
                    trTitle = "Çıkış kapısına ulaşıldı";
                    enBody = "Continue to the assembly area.";
                    trBody = "Toplanma alanına devam edin.";
                    break;
                case OfficeFireVoiceLineId.ExittedArchiveRoom:
                case OfficeFireVoiceLineId.ServerGasActiveLeaveArea:
                    enTitle = "Leave the area";
                    trTitle = "Alanı terk edin";
                    enBody = "Leave the area for your safety.";
                    trBody = "Güvenliğiniz için alanı terk edin.";
                    break;
                case OfficeFireVoiceLineId.ArchiveFireGrowth:
                    enTitle = "Fire is spreading";
                    trTitle = "Yangın büyüyor";
                    enBody = "Leave the area immediately.";
                    trBody = "Alanı derhal terk edin.";
                    break;
                case OfficeFireVoiceLineId.ServerIncidentDetected:
                    enTitle = "Server room incident";
                    trTitle = "Sunucu odası olayı";
                    enBody = "Inspect the server room.";
                    trBody = "Sunucu odasını kontrol edin.";
                    break;
                case OfficeFireVoiceLineId.ServerElectronicFireWarning:
                    enTitle = "Electronic fire risk";
                    trTitle = "Elektronik yangın riski";
                    enBody = "Do not use water on electrical equipment.";
                    trBody = "Elektrikli ekipmanlarda su kullanmayın.";
                    break;
                case OfficeFireVoiceLineId.ServerSuppressionInstruction:
                    enTitle = "Activate suppression";
                    trTitle = "Baskılamayı devreye alın";
                    enBody = "Use the suppression system before manual extinguishing.";
                    trBody = "Manuel söndürmeden önce baskılama sistemini kullanın.";
                    break;
                case OfficeFireVoiceLineId.ServerSuppressionCountdown:
                    enTitle = "Suppression countdown";
                    trTitle = "Baskılama geri sayımı";
                    enBody = "Wait for the suppression cycle to complete.";
                    trBody = "Baskılama döngüsünün tamamlanmasını bekleyin.";
                    break;
                case OfficeFireVoiceLineId.ServerFireControlled:
                case OfficeFireVoiceLineId.ArchiveFireControlled:
                    enTitle = "Fire controlled";
                    trTitle = "Yangın kontrol altında";
                    enBody = "The fire has been brought under control.";
                    trBody = "Yangın kontrol altına alındı.";
                    break;
            }

            if (id == OfficeFireVoiceLineId.None)
            {
                return new OfficeFireVoiceLinePopupText();
            }

            return new OfficeFireVoiceLinePopupText
            {
                EnglishTitle = enTitle,
                EnglishBody = enBody,
                TurkishTitle = trTitle,
                TurkishBody = trBody,
            };
        }
#endif
    }
}
