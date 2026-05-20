namespace Woi.OfficeFire
{
    public enum ArchiveRoomState
    {
        None = 0,
        WaitingForSmokeNotice = 1,
        WaitingForDoorOpen = 2,
        Intervention = 3,
        WaitingForPowerCut = 8,
        WaitingForExtinguisherUse = 7,
        WaitingForExitRoom = 4,
        WaitingForAssemblyArea = 5,
        Completed = 6,
    }
}
