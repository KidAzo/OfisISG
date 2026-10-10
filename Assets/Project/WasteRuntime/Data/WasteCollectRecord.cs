using System;
using UnityEngine;

[Serializable]
public class WasteCollectRecord
{
    public string wasteName;
    public WasteType wasteType;
    public float collectTime;
    public string sceneName;
    public Vector3 collectPosition;
}
