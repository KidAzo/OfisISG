using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Woi.OfficeFire
{
    [CreateAssetMenu(
        fileName = "KitchenCafeScenarioContentDatabase",
        menuName = "Woi/Office Fire/Kitchen Cafe Scenario Content Database")]
    public sealed class KitchenCafeScenarioContentDatabase : ScriptableObject
    {
        [Header("Popups")]
        [SerializeField]
        private List<KitchenCafePopupEntry> popupEntries = new List<KitchenCafePopupEntry>();

        [Header("Voices")]
        [SerializeField]
        private List<KitchenCafeVoiceEntry> voiceEntries = new List<KitchenCafeVoiceEntry>();

        [Header("Content Cues")]
        [SerializeField]
        private List<KitchenCafeContentCueEntry> cueEntries = new List<KitchenCafeContentCueEntry>();

        public bool TryGetPopupTurkish(KitchenCafePopupId id, out string title, out string body)
        {
            title = string.Empty;
            body = string.Empty;
            if (id == KitchenCafePopupId.None)
            {
                return false;
            }

            if (!TryFindPopupEntry(id, out KitchenCafePopupEntry entry))
            {
                Debug.LogWarning($"[KitchenCafeScenarioContentDatabase] Missing popup entry for id '{id}'.", this);
                return false;
            }

            if (entry.Text == null)
            {
                Debug.LogWarning($"[KitchenCafeScenarioContentDatabase] Popup '{id}' has no LocalizedPopupText.", this);
                return false;
            }

            title = entry.Text.GetTitleForTurkish() ?? string.Empty;
            body = entry.Text.GetBodyForTurkish() ?? string.Empty;
            return true;
        }

        public bool TryGetPopupEnglish(KitchenCafePopupId id, out string title, out string body)
        {
            title = string.Empty;
            body = string.Empty;
            if (id == KitchenCafePopupId.None)
            {
                return false;
            }

            if (!TryFindPopupEntry(id, out KitchenCafePopupEntry entry))
            {
                Debug.LogWarning($"[KitchenCafeScenarioContentDatabase] Missing popup entry for id '{id}'.", this);
                return false;
            }

            if (entry.Text == null)
            {
                Debug.LogWarning($"[KitchenCafeScenarioContentDatabase] Popup '{id}' has no LocalizedPopupText.", this);
                return false;
            }

            title = entry.Text.GetTitleForEnglish() ?? string.Empty;
            body = entry.Text.GetBodyForEnglish() ?? string.Empty;
            return true;
        }

        public bool TryGetVoiceClipTurkish(KitchenCafeVoiceId id, out AudioClip clip)
        {
            clip = null;
            if (id == KitchenCafeVoiceId.None)
            {
                return false;
            }

            if (!TryFindVoiceEntry(id, out KitchenCafeVoiceEntry entry))
            {
                Debug.LogWarning($"[KitchenCafeScenarioContentDatabase] Missing voice entry for id '{id}'.", this);
                return false;
            }

            if (entry.Clip == null)
            {
                Debug.LogWarning($"[KitchenCafeScenarioContentDatabase] Voice '{id}' has no LocalizedVoiceClip.", this);
                return false;
            }

            clip = entry.Clip.GetTurkishClip();
            if (clip == null)
            {
                Debug.LogWarning($"[KitchenCafeScenarioContentDatabase] Voice '{id}' has no Turkish AudioClip.", this);
                return false;
            }

            return true;
        }

        public bool TryGetVoiceClipEnglish(KitchenCafeVoiceId id, out AudioClip clip)
        {
            clip = null;
            if (id == KitchenCafeVoiceId.None)
            {
                return false;
            }

            if (!TryFindVoiceEntry(id, out KitchenCafeVoiceEntry entry))
            {
                Debug.LogWarning($"[KitchenCafeScenarioContentDatabase] Missing voice entry for id '{id}'.", this);
                return false;
            }

            if (entry.Clip == null)
            {
                Debug.LogWarning($"[KitchenCafeScenarioContentDatabase] Voice '{id}' has no LocalizedVoiceClip.", this);
                return false;
            }

            clip = entry.Clip.GetEnglishClip();
            if (clip == null)
            {
                Debug.LogWarning($"[KitchenCafeScenarioContentDatabase] Voice '{id}' has no English AudioClip.", this);
                return false;
            }

            return true;
        }

        public bool TryGetCue(KitchenCafeContentCueId id, out KitchenCafeContentCueEntry cue)
        {
            cue = null;
            if (id == KitchenCafeContentCueId.None)
            {
                return false;
            }

            if (cueEntries == null)
            {
                Debug.LogWarning($"[KitchenCafeScenarioContentDatabase] Missing cue entry for id '{id}'.", this);
                return false;
            }

            for (int i = 0; i < cueEntries.Count; i++)
            {
                KitchenCafeContentCueEntry e = cueEntries[i];
                if (e != null && e.Id == id)
                {
                    cue = e;
                    return true;
                }
            }

            Debug.LogWarning($"[KitchenCafeScenarioContentDatabase] Missing cue entry for id '{id}'.", this);
            return false;
        }

        private bool TryFindPopupEntry(KitchenCafePopupId id, out KitchenCafePopupEntry entry)
        {
            entry = null;
            if (popupEntries == null)
            {
                return false;
            }

            for (int i = 0; i < popupEntries.Count; i++)
            {
                KitchenCafePopupEntry e = popupEntries[i];
                if (e != null && e.Id == id)
                {
                    entry = e;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindVoiceEntry(KitchenCafeVoiceId id, out KitchenCafeVoiceEntry entry)
        {
            entry = null;
            if (voiceEntries == null)
            {
                return false;
            }

            for (int i = 0; i < voiceEntries.Count; i++)
            {
                KitchenCafeVoiceEntry e = voiceEntries[i];
                if (e != null && e.Id == id)
                {
                    entry = e;
                    return true;
                }
            }

            return false;
        }

        public bool HasPopupId(KitchenCafePopupId id)
        {
            return TryFindPopupEntry(id, out _);
        }

        public bool HasVoiceId(KitchenCafeVoiceId id)
        {
            return TryFindVoiceEntry(id, out _);
        }

        public bool HasCueId(KitchenCafeContentCueId id)
        {
            if (cueEntries == null)
            {
                return false;
            }

            for (int i = 0; i < cueEntries.Count; i++)
            {
                KitchenCafeContentCueEntry e = cueEntries[i];
                if (e != null && e.Id == id)
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        public void EditorEnsureAllDefaults()
        {
            FillDefaultKitchenPopupEntries();
            FillMissingKitchenVoiceEntries();
            FillMissingKitchenContentCueEntries();
        }

        [ContextMenu("Fill Default Kitchen Popup Entries")]
        private void FillDefaultKitchenPopupEntries()
        {
            if (popupEntries == null)
            {
                popupEntries = new List<KitchenCafePopupEntry>();
            }

            KitchenCafePopupId[] ids = (KitchenCafePopupId[])Enum.GetValues(typeof(KitchenCafePopupId));
            for (int i = 0; i < ids.Length; i++)
            {
                KitchenCafePopupId id = ids[i];
                if (id == KitchenCafePopupId.None)
                {
                    continue;
                }

                if (HasPopupId(id))
                {
                    continue;
                }

                LocalizedPopupText text = BuildDefaultPopupText(id);
                if (text == null)
                {
                    continue;
                }

                popupEntries.Add(new KitchenCafePopupEntry { Id = id, Text = text });
            }

            EditorUtility.SetDirty(this);
        }

        [ContextMenu("Fill Missing Kitchen Voice Entries")]
        private void FillMissingKitchenVoiceEntries()
        {
            if (voiceEntries == null)
            {
                voiceEntries = new List<KitchenCafeVoiceEntry>();
            }

            KitchenCafeVoiceId[] ids = (KitchenCafeVoiceId[])Enum.GetValues(typeof(KitchenCafeVoiceId));
            for (int i = 0; i < ids.Length; i++)
            {
                KitchenCafeVoiceId id = ids[i];
                if (id == KitchenCafeVoiceId.None)
                {
                    continue;
                }

                if (HasVoiceId(id))
                {
                    continue;
                }

                voiceEntries.Add(
                    new KitchenCafeVoiceEntry { Id = id, Clip = new LocalizedVoiceClip() });
            }

            EditorUtility.SetDirty(this);
        }

        [ContextMenu("Fill Missing Kitchen Content Cue Entries")]
        private void FillMissingKitchenContentCueEntries()
        {
            if (cueEntries == null)
            {
                cueEntries = new List<KitchenCafeContentCueEntry>();
            }

            KitchenCafeContentCueId[] ids = (KitchenCafeContentCueId[])Enum.GetValues(typeof(KitchenCafeContentCueId));
            for (int i = 0; i < ids.Length; i++)
            {
                KitchenCafeContentCueId id = ids[i];
                if (id == KitchenCafeContentCueId.None)
                {
                    continue;
                }

                if (HasCueId(id))
                {
                    continue;
                }

                GetDefaultCueMapping(id, out KitchenCafePopupId popupId, out KitchenCafeVoiceId voiceId);
                cueEntries.Add(
                    new KitchenCafeContentCueEntry
                    {
                        Id = id,
                        PopupId = popupId,
                        VoiceId = voiceId,
                        PopupDurationMode = PopupDurationMode.UseVoiceClipLength,
                        CustomPopupDuration = 3f,
                        StopPreviousVoice = true,
                        ClosePreviousPopup = true,
                    });
            }

            EditorUtility.SetDirty(this);
        }

        private static void GetDefaultCueMapping(
            KitchenCafeContentCueId id,
            out KitchenCafePopupId popupId,
            out KitchenCafeVoiceId voiceId)
        {
            switch (id)
            {
                case KitchenCafeContentCueId.FireRiskDetected:
                    popupId = KitchenCafePopupId.FireRiskDetected;
                    voiceId = KitchenCafeVoiceId.FireRiskDetected;
                    return;
                case KitchenCafeContentCueId.GoToKitchen:
                    popupId = KitchenCafePopupId.GoToKitchen;
                    voiceId = KitchenCafeVoiceId.GoToKitchen;
                    return;
                case KitchenCafeContentCueId.OilFireWarning:
                    popupId = KitchenCafePopupId.None;
                    voiceId = KitchenCafeVoiceId.OilFireWarning;
                    return;
                case KitchenCafeContentCueId.DecisionInstruction:
                    popupId = KitchenCafePopupId.DecisionInstruction;
                    voiceId = KitchenCafeVoiceId.DecisionInstruction;
                    return;
                case KitchenCafeContentCueId.WaterMistake:
                    popupId = KitchenCafePopupId.WaterMistake;
                    voiceId = KitchenCafeVoiceId.WaterMistake;
                    return;
                case KitchenCafeContentCueId.PanMoveMistake:
                    popupId = KitchenCafePopupId.PanMoveMistake;
                    voiceId = KitchenCafeVoiceId.PanMoveMistake;
                    return;
                case KitchenCafeContentCueId.ExtinguisherWarning:
                    popupId = KitchenCafePopupId.ExtinguisherWarning;
                    voiceId = KitchenCafeVoiceId.ExtinguisherWarning;
                    return;
                case KitchenCafeContentCueId.BlanketInstruction:
                    popupId = KitchenCafePopupId.BlanketInstruction;
                    voiceId = KitchenCafeVoiceId.BlanketInstruction;
                    return;
                case KitchenCafeContentCueId.BlanketFailed:
                    popupId = KitchenCafePopupId.BlanketFailed;
                    voiceId = KitchenCafeVoiceId.BlanketFailed;
                    return;
                case KitchenCafeContentCueId.BlanketSuccess:
                    popupId = KitchenCafePopupId.BlanketSuccess;
                    voiceId = KitchenCafeVoiceId.BlanketSuccess;
                    return;
                case KitchenCafeContentCueId.TurnOffStove:
                    popupId = KitchenCafePopupId.TurnOffStove;
                    voiceId = KitchenCafeVoiceId.TurnOffStoveInstruction;
                    return;
                case KitchenCafeContentCueId.FireControlled:
                    popupId = KitchenCafePopupId.FireControlled;
                    voiceId = KitchenCafeVoiceId.FireControlled;
                    return;
                case KitchenCafeContentCueId.FireGrowingEvacuate:
                    popupId = KitchenCafePopupId.FireGrowingEvacuate;
                    voiceId = KitchenCafeVoiceId.FireGrowingEvacuate;
                    return;
                case KitchenCafeContentCueId.PressAlarm:
                    popupId = KitchenCafePopupId.PressAlarm;
                    voiceId = KitchenCafeVoiceId.PressAlarmInstruction;
                    return;
                case KitchenCafeContentCueId.EvacuationInstruction:
                    popupId = KitchenCafePopupId.EvacuationInstruction;
                    voiceId = KitchenCafeVoiceId.EvacuationInstruction;
                    return;
                case KitchenCafeContentCueId.ReachAssemblyArea:
                    popupId = KitchenCafePopupId.ReachAssemblyArea;
                    voiceId = KitchenCafeVoiceId.ReachAssemblyArea;
                    return;
                case KitchenCafeContentCueId.ScenarioCompleted:
                    popupId = KitchenCafePopupId.None;
                    voiceId = KitchenCafeVoiceId.ScenarioCompleted;
                    return;
                default:
                    popupId = KitchenCafePopupId.None;
                    voiceId = KitchenCafeVoiceId.None;
                    return;
            }
        }

        private static LocalizedPopupText BuildDefaultPopupText(KitchenCafePopupId id)
        {
            switch (id)
            {
                case KitchenCafePopupId.FireRiskDetected:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Yangın Riski Algılandı",
                        TurkishBody =
                            "Mutfak alanında olağandışı duman ve alev belirtileri var. Alanı kontrol edin.",
                        EnglishTitle = "Fire Risk Detected",
                        EnglishBody =
                            "Unusual smoke and flame signs were detected in the kitchen area. Check the area.",
                    };
                case KitchenCafePopupId.GoToKitchen:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Mutfak Alanını Kontrol Et",
                        TurkishBody =
                            "Mutfakta yangın riski algılandı. Güvenli şekilde mutfak alanına ilerleyin.",
                        EnglishTitle = "Check the Kitchen Area",
                        EnglishBody =
                            "A fire risk was detected in the kitchen. Move to the kitchen area safely.",
                    };
                case KitchenCafePopupId.DecisionInstruction:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Doğru Müdahaleyi Seç",
                        TurkishBody =
                            "Yağ yangınlarında yanlış müdahale yangını büyütebilir. Uygun ekipmanı seçin.",
                        EnglishTitle = "Choose the Correct Response",
                        EnglishBody =
                            "Wrong actions can make oil fires worse. Choose the proper equipment.",
                    };
                case KitchenCafePopupId.WaterMistake:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Hatalı Müdahale",
                        TurkishBody =
                            "Yağ yangınlarında su kullanılmaz. Su, yanan yağın sıçramasına ve yangının büyümesine neden olabilir.",
                        EnglishTitle = "Wrong Action",
                        EnglishBody =
                            "Do not use water on oil fires. Water can spread burning oil and intensify the fire.",
                    };
                case KitchenCafePopupId.PanMoveMistake:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Hatalı Müdahale",
                        TurkishBody =
                            "Yanan tavayı taşımak yağın dökülmesine ve yangının yayılmasına neden olabilir.",
                        EnglishTitle = "Wrong Action",
                        EnglishBody =
                            "Moving a burning pan can spill hot oil and spread the fire.",
                    };
                case KitchenCafePopupId.ExtinguisherWarning:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Dikkat",
                        TurkishBody =
                            "Küçük yağ yangınlarında öncelikli yöntem yangın battaniyesiyle oksijen temasını kesmektir.",
                        EnglishTitle = "Warning",
                        EnglishBody =
                            "For small oil fires, the preferred method is to cut off oxygen using a fire blanket.",
                    };
                case KitchenCafePopupId.BlanketInstruction:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Yangın Battaniyesi",
                        TurkishBody =
                            "Battaniyeyi dikkatlice açın ve tavayı tamamen örtecek şekilde yerleştirin.",
                        EnglishTitle = "Fire Blanket",
                        EnglishBody =
                            "Open the blanket carefully and place it so it fully covers the pan.",
                    };
                case KitchenCafePopupId.BlanketFailed:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Battaniye Başarısız",
                        TurkishBody =
                            "Battaniye tavayı tamamen kapatmadı. Yangın büyüyor.",
                        EnglishTitle = "Blanket Failed",
                        EnglishBody =
                            "The blanket did not fully cover the pan. The fire is growing.",
                    };
                case KitchenCafePopupId.BlanketSuccess:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Doğru Müdahale",
                        TurkishBody =
                            "Battaniye alevin oksijenle temasını kesti. Yangın kontrol altına alınıyor.",
                        EnglishTitle = "Correct Response",
                        EnglishBody =
                            "The blanket cut off the oxygen supply. The fire is being controlled.",
                    };
                case KitchenCafePopupId.TurnOffStove:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Ocağı Kapat",
                        TurkishBody =
                            "Yangın kontrol altına alındı. Şimdi ocağın enerji kaynağını kapatın.",
                        EnglishTitle = "Turn Off the Stove",
                        EnglishBody =
                            "The fire is under control. Now turn off the stove's energy source.",
                    };
                case KitchenCafePopupId.FireControlled:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Yangın Kontrol Altında",
                        TurkishBody =
                            "Doğru müdahale sayesinde yangın kontrol altına alındı.",
                        EnglishTitle = "Fire Controlled",
                        EnglishBody =
                            "The fire was controlled thanks to the correct response.",
                    };
                case KitchenCafePopupId.FireGrowingEvacuate:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Yangın Büyüyor",
                        TurkishBody =
                            "Yangın kontrol altına alınamadı. Alarmı aktive edin ve alanı tahliye edin.",
                        EnglishTitle = "Fire Is Spreading",
                        EnglishBody =
                            "The fire could not be controlled. Activate the alarm and evacuate the area.",
                    };
                case KitchenCafePopupId.PressAlarm:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Alarmı Aktive Et",
                        TurkishBody =
                            "Yangın büyüyor. Çıkışa yakın alarm butonunu kullanın.",
                        EnglishTitle = "Activate the Alarm",
                        EnglishBody =
                            "The fire is spreading. Use the alarm button near the exit.",
                    };
                case KitchenCafePopupId.EvacuationInstruction:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Tahliye Et",
                        TurkishBody =
                            "Sakin olun ve en yakın güvenli çıkışa ilerleyin.",
                        EnglishTitle = "Evacuate",
                        EnglishBody =
                            "Stay calm and move toward the nearest safe exit.",
                    };
                case KitchenCafePopupId.ReachAssemblyArea:
                    return new LocalizedPopupText
                    {
                        TurkishTitle = "Toplanma Alanına Git",
                        TurkishBody =
                            "Binadan çıktıktan sonra belirlenen toplanma alanına ilerleyin.",
                        EnglishTitle = "Go to the Assembly Area",
                        EnglishBody =
                            "After leaving the building, proceed to the designated assembly area.",
                    };
                default:
                    return null;
            }
        }
#endif
    }
}
