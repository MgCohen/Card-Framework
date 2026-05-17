#nullable enable
using System.Collections.Generic;
using VerticalSlice.QueryBench.Model;
using VerticalSlice.QueryBench.Queries;

namespace VerticalSlice.QueryBench.Stores
{
    // "Optimistic upper bound" variant. Index is maintained by direct
    // calls inside Set, bypassing entity events. Models a hypothetical
    // design that doesn't pay event-dispatch cost per mutation.
    public sealed class IndexedStore_Pure : IBenchStore
    {
        private readonly Dictionary<int, BenchEntity> _entities = new();
        private readonly TagIndex _index = new();
        private int _nextId = 1;

        public TagIndex Index => _index;
        public IReadOnlyDictionary<int, BenchEntity> Entities => _entities;

        public void IndexMarker(BenchTagKey<bool> key)     => _index.RegisterMarker(key.Slot);
        public void IndexBy<T>(BenchTagKey<T> key)         => _index.RegisterValue(key.Slot);

        public BenchEntity Create()
        {
            var e = new BenchEntity { Id = _nextId++ };
            _entities[e.Id] = e;
            return e;
        }

        public void Set<T>(BenchEntity e, BenchTagKey<T> key, T value)
        {
            // Boxed once into the dict; nothing further on the hot path.
            e.SetSlotRaw(key.Slot, value!, out var wasNew, out var prev);
            if (wasNew)
                _index.OnTagSet(e.Id, key.Slot, value!);
            else if (!EqualityComparer<object>.Default.Equals(prev, value))
                _index.OnTagChanged(e.Id, key.Slot, prev!, value!);
        }

        public void Destroy(BenchEntity e)
        {
            if (!_entities.Remove(e.Id)) return;
            _index.OnEntityDestroyed(e.Id, e.Slots);
        }

        public BenchEntity? Get(int id) => _entities.TryGetValue(id, out var e) ? e : null;

        // --- Query entry points -----------------------------------------

        public OptionBQuery With(BenchTagKey<bool> marker)
        {
            _index.TryGetMarker(marker.Slot, out var set);
            return new OptionBQuery(_entities, _index,
                new OptionBQuery.Predicate(marker.Slot, null, true, set ?? TagIndex.EmptySet));
        }

        public OptionBQuery Where<T>(BenchTagKey<T> key, T value) where T : notnull
        {
            object boxed = value;
            _index.TryGetValueBucket(key.Slot, boxed, out var set);
            return new OptionBQuery(_entities, _index,
                new OptionBQuery.Predicate(key.Slot, boxed, false, set));
        }
    }
}
