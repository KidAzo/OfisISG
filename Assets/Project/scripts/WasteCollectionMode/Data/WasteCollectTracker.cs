using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tracks collected waste items for end-of-session reporting.
/// Place one instance in the scene and assign a WasteDatabase.
///
/// Integration example (call when an item is collected):
/// <code>
/// var wasteCollectable = collectedObject.GetComponent&lt;WasteCollectable&gt;();
/// if (wasteCollectable != null)
/// {
///     wasteCollectTracker.Collect(wasteCollectable);
/// }
/// </code>
///
/// End-of-game export example:
/// <code>
/// WasteCsvExporter.Export("waste_result.csv", wasteCollectTracker.Records);
/// </code>
/// </summary>
public class WasteCollectTracker : MonoBehaviour
{
    private static WasteCollectTracker active;

    [SerializeField] private WasteDatabase database;

    private readonly List<WasteCollectRecord> records = new();

    public IReadOnlyList<WasteCollectRecord> Records => records;

    public static bool TryGetActive(out WasteCollectTracker tracker)
    {
        tracker = active;
        return tracker != null;
    }

    private void OnEnable()
    {
        active = this;
    }

    private void OnDisable()
    {
        if (active == this)
            active = null;
    }

    public void Collect(WasteCollectable collectable)
    {
        if (collectable == null)
            return;

        WasteDefinition definition = collectable.Definition;
        if (definition == null)
        {
            Debug.LogWarning("[WasteCollectTracker] WasteDefinition is not assigned on collectable.", collectable);
            return;
        }

        if (database != null && !database.Contains(definition))
        {
            Debug.LogWarning(
                $"[WasteCollectTracker] WasteDefinition '{definition.Name}' is not registered in WasteDatabase.",
                this);
            return;
        }

        var record = new WasteCollectRecord
        {
            wasteName = definition.Name,
            wasteType = definition.Type,
            collectTime = Time.time,
            sceneName = SceneManager.GetActiveScene().name,
            collectPosition = collectable.transform.position
        };

        records.Add(record);
    }
}
