using Framework.Wire;

namespace Framework.Entities
{
    public sealed class EntityService
    {
        readonly EntityStore _store;
        readonly IEventEmitter _events;
        int _nextId = 1;

        public EntityService(EntityStore store, IEventEmitter events)
        {
            _store = store;
            _events = events;
        }

        public Entity Create(EntityType type, int definitionId = 0)
        {
            var id = _nextId++;
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
