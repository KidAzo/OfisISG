using UnityEngine;

/// <summary>
/// VR'de tüpün kuşanılacağı (equip edileceği) elin (genellikle sol kontrolcü) transformunu kaydeder.
/// </summary>
public static class FireVrGameplayEquipAnchor
{
    static Transform s_EquipAnchor;
    static object s_RegisteredOwner;

    public static void Register(object owner, Transform equipAnchor)
    {
        if (owner == null || equipAnchor == null)
            return;

        s_RegisteredOwner = owner;
        s_EquipAnchor = equipAnchor;
    }

    public static void Unregister(object owner)
    {
        if (owner == null || s_RegisteredOwner != owner)
            return;

        s_RegisteredOwner = null;
        s_EquipAnchor = null;
    }

    public static Transform RegisteredAnchorOrNull => s_EquipAnchor;
}
