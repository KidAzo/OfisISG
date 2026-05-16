using System.Collections.Generic;
using FireExtinguisher.Core;

namespace Woi.Training
{
    /// <summary>
    /// <see cref="FireCriticalProximityVolume"/> oyuncu içerideyken sayaç tutar.
    /// HUD, proximity anonsu ve söndürme shake bu kaynağı okuyarak yangına fiziksel olarak uzak olsalar bile
    /// "kritik yakınlık" tepkisini tetikleyebilir.
    /// </summary>
    public static class ForcedCriticalProximityRegistry
    {
        static readonly Dictionary<int, int> CountByFireSourceId = new Dictionary<int, int>(8);

        /// <summary>Oyuncu en az bir zorunlu hacimde ve hedef yangın bu hacimlerden biriyle eşleşiyorsa true.</summary>
        public static bool IsForcedFor(FireSource fire)
        {
            if (fire == null)
                return false;

            int id = fire.GetInstanceID();
            return CountByFireSourceId.TryGetValue(id, out int n) && n > 0;
        }

        internal static void Increment(FireSource fire)
        {
            if (fire == null)
                return;

            int id = fire.GetInstanceID();
            CountByFireSourceId.TryGetValue(id, out int n);
            CountByFireSourceId[id] = n + 1;
        }

        internal static void Decrement(FireSource fire)
        {
            if (fire == null)
                return;

            int id = fire.GetInstanceID();
            if (!CountByFireSourceId.TryGetValue(id, out int n))
                return;

            n--;
            if (n <= 0)
                CountByFireSourceId.Remove(id);
            else
                CountByFireSourceId[id] = n;
        }
    }
}
