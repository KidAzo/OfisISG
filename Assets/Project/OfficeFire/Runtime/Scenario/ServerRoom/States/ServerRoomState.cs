namespace Woi.OfficeFire
{
    public enum ServerRoomState
    {
        None = 0,
        WaitingForSmokeNotice = 1,
        WaitingForEntry = 2,
        WaitingForSuppressionActivation = 3,
        WaitingForExitRoom = 4,
        WaitingForAssemblyArea = 5,
        Completed = 6,
    }
}
