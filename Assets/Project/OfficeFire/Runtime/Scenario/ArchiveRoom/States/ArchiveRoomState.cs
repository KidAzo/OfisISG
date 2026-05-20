namespace Woi.OfficeFire
{
    public enum ArchiveRoomState
    {
        None = 0,
        WaitingForSmokeNotice = 1,
        WaitingForDoorOpen = 2,
        Intervention = 3,
        WaitingForExitRoom = 4,
        WaitingForAssemblyArea = 5,
        Completed = 6,
    }
}
