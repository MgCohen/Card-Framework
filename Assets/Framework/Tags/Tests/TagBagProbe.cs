using System;
using System.Collections.Generic;
using Framework.Wire;

namespace Framework.Tags.Tests
{
    // Test stub — delete when Framework.Identity ships a real entity.
    internal sealed class TagBagProbe
    {
        readonly Dictionary<int, object> _slots = new();

        public int EntityId;
        public Action<WireEvent> Emit;

        public void Set<T>(TagKey<T> key, T value) where T : unmanaged
        {
            _slots[key.Slot] = value;
            Emit?.Invoke(new TagSetEvent<T>
            {
                EntityId = EntityId,
                Slot     = key.Slot,
                Value    = value,
            });
        }

        public T    Get<T>(TagKey<T> key) where T : unmanaged => (T)_slots[key.Slot];
        public bool Has(TagKey key)                            => _slots.ContainsKey(key.Slot);

        public bool Remove(TagKey key)
        {
            if (!_slots.Remove(key.Slot)) return false;
            Emit?.Invoke(new TagRemovedEvent { EntityId = EntityId, Slot = key.Slot });
            return true;
        }

        public void Set<TTag>(bool   value) where TTag : ITagKey<bool>   => Set(Tag<TTag, bool>.Key,   value);
        public void Set<TTag>(int    value) where TTag : ITagKey<int>    => Set(Tag<TTag, int>.Key,    value);

        public T    Get<TTag, T>()          where TTag : ITagKey<T> where T : unmanaged
            => Get(Tag<TTag, T>.Key);

        public bool Has<TTag>()             where TTag : ITagKey       => Has(Tag<TTag>.Key);
        public bool Remove<TTag>()          where TTag : ITagKey       => Remove(Tag<TTag>.Key);

        public void Apply<T>(TagSetEvent<T> ev) where T : unmanaged
        {
            _slots[ev.Slot] = ev.Value;
        }

        public void Apply(TagRemovedEvent ev) => _slots.Remove(ev.Slot);
    }
}
