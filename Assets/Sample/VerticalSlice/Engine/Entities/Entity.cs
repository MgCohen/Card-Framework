#nullable enable
using System;
using System.Collections.Generic;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Entities
{
    /// <summary>
    /// The single base type for everything that participates in the game
    /// state — cards, modifiers, zones, players, matches.
    ///
    /// Storage is a sparse <c>(slot → boxed value)</c> dictionary keyed by
    /// <see cref="TagKey.Slot"/>. Boxing is acceptable at slice scale.
    ///
    /// Equality of values uses <c>EqualityComparer&lt;T&gt;.Default</c>:
    /// value equality for value types, reference equality for reference
    /// types. Reference-typed tag values (lists, dicts) must be reassigned
    /// to a new instance to register as a change — mutating in place will
    /// be a silent no-op.
    /// </summary>
    public abstract class Entity
    {
        public int Id { get; internal set; }
        private readonly Dictionary<int, object?> _slots = new();

        /// <summary>
        /// Fires after a tag value actually changed (the equality-skip
        /// has already cleared no-op writes). Internal — only the
        /// <c>World</c> subscribes, so the call sites are bounded.
        /// </summary>
        internal event Action<Entity, TagKey, object?, object?>? TagChanged;

        public T Get<T>(TagKey<T> key)
        {
            if (_slots.TryGetValue(key.Slot, out var raw) && raw is T t)
                return t;
            return default!;
        }

        public bool Has(TagKey key) => _slots.ContainsKey(key.Slot);

        public void Set<T>(TagKey<T> key, T value)
        {
            T prev = default!;
            if (_slots.TryGetValue(key.Slot, out var raw) && raw is T existing)
                prev = existing;

            if (EqualityComparer<T>.Default.Equals(prev, value))
                return;

            _slots[key.Slot] = value;
            TagChanged?.Invoke(this, key, prev, value);
        }

        public override string ToString() => $"{GetType().Name}#{Id}";
    }
}
