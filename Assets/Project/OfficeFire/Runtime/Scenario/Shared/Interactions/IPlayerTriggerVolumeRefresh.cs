namespace Woi.OfficeFire
{
    /// <summary>
    /// Trigger volumes that must re-check overlaps after the player rig teleports into them.
    /// </summary>
    public interface IPlayerTriggerVolumeRefresh
    {
        void RefreshPlayerOverlap();
    }
}
