using UnityEngine;

public class NPCController : MonoBehaviour
{
    [SerializeField] private Vector3 tpOffset;

    public void TeleportTo()
    {
        Vector3 t = transform.position + tpOffset;
        transform.position = t;
    }
}
