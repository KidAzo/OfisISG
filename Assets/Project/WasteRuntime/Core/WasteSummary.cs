using System.Collections.Generic;

public class WasteSummary
{
    public int TotalCount { get; set; }

    public IReadOnlyDictionary<WasteType, int> CountByType => countByType;

    private readonly Dictionary<WasteType, int> countByType = new();

    internal void SetCount(WasteType type, int count)
    {
        countByType[type] = count;
    }
}
