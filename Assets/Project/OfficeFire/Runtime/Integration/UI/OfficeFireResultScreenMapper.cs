using System.Globalization;

namespace Woi.OfficeFire
{
    public static class OfficeFireResultScreenMapper
    {
        public static OfficeFireResultScreenModel FromReport(OfficeFireScenarioReport report, bool turkish)
        {
            OfficeFireResultScreenModel model = new OfficeFireResultScreenModel
            {
                Title = turkish ? "Eğitim Sonucu" : "Training Result",
                Subtitle = GetScenarioTitle(report?.scenarioId ?? OfficeFireScenarioId.None, turkish),
                ReactionTimeLabel = turkish ? "TEPKİ SÜRESİ" : "REACTION TIME",
                ReactionTimeValue = FormatReactionTime(report?.reactionTime ?? 0f),
                FireControlledLabel = turkish ? "YANGIN" : "FIRE",
                FireControlledValue = FormatBool(
                    report != null && report.fireControlled,
                    turkish ? "Kontrol Altına Alındı" : "Brought Under Control",
                    turkish ? "Kontrol Altına Alınmadı" : "Not Brought Under Control"),
                EvacuatedLabel = turkish ? "TAHLİYE" : "EVACUATION",
                EvacuatedValue = FormatBool(
                    report != null && report.evacuated,
                    turkish ? "Tamamlandı" : "Completed",
                    turkish ? "Tamamlanmadı" : "Not completed"),
                CorrectSectionTitle = turkish ? "Tamamlanan Görevler" : "Completed Objectives",
                MissingSectionTitle = turkish ? "Eksik Görevler" : "Missing Objectives",
                MistakesSectionTitle = turkish ? "Hatalar" : "Mistakes",
                EmptyCorrectText = turkish ? "Tamamlanan görev yok." : "No completed objectives.",
                EmptyMissingText = turkish ? "Eksik görev yok." : "No missing objectives.",
                EmptyMistakesText = turkish ? "Hata kaydedilmedi." : "No mistakes recorded.",
                ContinueButtonText = turkish ? "Devam" : "Continue",
            };

            if (report == null)
            {
                model.Passed = false;
                model.StatusLabel = turkish ? "İYİLEŞTİRME GEREKİYOR" : "NEEDS IMPROVEMENT";
                return model;
            }

            OfficeFireScenarioResultCatalog.EvaluateObjectives(
                report,
                model.CompletedObjectives,
                model.MissingObjectives,
                objectiveId => GetObjectiveLabel(objectiveId, turkish));

            for (int i = 0; i < report.mistakes.Count; i++)
            {
                model.Mistakes.Add(GetMistakeLabel(report.mistakes[i], turkish));
            }

            bool passed = model.MissingObjectives.Count == 0 && model.Mistakes.Count == 0;
            model.Passed = passed;
            model.StatusLabel = passed
                ? (turkish ? "BAŞARILI" : "PASSED")
                : (turkish ? "İYİLEŞTİRME GEREKİYOR" : "NEEDS IMPROVEMENT");

            return model;
        }

        public static string GetObjectiveLabel(OfficeFireObjectiveId id, bool turkish)
        {
            if (id == OfficeFireObjectiveId.None)
            {
                return string.Empty;
            }

            (string en, string tr) = id switch
            {
                OfficeFireObjectiveId.EvacuateBuilding => ("Evacuate the building", "Binayı tahliye edin"),
                OfficeFireObjectiveId.GoToEmergencyExit => ("Go to the emergency exit", "Acil çıkışa gidin"),
                OfficeFireObjectiveId.GoToStairs => ("Go to the stairs", "Merdivenlere gidin"),
                OfficeFireObjectiveId.GoToAssemblyArea => ("Go to the assembly area", "Toplanma alanına gidin"),
                OfficeFireObjectiveId.PressAlarm => ("Press the alarm", "Alarmı çalıştırın"),
                OfficeFireObjectiveId.CheckArchiveRoom => ("Inspect the archive room", "Arşiv odasını kontrol edin"),
                OfficeFireObjectiveId.OpenArchiveDoor => ("Open the archive door", "Arşiv kapısını açın"),
                OfficeFireObjectiveId.PressArchiveAlarm => ("Press the alarm", "Alarmı çalıştırın"),
                OfficeFireObjectiveId.CutArchivePower => ("Cut archive power", "Arşiv elektriğini kesin"),
                OfficeFireObjectiveId.UseArchiveExtinguisher => ("Use the extinguisher", "Söndürücüyü kullanın"),
                OfficeFireObjectiveId.ExitArchiveRoom => ("Exit the archive room", "Arşiv odasından çıkın"),
                OfficeFireObjectiveId.CheckServerRoom => ("Inspect the server room", "Sunucu odasını kontrol edin"),
                OfficeFireObjectiveId.EnterServerRoom => ("Enter the server room", "Sunucu odasına girin"),
                OfficeFireObjectiveId.ActivateServerSuppression => ("Activate suppression system", "Söndürme sistemini devreye alın"),
                OfficeFireObjectiveId.EvacuateServerRoom => ("Evacuate the server room", "Sunucu odasını tahliye edin"),
                OfficeFireObjectiveId.LeaveServerRoom => ("Leave the server room", "Sunucu odasından çıkın"),
                OfficeFireObjectiveId.UseServerFireBlanket => ("Use the fire blanket", "Yangın battaniyesini kullanın"),
                OfficeFireObjectiveId.CheckKitchenArea => ("Check the kitchen area", "Mutfak alanını kontrol edin"),
                OfficeFireObjectiveId.EnterKitchenCafe => ("Enter the kitchen area", "Mutfak alanına girin"),
                OfficeFireObjectiveId.GetFireBlanket => ("Get the fire blanket", "Yangın battaniyesini alın"),
                OfficeFireObjectiveId.PlaceFireBlanket => ("Place the fire blanket", "Yangın battaniyesini yerleştirin"),
                OfficeFireObjectiveId.TurnOffStove => ("Turn off the stove", "Ocağı kapatın"),
                OfficeFireObjectiveId.PressKitchenAlarm => ("Press the kitchen alarm", "Mutfak alarmını çalıştırın"),
                OfficeFireObjectiveId.ExitKitchenArea => ("Exit the kitchen area", "Mutfak alanından çıkın"),
                OfficeFireObjectiveId.ActivateKitchenSuppression => ("Activate suppression system", "Söndürme sistemini devreye alın"),
                OfficeFireObjectiveId.LeaveKitchenCafe => ("Leave the kitchen area", "Mutfak alanından çıkın"),
                OfficeFireObjectiveId.KitchenBlanketUsage => ("Fire blanket usage", "Battaniye kullanımı"),
                OfficeFireObjectiveId.KitchenWaterUsage => ("Water usage", "Su kullanımı"),
                _ => (id.ToString(), id.ToString()),
            };

            return Pick(en, tr, turkish);
        }

        public static string GetScenarioTitle(OfficeFireScenarioId scenarioId, bool turkish)
        {
            return scenarioId switch
            {
                OfficeFireScenarioId.ArchiveRoom => turkish ? "Arşiv Odası Senaryosu" : "Archive Room Scenario",
                OfficeFireScenarioId.ServerRoom => turkish ? "Sunucu Odası Senaryosu" : "Server Room Scenario",
                OfficeFireScenarioId.KitchenCafe => turkish ? "Mutfak / Kafe Senaryosu" : "Kitchen / Cafe Scenario",
                _ => turkish ? "Ofis Yangın Eğitimi" : "Office Fire Training",
            };
        }

        public static string GetCorrectActionLabel(OfficeFireCorrectActionId id, bool turkish)
        {
            if (id == OfficeFireCorrectActionId.None)
            {
                return string.Empty;
            }

            (string en, string tr) = id switch
            {
                OfficeFireCorrectActionId.NoticedSmoke => ("Noticed smoke", "Duman fark edildi"),
                OfficeFireCorrectActionId.PressedAlarm => ("Pressed the alarm", "Alarm basıldı"),
                OfficeFireCorrectActionId.EvacuatedSafely => ("Evacuated safely", "Güvenli tahliye"),
                OfficeFireCorrectActionId.ReachedAssemblyArea => ("Reached assembly area", "Toplanma alanına ulaşıldı"),
                OfficeFireCorrectActionId.OpenedArchiveDoor => ("Opened archive door", "Arşiv kapısı açıldı"),
                OfficeFireCorrectActionId.CutPower => ("Cut electrical power", "Elektrik kesildi"),
                OfficeFireCorrectActionId.UsedExtinguisherCorrectly => ("Used extinguisher correctly", "Söndürücü doğru kullanıldı"),
                OfficeFireCorrectActionId.ControlledArchiveFire => ("Controlled archive fire", "Arşiv yangını kontrol altına alındı"),
                OfficeFireCorrectActionId.GrabbedExtinguisher => ("Grabbed extinguisher", "Söndürücü alındı"),
                OfficeFireCorrectActionId.EnteredServerRoomSafely => ("Entered server room safely", "Sunucu odasına güvenli giriş"),
                OfficeFireCorrectActionId.ActivatedSuppressionSystem => ("Activated suppression system", "Söndürme sistemi devreye alındı"),
                OfficeFireCorrectActionId.LeftServerRoomBeforeGas => ("Left server room before gas release", "Gaz salınımından önce odadan çıkıldı"),
                OfficeFireCorrectActionId.ControlledServerFire => ("Controlled server fire", "Sunucu yangını kontrol altına alındı"),
                OfficeFireCorrectActionId.SelectedFireBlanket => ("Selected fire blanket", "Yangın battaniyesi alındı"),
                OfficeFireCorrectActionId.PlacedFireBlanketCorrectly => ("Placed fire blanket correctly", "Yangın battaniyesi doğru yerleştirildi"),
                OfficeFireCorrectActionId.TurnedOffStove => ("Turned off the stove", "Ocak kapatıldı"),
                OfficeFireCorrectActionId.ControlledKitchenFire => ("Controlled kitchen fire", "Mutfak yangını kontrol altına alındı"),
                OfficeFireCorrectActionId.UsedExtinguisherControlled => ("Used extinguisher in a controlled way", "Söndürücü kontrollü kullanıldı"),
                OfficeFireCorrectActionId.LeanedCorrectly => ("Leaned correctly in smoke", "Duman içinde doğru eğilme"),
                OfficeFireCorrectActionId.ReachedExitDoor => ("Reached exit door", "Çıkış kapısına ulaşıldı"),
                OfficeFireCorrectActionId.ExitedArchiveRoom => ("Exited archive room", "Arşiv odasından çıkıldı"),
                OfficeFireCorrectActionId.EnteredKitchenCafeSafely => ("Entered kitchen safely", "Mutfak alanına güvenli giriş"),
                OfficeFireCorrectActionId.LeftKitchenCafeBeforeGas => ("Left kitchen before gas release", "Gaz salınımından önce mutfaktan çıkıldı"),
                _ => (id.ToString(), id.ToString()),
            };

            return Pick(en, tr, turkish);
        }

        public static string GetMistakeLabel(OfficeFireMistakeId id, bool turkish)
        {
            if (id == OfficeFireMistakeId.None)
            {
                return string.Empty;
            }

            (string en, string tr) = id switch
            {
                OfficeFireMistakeId.DelayedReaction => ("Delayed reaction", "Geç tepki"),
                OfficeFireMistakeId.StoodInSmoke => ("Stood in smoke", "Duman içinde ayakta kalındı"),
                OfficeFireMistakeId.ReturnedToFireZone => ("Returned to fire zone", "Yangın bölgesine geri dönüldü"),
                OfficeFireMistakeId.UsedElevator => ("Used elevator during evacuation", "Tahliyede asansör kullanıldı"),
                OfficeFireMistakeId.DelayedEvacuation => ("Delayed evacuation", "Tahliye geciktirildi"),
                OfficeFireMistakeId.UsedWaterOnElectricalFire => ("Used water on electrical fire", "Elektrik yangınına su kullanıldı"),
                OfficeFireMistakeId.UsedExtinguisherBeforeAlarm => ("Used extinguisher before alarm", "Alarmdan önce söndürücü kullanıldı"),
                OfficeFireMistakeId.UsedExtinguisherBeforePowerCut => ("Used extinguisher before power cut", "Elektrik kesilmeden söndürücü kullanıldı"),
                OfficeFireMistakeId.WrongExtinguisherDistance => ("Wrong extinguisher distance", "Yanlış söndürücü mesafesi"),
                OfficeFireMistakeId.WrongExtinguisherAngle => ("Wrong extinguisher angle", "Yanlış söndürücü açısı"),
                OfficeFireMistakeId.UsedWaterOnServerFire => ("Used water on server fire", "Sunucu yangınına su kullanıldı"),
                OfficeFireMistakeId.UsedWaterOnKitchenFire => ("Used water on kitchen fire", "Mutfak yangınına su kullanıldı"),
                OfficeFireMistakeId.UsedManualExtinguisherBeforeSuppression => ("Used manual extinguisher before suppression", "Sistem devreye alınmadan manuel söndürücü kullanıldı"),
                OfficeFireMistakeId.StayedInsideDuringGasSuppression => ("Stayed inside during gas suppression", "Gazlı söndürme sırasında içeride kalındı"),
                OfficeFireMistakeId.UsedWaterOnOilFire => ("Used water on oil fire", "Yağ yangınına su kullanıldı"),
                OfficeFireMistakeId.MovedBurningPan => ("Moved burning pan", "Yanan tencere taşındı"),
                OfficeFireMistakeId.UsedExtinguisherTooCloseToOilFire => ("Used extinguisher too close to oil fire", "Yağ yangınına çok yakın söndürücü kullanıldı"),
                OfficeFireMistakeId.UsedExtinguisherUncontrolled => ("Used extinguisher uncontrolled", "Söndürücü kontrolsüz kullanıldı"),
                OfficeFireMistakeId.FailedToCoverPanWithBlanket => ("Failed to cover pan with blanket", "Tencere battaniye ile kapatılamadı"),
                OfficeFireMistakeId.ForgotToTurnOffStove => ("Forgot to turn off stove", "Ocak kapatılmadı"),
                _ => (id.ToString(), id.ToString()),
            };

            return Pick(en, tr, turkish);
        }

        private static string Pick(string english, string turkish, bool useTurkish)
        {
            return useTurkish ? turkish : english;
        }

        private static string FormatReactionTime(float seconds)
        {
            if (seconds <= 0.01f)
            {
                return "—";
            }

            return seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }

        public static string FormatReactionTimeForExport(float seconds)
        {
            if (seconds <= 0.01f)
            {
                return "-";
            }

            return seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }

        private static string FormatBool(bool value, string yes, string no)
        {
            return value ? yes : no;
        }
    }
}
