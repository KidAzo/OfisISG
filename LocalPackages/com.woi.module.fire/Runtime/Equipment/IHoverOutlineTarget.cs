using UnityEngine;

namespace Woi.Game
{
    /// <summary>
    /// Pickups that should match hover rays through intervening geometry (same scan as extinguishers).
    /// </summary>
    public interface IHoverOutlineTarget
    {
        bool IsHoveredCollider(Transform hitTransform);
    }
}
