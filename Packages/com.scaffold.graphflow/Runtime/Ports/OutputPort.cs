#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Scaffold.GraphFlow
{
    public sealed class OutputPort<T> : Port
    {
        // Paired version-stamp + value, one slot per flow index. An entry is
        // fresh only when its Version matches the current flow.CacheVersion;
        // anything else is a miss and triggers recompute.
        internal struct Entry
        {
            public int Version;
            public T Value;
        }

        Entry[] _cache = Array.Empty<Entry>();
        readonly Func<Flow, T> _compute;
        readonly bool _shouldCache;

        public OutputPort(Func<Flow, T> compute, bool cache = true)
        {
            _compute = compute;
            _shouldCache = cache;
        }

        internal override void Bake(int maxFlows)
        {
            if (!_shouldCache) return;
            // Bake runs exactly once per port lifetime (GraphBuilder iterates
            // every port and dispatches once). A second call indicates the same
            // baked graph was wired into two runners, which the rest of the
            // builder pipeline already disallows.
            System.Diagnostics.Debug.Assert(_cache.Length == 0,
                "OutputPort.Bake called twice — same port shared across runners?");
            _cache = new Entry[maxFlows];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read(Flow flow)
        {
            if (!_shouldCache) return _compute(flow);

            ref var e = ref _cache[flow.Index];
            if (e.Version == flow.CacheVersion) return e.Value;
            e.Value = _compute(flow);
            e.Version = flow.CacheVersion;
            return e.Value;
        }
    }
}
