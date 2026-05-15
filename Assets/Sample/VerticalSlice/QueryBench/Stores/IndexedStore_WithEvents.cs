#nullable enable
using System.Collections.Generic;
using VerticalSlice.QueryBench.Model;
using VerticalSlice.QueryBench.Queries;

namespace VerticalSlice.QueryBench.Stores
{
    // Production-shape variant. Subscribes to entity TagSet / TagChanged
    // events and appends to a fake log on every mutation. Models the
    // dispatch + wire-event cost that the real EntityStore would carry.
    public sealed class IndexedStore_WithEvents : IBenchStore
    {
        private readonly Dictionary<int, BenchEntity> _entities = new();
        private readonly TagIndex _index = new();
        private readonly BenchEventLog _log = new();
        private int _nextId = 1;

        public TagIndex Index => _index;
        public BenchEventLog Log => _log;
        public IReadOnlyDictionary<int, BenchEntity> Entities => _entities;

        public void IndexMarker(BenchTagKey<bool> key)     => _index.RegisterMarker(key.Slot);
        public void IndexBy<T>(BenchTagKey<T> key)         => _index.RegisterValue(key.Slot);

        public BenchEntity Create()
        {
            var e = new BenchEntity { Id = _nextId++ };
            e.TagSet     += OnTagSet;
            e.TagChanged += OnTagChanged;
            _entities[e.Id] = e;
            _log.Append(e.Id);
            return e;
        }

        public void Set<T>(BenchEntity e, BenchTagKey<T> key, T value) => e.Set(key, value);

        public void Destroy(BenchEntity e)
        {
            if (!_entities.Remove(e.Id)) return;
            _index.OnEntityDestroyed(e.Id, e.SnapshotSlots());
            e.TagSet     -= OnTagSet;
            e.TagChanged -= OnTagChanged;
            _log.Append(-e.Id);
        }

        public BenchEntity? Get(int id) => _entities.TryGetValue(id, out var e) ? e : null;

        private void OnTagSet(BenchEntity e, int slot, object value)
        {
            _log.Append(slot);
            _index.OnTagSet(e.Id, slot, value);
        }

        private void OnTagChanged(BenchEntity e, int slot, object prev, object next)
        {
            _log.Append(slot);
            _index.OnTagChanged(e.Id, slot, prev, next);
        }

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
