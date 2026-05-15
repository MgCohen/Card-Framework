#nullable enable
using System;
using System.Collections.Generic;
using VerticalSlice.QueryBench.Model;
using VerticalSlice.QueryBench.Stores;

namespace VerticalSlice.QueryBench.Queries
{
    // Option B (revised): readonly struct, up to 4 predicates, *dynamic
    // driver selection*. Each predicate carries its indexed HashSet so
    // the query picks the smallest set as driver at enumeration time and
    // filters by the rest. Author writes the natural broad-to-narrow
    // shape — the framework picks the fast path.
    //
    // First-draft Option B drove off the first predicate; the bench
    // showed that loses badly when markers (~98% selectivity) lead the
    // chain. Driver selection is the load-bearing optimization.
    public readonly struct OptionBQuery
    {
        private readonly Dictionary<int, BenchEntity> _entities;
        private readonly TagIndex _index;
        private readonly Predicate _p0, _p1, _p2, _p3;
        private readonly int _count;

        internal OptionBQuery(Dictionary<int, BenchEntity> entities, TagIndex index, Predicate p0)
        {
            _entities = entities;
            _index = index;
            _p0 = p0;
            _p1 = _p2 = _p3 = default;
            _count = 1;
        }

        private OptionBQuery(Dictionary<int, BenchEntity> entities, TagIndex index,
                             Predicate p0, Predicate p1, Predicate p2, Predicate p3, int count)
        {
            _entities = entities; _index = index;
            _p0 = p0; _p1 = p1; _p2 = p2; _p3 = p3;
            _count = count;
        }

        public OptionBQuery And(BenchTagKey<bool> marker)
        {
            _index.TryGetMarker(marker.Slot, out var set);
            return Append(new Predicate(marker.Slot, null, true, set ?? TagIndex.EmptySet));
        }

        public OptionBQuery And<T>(BenchTagKey<T> key, T value) where T : notnull
        {
            // Box once and reuse for both the bucket lookup and the predicate
            // field — halves the per-.And allocation on value-typed predicates.
            object boxed = value;
            _index.TryGetValueBucket(key.Slot, boxed, out var set);
            return Append(new Predicate(key.Slot, boxed, false, set));
        }

        private OptionBQuery Append(Predicate p)
        {
            switch (_count)
            {
                case 1: return new OptionBQuery(_entities, _index, _p0, p,   _p2, _p3, 2);
                case 2: return new OptionBQuery(_entities, _index, _p0, _p1, p,   _p3, 3);
                case 3: return new OptionBQuery(_entities, _index, _p0, _p1, _p2, p,   4);
                default: throw new InvalidOperationException("Option B caps compound depth at 4.");
            }
        }

        public Enumerator GetEnumerator()
        {
            // Pick the smallest indexed set as driver.
            int driverIdx = 0;
            int driverCount = _p0.IndexSet.Count;
            if (_count >= 2 && _p1.IndexSet.Count < driverCount) { driverIdx = 1; driverCount = _p1.IndexSet.Count; }
            if (_count >= 3 && _p2.IndexSet.Count < driverCount) { driverIdx = 2; driverCount = _p2.IndexSet.Count; }
            if (_count >= 4 && _p3.IndexSet.Count < driverCount) { driverIdx = 3; driverCount = _p3.IndexSet.Count; }

            var driver = driverIdx switch
            {
                0 => _p0.IndexSet,
                1 => _p1.IndexSet,
                2 => _p2.IndexSet,
                _ => _p3.IndexSet,
            };
            return new Enumerator(driver.GetEnumerator(), _entities, _p0, _p1, _p2, _p3, _count, driverIdx);
        }

        public readonly struct Predicate
        {
            public readonly int Slot;
            public readonly object? Expected;     // null when isMarker
            public readonly bool IsMarker;
            public readonly HashSet<int> IndexSet;

            public Predicate(int slot, object? expected, bool isMarker, HashSet<int> indexSet)
            {
                Slot = slot; Expected = expected; IsMarker = isMarker; IndexSet = indexSet;
            }
        }

        public struct Enumerator
        {
            private HashSet<int>.Enumerator _inner;
            private readonly Dictionary<int, BenchEntity> _entities;
            private readonly Predicate _p0, _p1, _p2, _p3;
            private readonly int _count;
            private readonly int _driverIdx;
            private BenchEntity? _current;

            internal Enumerator(HashSet<int>.Enumerator inner,
                                Dictionary<int, BenchEntity> entities,
                                Predicate p0, Predicate p1, Predicate p2, Predicate p3,
                                int count, int driverIdx)
            {
                _inner = inner; _entities = entities;
                _p0 = p0; _p1 = p1; _p2 = p2; _p3 = p3;
                _count = count;
                _driverIdx = driverIdx;
                _current = null;
            }

            public BenchEntity Current => _current!;

            public bool MoveNext()
            {
                while (_inner.MoveNext())
                {
                    if (!_entities.TryGetValue(_inner.Current, out var e)) continue;
                    if (_count >= 1 && _driverIdx != 0 && !Match(e, _p0)) continue;
                    if (_count >= 2 && _driverIdx != 1 && !Match(e, _p1)) continue;
                    if (_count >= 3 && _driverIdx != 2 && !Match(e, _p2)) continue;
                    if (_count >= 4 && _driverIdx != 3 && !Match(e, _p3)) continue;
                    _current = e;
                    return true;
                }
                _current = null;
                return false;
            }

            private static bool Match(BenchEntity e, Predicate p)
            {
                if (p.IsMarker)
                {
                    if (!e.TryGetSlot(p.Slot, out var raw)) return false;
                    return raw is bool b && b;
                }
                if (!e.TryGetSlot(p.Slot, out var v)) return false;
                return Equals(v, p.Expected);
            }
        }
    }
}
