using System.Collections.Generic;
using Framework.Wire;

namespace Framework.Tags
{
    // Command surface over TagStore. Writes to the store + emits the matching wire event.
    // Equality short-circuit on Set kills accidental re-entry as a defense in depth backstop.
    public sealed class TagService
    {
        readonly TagStore _store;
        readonly IEventEmitter _events;

        public TagService(TagStore store, IEventEmitter events)
        {
            _store = store;
            _events = events;
        }

        public T Get<T>(int entityId, TagKey<T> key) where T : unmanaged
            => _store.Get<T>(entityId, key.Slot);

        public bool Has(int entityId, TagKey key) => _store.Has(entityId, key.Slot);

        public void Set<T>(int entityId, TagKey<T> key, T value) where T : unmanaged
        {
            if (_store.Has(entityId, key.Slot))
            {
                var prev = _store.Get<T>(entityId, key.Slot);
                if (EqualityComparer<T>.Default.Equals(prev, value)) return;  // ← equality short-circuit
                _store.Set(entityId, key.Slot, value);
                _events.Emit(new TagChangeEvent<T>
                {
                    EntityId = entityId,
                    Slot     = key.Slot,
                    Prev     = prev,
                    Next     = value,
                });
            }
            else
            {
                _store.Set(entityId, key.Slot, value);
                _events.Emit(new TagSetEvent<T>
                {
                    EntityId = entityId,
                    Slot     = key.Slot,
                    Value    = value,
                });
            }
        }

        public bool Remove(int entityId, TagKey key)
        {
            if (!_store.Remove(entityId, key.Slot)) return false;
            _events.Emit(new TagRemovedEvent { EntityId = entityId, Slot = key.Slot });
            return true;
        }
    }
}
