using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WasteDatabase", menuName = "Waste Collection/Waste Database")]
public class WasteDatabase : ScriptableObject
{
    [SerializeField] private List<WasteDefinition> wastes = new();

    public IReadOnlyList<WasteDefinition> Wastes => wastes;

    public WasteDefinition GetByName(string wasteName)
    {
        if (string.IsNullOrWhiteSpace(wasteName) || wastes == null)
            return null;

        for (int i = 0; i < wastes.Count; i++)
        {
            WasteDefinition definition = wastes[i];
            if (definition != null && definition.Name == wasteName)
                return definition;
        }

        return null;
    }

    public bool Contains(WasteDefinition definition)
    {
        if (definition == null || wastes == null)
            return false;

        return wastes.Contains(definition);
    }
}
