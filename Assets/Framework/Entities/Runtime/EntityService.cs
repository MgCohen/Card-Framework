using Framework.Wire;

namespace Framework.Entities
{
    // Command surface over EntityStore. Allocates an id, emits EntityCreatedEvent, returns
    // the entity the handler synchronously inserted. The store write happens exclusively
    // through the EntityCreatedHandler — same path on server (this scope) and on the client
    // (via the wire). No double-write.
    public sealed class EntityService
    {
        readonly EntityStore _store;
        readonly IEventEmitter _events;

        public EntityService(EntityStore store, IEventEmitter events)
        {
            _store = store;
            _events = events;
        }

        public Entity Create(EntityType type, int definitionId = 0)
        {
            var id = _store.AllocateId();
            _events.Emit(new EntityCreatedEvent
            {
                EntityId     = id,
                TypeSlot     = type.Slot,
                DefinitionId = definitionId,
            });
            return _store.ById(id);
        }

        public Entity ById(int id) => _store.ById(id);
        public bool TryGetById(int id, out Entity entity) => _store.TryGetById(id, out entity);
    }
}
