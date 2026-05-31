using System.Collections.Generic;

public static class WasteSummaryBuilder
{
    public static WasteSummary Build(IEnumerable<WasteCollectRecord> records)
    {
        var summary = new WasteSummary();

        if (records == null)
            return summary;

        var countByType = new Dictionary<WasteType, int>();

        foreach (WasteCollectRecord record in records)
        {
            if (record == null)
                continue;

            summary.TotalCount++;

            if (countByType.TryGetValue(record.wasteType, out int count))
                countByType[record.wasteType] = count + 1;
            else
                countByType[record.wasteType] = 1;
        }

        foreach (KeyValuePair<WasteType, int> pair in countByType)
            summary.SetCount(pair.Key, pair.Value);

        return summary;
    }
}
