#nullable enable
using System;
using System.Collections.Generic;

namespace VerticalSlice.QueryBench.Model
{
    // Mirrors production Entity shape on purpose:
    //  - sparse-dict tag storage keyed by slot
    //  - internal TagSet / TagChanged events the WithEvents store subscribes to
    // Production Entity is NOT referenced — this is the bench's own type.
    public sealed class BenchEntity
    {
        public int Id { get; internal set; }

        private readonly Dictionary<int, object?> _slots = new();

        public T Get<T>(BenchTagKey<T> key)
        {
            if (_slots.TryGetValue(key.Slot, out var raw) && raw is T typed)
                return typed;
            return default!;
        }

        public bool Has<T>(BenchTagKey<T> key) => _slots.ContainsKey(key.Slot);

        public void Set<T>(BenchTagKey<T> key, T value)
        {
            if (_slots.TryGetValue(key.Slot, out var raw))
            {
                var prev = raw is T t ? t : default!;
                if (EqualityComparer<T>.Default.Equals(prev, value)) return;
                _slots[key.Slot] = value;
                TagChanged?.Invoke(this, key.Slot, prev!, value!);
            }
            else
            {
                _slots[key.Slot] = value;
                TagSet?.Invoke(this, key.Slot, value!);
            }
        }

        // Untyped slot bag access — Destroy uses it for index cleanup,
        // queries use it for per-predicate matching.
        internal IReadOnlyDictionary<int, object?> Slots => _slots;

        // Event-free setter used by IndexedStore_Pure to model the
        // "ideal lower bound" — index maintained without dispatch.
        internal void SetSlotRaw(int slot, object value, out bool wasNew, out object? prev)
        {
            if (_slots.TryGetValue(slot, out var raw))
            {
                wasNew = false;
                prev = raw;
                _slots[slot] = value;
            }
            else
            {
                wasNew = true;
                prev = null;
                _slots[slot] = value;
            }
        }

        internal event Action<BenchEntity, int, object>? TagSet;
        internal event Action<BenchEntity, int, object, object>? TagChanged;
    }
}
