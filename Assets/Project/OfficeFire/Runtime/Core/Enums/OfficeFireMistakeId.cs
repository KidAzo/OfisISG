namespace Woi.OfficeFire
{
    public enum OfficeFireMistakeId
    {
        None = 0,

        DelayedReaction = 10,
        StoodInSmoke = 11,
        ReturnedToFireZone = 12,
        UsedElevator = 13,
        DelayedEvacuation = 14,
        DidNotHoldHandrail = 15,

        UsedWaterOnElectricalFire = 100,
        UsedExtinguisherBeforeAlarm = 101,
        UsedExtinguisherBeforePowerCut = 102,
        WrongExtinguisherDistance = 103,
        WrongExtinguisherAngle = 104,

        UsedWaterOnServerFire = 200,
        UsedManualExtinguisherBeforeSuppression = 201,
        StayedInsideDuringGasSuppression = 202,

        UsedWaterOnOilFire = 300,
        MovedBurningPan = 301,
        UsedExtinguisherTooCloseToOilFire = 302,
        UsedExtinguisherUncontrolled = 303,
        FailedToCoverPanWithBlanket = 304,
        ForgotToTurnOffStove = 305,
        UsedWaterOnKitchenFire = 306,
    }
}
