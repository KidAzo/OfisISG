using UnityEngine;

public class WasteCollectable : MonoBehaviour
{
    [SerializeField] private WasteDefinition wasteDefinition;

    public WasteDefinition Definition => wasteDefinition;
}
