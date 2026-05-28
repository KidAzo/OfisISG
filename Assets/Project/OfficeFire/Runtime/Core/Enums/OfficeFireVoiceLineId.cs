namespace Woi.OfficeFire
{
    public enum OfficeFireVoiceLineId
    {
        None = 0,

        SmokeWarning = 10,
        CrouchInSmoke = 11,
        AlarmInstruction = 12,
        EvacuationInstruction = 13,
        DoNotUseElevator = 14,
        GoToAssemblyArea = 15,
        ScenarioCompleted = 16,

        ArchiveIncidentDetected = 100,
        ArchiveElectricalFireWarning = 101,
        ArchiveWaterMistake = 102,
        ArchivePressAlarmInstruction = 103,
        ArchiveCutPowerInstruction = 104,
        ArchivePowerCutSuccess = 105,
        ArchiveUseExtinguisherInstruction = 106,
        ArchiveFireControlled = 107,
        ArchiveFireNotControlledEvacuate = 108,

        ServerIncidentDetected = 200,
        ServerElectronicFireWarning = 201,
        ServerWaterMistake = 202,
        ServerManualExtinguisherWarning = 203,
        ServerSuppressionInstruction = 204,
        ServerSuppressionCountdown = 205,
        ServerGasActiveLeaveArea = 206,
        ServerFireControlled = 207,

        KitchenIncidentDetected = 300,
        KitchenOilFireWarning = 301,
        KitchenWaterMistake = 302,
        KitchenPanMoveMistake = 303,
        KitchenExtinguisherWarning = 304,
        KitchenBlanketInstruction = 305,
        KitchenBlanketSuccess = 306,
        KitchenTurnOffStoveInstruction = 307,
        KitchenFireGrowingEvacuate = 308,
        LeanCorrectly = 309,
        EstinguisherHandled = 310,
        EstinguishingStarted = 311,
        ArchiveFireGrowth = 312,
        ExittedArchiveRoom = 313,
        ReachAssemblyArea = 314,
        ReachedExitDoor = 315,
        ReachedAssemblyAreaDoor = 316,
    }
}
