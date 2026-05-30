namespace Woi.OfficeFire
{
    public enum KitchenCafeState
    {
        None = 0,
        WaitingForSmokeNotice = 1,
        WaitingForDoorOpen = 2,
        Intervention = 3,
        WaitingForExitRoom = 4,
        WaitingForAssemblyArea = 5,
        Completed = 6,
        WaitingForExtinguisherUse = 7,
        WaitingForPowerCut = 8,
    }
}
