#nullable enable
using System;
using System.Collections.Generic;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Indexed compound entity query — <c>readonly struct</c>, up to 4
    /// predicates, dynamic driver selection. Returned by
    /// <see cref="EntityStore.With"/> / <see cref="EntityStore.Where{T}"/>
    /// and by scope-forwarded equivalents. Chains via <see cref="And"/> /
    /// <see cref="And{T}"/> — each predicate captures its indexed
    /// <c>HashSet&lt;int&gt;</c> so the enumerator can pick the smallest set
    /// as iteration driver, filtering the rest per-entity.
    ///
    /// <para><b>Why dynamic driver selection.</b> Author-written shapes
    /// tend toward broad-to-narrow (<c>With(IsMinion).And(Zone, Board)</c>);
    /// at Hearthstone scale that means the marker hits ~98% of the
    /// population, so iterating the first predicate is effectively a
    /// full-table scan. Picking the smallest set as driver — regardless
    /// of author order — was the bench's load-bearing optimization
    /// (1.15× → 10.3× LINQ at Hearthstone-sized populations). The other
    /// predicates become cheap per-entity checks.</para>
    ///
    /// <para>Compound depth caps at 4. Throws on the fifth <c>And</c>.
    /// All queries we can name in the slice top out at 3-deep; 4 is
    /// headroom. Escalation path (Friflo-style pre-built
    /// <c>QueryDefinition</c>) is on the shelf if depth ever bites.</para>
    /// </summary>
    public readonly struct EntityQuery
    {
        private readonly Dictionary<int, Entity> _entities;
        private readonly TagIndex _index;
        private readonly Predicate _p0, _p1, _p2, _p3;
        private readonly int _count;

        internal EntityQuery(Dictionary<int, Entity> entities, TagIndex index, Predicate p0)
        {
            _entities = entities; _index = index;
            _p0 = p0; _p1 = _p2 = _p3 = default;
            _count = 1;
        }

        private EntityQuery(Dictionary<int, Entity> entities, TagIndex index,
                            Predicate p0, Predicate p1, Predicate p2, Predicate p3, int count)
        {
            _entities = entities; _index = index;
            _p0 = p0; _p1 = p1; _p2 = p2; _p3 = p3;
            _count = count;
        }

        public EntityQuery And(TagKey<bool> marker)
        {
            _index.TryGetMarker(marker.Slot, out var set);
            return Append(new Predicate(marker.Slot, null, true, set ?? TagIndex.EmptySet));
        }

        public EntityQuery And<T>(TagKey<T> key, T value) where T : notnull
        {
            // Box once, reuse for bucket lookup AND predicate field — halves
            // per-And allocation on value-typed predicates (48 B/q gate).
            object boxed = value;
            _index.TryGetValueBucket(key.Slot, boxed, out var set);
            return Append(new Predicate(key.Slot, boxed, false, set));
        }

        private EntityQuery Append(Predicate p)
        {
            switch (_count)
            {
                case 1: return new EntityQuery(_entities, _index, _p0, p,   _p2, _p3, 2);
                case 2: return new EntityQuery(_entities, _index, _p0, _p1, p,   _p3, 3);
                case 3: return new EntityQuery(_entities, _index, _p0, _p1, _p2, p,   4);
                default: throw new InvalidOperationException("EntityQuery caps compound depth at 4.");
            }
        }

        public Enumerator GetEnumerator()
        {
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

        /// <summary>
        /// Materialise the query results to a fresh <see cref="List{T}"/>.
        /// Allocates — for tests / diagnostics / occasional "give me the
        /// whole set" call sites. Hot-path callers should <c>foreach</c>
        /// over the query directly to stay zero-alloc.
        /// </summary>
        public List<IEntity> ToList()
        {
            var list = new List<IEntity>();
            foreach (var e in this) list.Add(e);
            return list;
        }

        /// <summary>First match, or <c>null</c> if the query yields nothing.</summary>
        public IEntity? FirstOrDefault()
        {
            foreach (var e in this) return e;
            return null;
        }

        public readonly struct Predicate
        {
            public readonly int Slot;
            public readonly object? Expected;       // null when IsMarker
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
            private readonly Dictionary<int, Entity> _entities;
            private readonly Predicate _p0, _p1, _p2, _p3;
            private readonly int _count;
            private readonly int _driverIdx;
            private Entity? _current;

            internal Enumerator(HashSet<int>.Enumerator inner,
                                Dictionary<int, Entity> entities,
                                Predicate p0, Predicate p1, Predicate p2, Predicate p3,
                                int count, int driverIdx)
            {
                _inner = inner; _entities = entities;
                _p0 = p0; _p1 = p1; _p2 = p2; _p3 = p3;
                _count = count; _driverIdx = driverIdx;
                _current = null;
            }

            // Current returns IEntity (read-only view) so query results
            // from inside an Effect's Run body can't bypass the
            // dispatch-only-via-commands discipline. Callers that need
            // write access (rare — usually inside Command.Apply on a
            // specific entity from the payload) upgrade via
            // ICommandScope.Resolve or .Get.
            public IEntity Current => _current!;

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

            private static bool Match(Entity e, Predicate p)
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
