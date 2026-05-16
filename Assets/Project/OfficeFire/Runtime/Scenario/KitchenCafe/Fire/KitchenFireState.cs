namespace Woi.OfficeFire
{
    public enum KitchenFireState
    {
        None,

        SmallPanFire,
        GrowingPanFire,
        Fireball,
        OilSpreadOnFloor,
        HoodSpread,

        SuppressedByBlanket,
        SuppressedByExtinguisher,

        Controlled,
        Uncontrolled,
    }
}
