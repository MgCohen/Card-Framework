#nullable enable
using System.Collections.Generic;

namespace VerticalSlice.QueryBench.Model
{
    // Stand-in for production EventLog. Just enough work to model the
    // per-mutation log-append cost so the WithEvents store path is fair.
    public sealed class BenchEventLog
    {
        private readonly List<int> _events = new();
        public int Count => _events.Count;
        public void Append(int marker) => _events.Add(marker);
    }
}
