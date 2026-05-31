using System;

[Serializable]
public class WasteClassificationRecord
{
    public string wasteName;
    public WasteType wasteType;
    public string selectedBinId;
    public string correctBinId;
    public string selectedBinName;
    public string correctBinName;
    public bool isCorrect;
}
