namespace Woi.OfficeFire
{
    public enum KitchenCafeState
    {
        None = 0,

        FireStarted = 1,
        Awareness = 2,
        NotNoticed = 3,
        KitchenDecision = 4,

        WaitingForBlanketPlacement = 5,

        PitcherWrongResult = 6,
        MovePanWrongResult = 7,
        ExtinguisherWrongResult = 8,
        ExtinguisherAcceptableResult = 9,
        BlanketWrongResult = 10,
        BlanketCorrectResult = 11,

        WaitingForStoveOff = 12,
        FireControlled = 13,
        AlarmAndEvacuation = 14,
        WaitingForAssemblyArea = 15,
        Completed = 16,
    }
}
