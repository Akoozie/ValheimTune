using System.Collections.Generic;
using System.Linq;

namespace ValheimTune
{
    // Counts occurrences per int key and reports the top N. Used for "which prefabs churn".
    public sealed class ChurnTally
    {
        private readonly Dictionary<int, int> _counts = new Dictionary<int, int>();
        public int Total { get; private set; }

        public void Add(int key)
        {
            _counts.TryGetValue(key, out int n);
            _counts[key] = n + 1;
            Total++;
        }

        public List<KeyValuePair<int, int>> Top(int n) =>
            _counts.OrderByDescending(kv => kv.Value).Take(n).ToList();

        public void Reset()
        {
            _counts.Clear();
            Total = 0;
        }
    }
}
