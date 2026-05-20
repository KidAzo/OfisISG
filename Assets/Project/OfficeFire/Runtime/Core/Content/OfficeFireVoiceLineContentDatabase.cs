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
            return value >= 10 && value < 100;
        }

#if UNITY_EDITOR
        public void EditorSetScenario(OfficeFireScenarioId scenario)
        {
            scenarioId = scenario;
            EditorUtility.SetDirty(this);
        }

        [ContextMenu("Fill Voice Lines For Assigned Scenario")]
        public void EditorFillForAssignedScenario()
        {
            if (entries == null)
            {
                entries = new List<OfficeFireVoiceLineEntry>();
            }

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
