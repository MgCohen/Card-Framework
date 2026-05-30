using System.Collections.Generic;
using Framework.Wire;

namespace Framework.Tags
{
    // Command surface over TagStore. Peeks the store to dedupe + pick the right event,
    // then emits. The store write happens exclusively through TagSetHandler<T> /
    // TagChangeHandler<T> / TagRemovedHandler — same path on server (this scope) and on
    // the client (via the wire). No double-write.
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
                if (EqualityComparer<T>.Default.Equals(prev, value)) return;
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
            if (!_store.Has(entityId, key.Slot)) return false;
            _events.Emit(new TagRemovedEvent { EntityId = entityId, Slot = key.Slot });
            return true;
        }
    }
}
