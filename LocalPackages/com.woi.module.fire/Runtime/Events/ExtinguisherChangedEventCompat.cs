using System.Reflection;

namespace Woi.Events
{
    /// <summary>
    /// Allows Fire package code to work when <see cref="ExtinguisherChangedEvent"/> in Obvious.Soap
    /// does not yet include a <c>subtitle</c> field (older HUB / fork mismatch).
    /// </summary>
    public static class ExtinguisherChangedEventCompat
    {
        private const BindingFlags FieldFlags = BindingFlags.Public | BindingFlags.Instance;

        public static string TryGetSubtitle(in ExtinguisherChangedEvent e)
        {
            object boxed = e;
            FieldInfo fi = boxed.GetType().GetField("subtitle", FieldFlags);
            return fi != null && fi.FieldType == typeof(string)
                ? (string)fi.GetValue(boxed) ?? string.Empty
                : string.Empty;
        }

        public static ExtinguisherChangedEvent CreateEquipped(
            string itemName,
            string subtitle,
            int capacity,
            float maxCapacity,
            float remainingTime,
            bool pinPulled)
        {
            var e = new ExtinguisherChangedEvent
            {
                itemName = itemName,
                capacity = capacity,
                maxCapacity = maxCapacity,
                remainingTime = remainingTime,
                isSpraying = false,
                pinPulled = pinPulled,
            };
            TryAssignSubtitle(ref e, subtitle);
            return e;
        }

        public static ExtinguisherChangedEvent CreateEmpty()
        {
            var e = new ExtinguisherChangedEvent
            {
                itemName = string.Empty,
                capacity = 0,
                remainingTime = 0f,
                isSpraying = false,
                pinPulled = false,
            };
            TryAssignSubtitle(ref e, string.Empty);
            return e;
        }

        private static void TryAssignSubtitle(ref ExtinguisherChangedEvent e, string subtitle)
        {
            object boxed = e;
            FieldInfo fi = boxed.GetType().GetField("subtitle", FieldFlags);
            if (fi != null && fi.FieldType == typeof(string))
            {
                fi.SetValue(boxed, subtitle ?? string.Empty);
                e = (ExtinguisherChangedEvent)boxed;
            }
        }
    }
}
