namespace Woi.OfficeFire
{
    public enum ArchiveRoomState
    {
        None = 0,
        WaitingForSmokeNotice = 1,
        WaitingForDoorOpen = 2,
        WaitingForAlarm = 3,
        WaitingForPowerCut = 4,
        WaitingForExtinguisherUse = 5,
        WaitingForExitRoom = 6,
        WaitingForAssemblyArea = 7,
        Completed = 8,
    }
}
