using System;
using UnityEngine;

[Serializable]
public class Waste 
{
    public string name;
    public WasteType type;
}

public enum WasteType
{
    Plastic = 0,
    Paper = 1,
    Glass = 2,
    Metal = 3,
    Organic = 4,
}
