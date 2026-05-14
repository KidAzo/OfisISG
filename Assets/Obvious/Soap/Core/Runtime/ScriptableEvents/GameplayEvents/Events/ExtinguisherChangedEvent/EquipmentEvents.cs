namespace Woi.Events
{
    /// <summary>
    /// Raised by <see cref="PlayerExtinguisherEquipment"/> whenever the equipped
    /// extinguisher slot changes — both on equip and on drop.
    /// </summary>
    /// <remarks>
    /// <see cref="Item"/> is the newly equipped item.
    /// When <see cref="Item"/> is <c>null</c> the slot has become empty (dropped).
    /// </remarks>
    public struct ExtinguisherChangedEvent
    {
        /// <summary>Display name of the equipped extinguisher, or empty when slot cleared.</summary>
        public string itemName;

        /// <summary>İkinci satır (ajan / kapasite metni); HUD’da boşsa alt satır gizlenir.</summary>
        public string subtitle;

        /// <summary>Normalized capacity 0–100, used for bar fill and color thresholds.</summary>
        public int capacity;
        /// <summary>Maximum absolute capacity in the same units as ConsumptionRate (e.g. kg).</summary>
        public float maxCapacity;
        public bool isSpraying;
        /// <summary>Remaining discharge time in seconds at current consumption rate.</summary>
        public float remainingTime;
        /// <summary>
        /// Whether the safety pin has been pulled for the equipped extinguisher.
        /// Spray is blocked until this is <c>true</c>.
        /// </summary>
        public bool pinPulled;
    }
}
