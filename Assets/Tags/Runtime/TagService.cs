using System.Collections.Generic;
using Framework.Wire;

namespace Framework.Tags
{
    public sealed class TagService
    {
        readonly TagStore _store;
        readonly IEventEmitter _events;

        public TagService(TagStore store, IEventEmitter events)
        {
            _store = store;
            _events = events;
        }

        public T Get<T>(int entityId, Tag<T> key) where T : unmanaged
            => _store.Get<T>(entityId, key.Slot);

        public bool Has(int entityId, Tag key) => _store.Has(entityId, key.Slot);

        public void Set<T>(int entityId, Tag<T> key, T value) where T : unmanaged
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

        public bool Remove(int entityId, Tag key)
        {
            if (!_store.Has(entityId, key.Slot)) return false;
            _events.Emit(new TagRemovedEvent { EntityId = entityId, Slot = key.Slot });
            return true;
        }
    }
}
