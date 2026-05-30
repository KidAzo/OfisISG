namespace Woi.OfficeFire
{
    public enum OfficeFireCorrectActionId
    {
        None = 0,

        NoticedSmoke = 10,
        PressedAlarm = 11,
        EvacuatedSafely = 12,
        ReachedAssemblyArea = 13,

        OpenedArchiveDoor = 100,
        CutPower = 101,
        UsedExtinguisherCorrectly = 102,
        ControlledArchiveFire = 103,

        EnteredServerRoomSafely = 200,
        ActivatedSuppressionSystem = 201,
        LeftServerRoomBeforeGas = 202,
        ControlledServerFire = 203,

        SelectedFireBlanket = 300,
        PlacedFireBlanketCorrectly = 301,
        TurnedOffStove = 302,
        ControlledKitchenFire = 303,
        UsedExtinguisherControlled = 304,
        LeanedCorrectly = 305,
        ReachedExitDoor = 306,
        ExitedArchiveRoom = 307,
        EnteredKitchenCafeSafely = 308,
        LeftKitchenCafeBeforeGas = 309,
    }
}
