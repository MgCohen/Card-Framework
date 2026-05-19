using System;
using System.Collections.Generic;
using Framework.Wire;

namespace Framework.Tags.Tests
{
    // ================================================================
    // TEST STUB — not the real Entity API. Exists only to exercise the
    // tag registry + key shape + wire round-trip end-to-end.
    // Delete when Framework.Identity ships a real entity.
    // ================================================================
    internal sealed class TagBagProbe
    {
        readonly Dictionary<int, object> _slots = new();

        public int EntityId;
        public Action<WireEvent> Emit;   // null = no emission

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

        public void Apply<T>(TagSetEvent<T> ev) where T : unmanaged
        {
            _slots[ev.Slot] = ev.Value;
        }

        public void Apply(TagRemovedEvent ev) => _slots.Remove(ev.Slot);
    }
}
