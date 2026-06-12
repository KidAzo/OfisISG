namespace Woi.OfficeFire
{
    public enum OfficeFireObjectiveId
    {
        None = 0,

        EvacuateBuilding = 10,
        GoToEmergencyExit = 11,
        GoToStairs = 12,
        GoToAssemblyArea = 13,
        PressAlarm = 14,
        HoldHandrail = 15,

        CheckArchiveRoom = 100,
        OpenArchiveDoor = 101,
        PressArchiveAlarm = 102,
        CutArchivePower = 103,
        UseArchiveExtinguisher = 104,
        ExitArchiveRoom = 105,

        CheckServerRoom = 200,
        EnterServerRoom = 201,
        ActivateServerSuppression = 202,
        EvacuateServerRoom = 203,
        LeaveServerRoom = 204,
        UseServerFireBlanket = 205,

        CheckKitchenArea = 300,
        GetFireBlanket = 301,
        PlaceFireBlanket = 302,
        TurnOffStove = 303,
        PressKitchenAlarm = 304,
        ExitKitchenArea = 305,
        ActivateKitchenSuppression = 306,
        LeaveKitchenCafe = 307,
        EnterKitchenCafe = 308,
        KitchenBlanketUsage = 309,
        KitchenWaterUsage = 310,
    }
}
