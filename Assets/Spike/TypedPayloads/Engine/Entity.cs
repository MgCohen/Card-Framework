#nullable enable
using System;
using System.Collections.Generic;

namespace TypedPayloads.Engine
{
    /// <summary>
    /// Slot-keyed entity. <see cref="Set{T}"/> raises internal events that
    /// <see cref="EntityStore"/> subscribes to in order to emit wire events —
    /// Entity itself knows nothing about the wire. The internal-event
    /// visibility means game code cannot subscribe and accidentally short
    /// the pipeline.
    /// </summary>
    public sealed class Entity
    {
        public long Id { get; internal set; }

        private readonly Dictionary<int, object?> _slots = new();

        public T Get<T>(TagKey<T> key)
        {
            if (_slots.TryGetValue(key.Slot, out var raw) && raw is T typed)
                return typed;
            return default!;
        }

        public bool Has<T>(TagKey<T> key) => _slots.ContainsKey(key.Slot);

        public void Set<T>(TagKey<T> key, T value)
        {
            if (_slots.TryGetValue(key.Slot, out var raw))
            {
                var prev = raw is T t ? t : default!;
                if (EqualityComparer<T>.Default.Equals(prev, value)) return;   // no-op skip
                _slots[key.Slot] = value;
                TagChanged?.Invoke(this, key, prev!, value!);
            }
            else
            {
                _slots[key.Slot] = value;
                TagSet?.Invoke(this, key, value!);
            }
        }

        public void Remove<T>(TagKey<T> key)
        {
            if (!_slots.TryGetValue(key.Slot, out var raw)) return;
            _slots.Remove(key.Slot);
            TagRemoved?.Invoke(this, key, raw!);
        }

        // Subscribed by EntityStore only. Internal so authors can't bypass
        // the wire-emit path by listening directly.
        internal event Action<Entity, TagKey, object>?         TagSet;
        internal event Action<Entity, TagKey, object, object>? TagChanged;
        internal event Action<Entity, TagKey, object>?         TagRemoved;

        public override string ToString() => $"Entity#{Id}";
    }
}
