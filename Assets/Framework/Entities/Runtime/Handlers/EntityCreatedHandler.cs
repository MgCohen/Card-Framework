using Framework.Wire;

namespace Framework.Entities
{
    // Sole writer for EntityCreatedEvent on both sides. Server: EntityService allocates
    // an id and emits — this handler runs synchronously and does the Insert. Client: the
    // wire delivers the event — same handler, same write. Explicit-id Insert is idempotent
    // for safety, but the no-double-write invariant means it shouldn't trigger.
    internal sealed class EntityCreatedHandler : WireEventHandler<EntityCreatedEvent>
    {
        readonly EntityStore _store;
        public EntityCreatedHandler(EntityStore store) { _store = store; }
        public override void Apply(EntityCreatedEvent ev)
            => _store.Insert(ev.EntityId, EntityTypeRegistry.BySlot(ev.TypeSlot), ev.DefinitionId);
    }
}
