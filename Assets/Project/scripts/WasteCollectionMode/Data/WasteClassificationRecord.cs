using System;

[Serializable]
public class WasteClassificationRecord
{
    public string wasteName;
    public WasteType wasteType;
    public string selectedBinId;
    public string correctBinId;
    public bool isCorrect;
}
