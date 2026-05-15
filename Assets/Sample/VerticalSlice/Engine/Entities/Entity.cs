#nullable enable
using System;
using System.Collections.Generic;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Entities
{
    /// <summary>
    /// The single concrete entity type. Every game thing — card, zone,
    /// player, modifier, anything — is an Entity. Identity comes from
    /// <see cref="Id"/>; "kind" comes from tags (<c>Tags.IsCard</c>,
    /// <c>Tags.IsZone</c>, …); state comes from tag values.
    ///
    /// Sparse-dict storage keyed by <see cref="TagKey.Slot"/>. Missing
    /// keys return <c>default(T)</c> from <see cref="Get{T}"/>; to
    /// distinguish "never set" from "set to default" use <see cref="Has"/>.
    /// The wire alphabet uses <c>TagSetEvent</c> for first-write-of-a-key
    /// and <c>TagChangeEvent</c> for subsequent writes — that's how the
    /// mirror knows whether to remove the key on Unapply or write back the
    /// previous value.
    ///
    /// Mutation goes through <see cref="Set{T}"/>. The owning <c>World</c>
    /// subscribes to internal <see cref="TagSet"/> / <see cref="TagChanged"/>
    /// events to emit wire events. Authors writing inside an
    /// <c>ICommandScope</c> reach Entity through <c>scope.Resolve(...)</c>;
    /// authors inside an <c>IEffectScope</c> only see <see cref="IEntity"/>.
    /// </summary>
    public sealed class Entity : IEntity
    {
        public int Id { get; internal set; }

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
                if (EqualityComparer<T>.Default.Equals(prev, value)) return;   // no-op
                _slots[key.Slot] = value;
                TagChanged?.Invoke(this, key, prev!, value!);
            }
            else
            {
                _slots[key.Slot] = value;
                TagSet?.Invoke(this, key, value!);
            }
        }

        /// <summary>
        /// Three-state clear — strips the slot entirely. Distinct from
        /// <c>Set(key, default)</c> ("silenced"): after Remove,
        /// <see cref="Has"/> returns false. Emits
        /// <c>TagRemovedEvent</c> on the wire; mirror unapply restores
        /// the previous value at the slot.
        /// </summary>
        public void Remove<T>(TagKey<T> key)
        {
            if (!_slots.TryGetValue(key.Slot, out var raw)) return;
            _slots.Remove(key.Slot);
            TagRemoved?.Invoke(this, key, raw!);
        }

        // --- Pattern B sugar (type-keyed; routes through Tag<TTag>.Key) ----

        public void Set<TTag>(bool value) where TTag : class, ITagKey<bool>
            => Set((TagKey<bool>)Tag<TTag>.Key, value);

        public bool Has<TTag>() where TTag : class, ITagKey<bool>
            => Has((TagKey<bool>)Tag<TTag>.Key);

        public bool Get<TTag>() where TTag : class, ITagKey<bool>
            => Get((TagKey<bool>)Tag<TTag>.Key);

        public void Remove<TTag>() where TTag : class, ITagKey<bool>
            => Remove((TagKey<bool>)Tag<TTag>.Key);

        public override string ToString() => $"Entity#{Id}";

        // Subscribed by EntityStore only. Internal so authors can't bypass
        // the wire.
        internal event Action<Entity, TagKey, object>? TagSet;
        internal event Action<Entity, TagKey, object, object>? TagChanged;
        internal event Action<Entity, TagKey, object>? TagRemoved;

        // --- Internal accessors for EntityStore + EntityQuery -------------
        // The store needs slot-keyed reads (untyped) for index maintenance
        // and predicate matching without paying generic dispatch per call.
        // Stays internal so the public surface remains Get<T>(TagKey<T>).

        internal IEnumerable<KeyValuePair<int, object?>> SnapshotSlots() => _slots;

        internal bool TryGetSlot(int slot, out object? value)
            => _slots.TryGetValue(slot, out value);

        internal bool HasSlot(int slot) => _slots.ContainsKey(slot);
    }
}
