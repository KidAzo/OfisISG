using UnityEngine;

[CreateAssetMenu(fileName = "WasteDefinition", menuName = "Waste Collection/Waste Definition")]
public class WasteDefinition : ScriptableObject
{
    public Waste waste;

    public string Name => waste != null ? waste.name : string.Empty;

    public WasteType Type => waste != null ? waste.type : default;
}
