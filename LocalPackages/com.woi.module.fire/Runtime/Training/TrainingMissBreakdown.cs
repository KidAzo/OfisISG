using System.Collections.Generic;
using FireExtinguisher.Core;

namespace Woi.Game.Training
{
    /// <summary>
    /// Miss tick counts by <see cref="SprayMissReason"/> (excludes <see cref="SprayMissReason.None"/>).
    /// </summary>
    public sealed class TrainingMissBreakdown
    {
        private readonly IReadOnlyDictionary<SprayMissReason, int> _counts;

        public TrainingMissBreakdown(IReadOnlyDictionary<SprayMissReason, int> counts)
        {
            _counts = counts ?? new Dictionary<SprayMissReason, int>();
        }

        public IReadOnlyDictionary<SprayMissReason, int> Counts => _counts;

        public int GetCount(SprayMissReason reason)
        {
            _counts.TryGetValue(reason, out int n);
            return n;
        }

        public int TotalMissTicks
        {
            get
            {
                int sum = 0;
                foreach (var kv in _counts)
                {
                    if (kv.Key != SprayMissReason.None)
                        sum += kv.Value;
                }

                return sum;
            }
        }
    }
}
