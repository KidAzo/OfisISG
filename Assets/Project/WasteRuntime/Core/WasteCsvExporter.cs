using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class WasteCsvExporter
{
    private const string Header = "WasteName,WasteType,CollectTime,SceneName,PositionX,PositionY,PositionZ";

    public static string Export(string fileName, IEnumerable<WasteCollectRecord> records)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "waste_result.csv";

        string path = Path.Combine(Application.persistentDataPath, fileName);
        var builder = new StringBuilder();
        builder.AppendLine(Header);

        if (records != null)
        {
            foreach (WasteCollectRecord record in records)
            {
                if (record == null)
                    continue;

                Vector3 position = record.collectPosition;
                builder.Append(Escape(record.wasteName));
                builder.Append(',');
                builder.Append(Escape(record.wasteType.ToString()));
                builder.Append(',');
                builder.Append(record.collectTime.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(Escape(record.sceneName));
                builder.Append(',');
                builder.Append(position.x.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(position.y.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.AppendLine(position.z.ToString(CultureInfo.InvariantCulture));
            }
        }

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        Debug.Log($"[WasteCsvExporter] CSV exported to: {path}");
        return path;
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
