using Framework.Wire;

namespace Framework.Entities
{
    // Command surface over EntityStore. Public Create allocates an entity via the store and
    // emits EntityCreatedEvent so the mirror side can reconstruct it. The store handles
    // raw inserts; this class owns the emit seam.
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
            var entity = _store.Insert(type, definitionId);
            _events.Emit(new EntityCreatedEvent
            {
                EntityId     = entity.Id,
                TypeSlot     = type.Slot,
                DefinitionId = definitionId,
            });
            return entity;
        }

        public Entity ById(int id) => _store.ById(id);
        public bool TryGetById(int id, out Entity entity) => _store.TryGetById(id, out entity);
    }
}
