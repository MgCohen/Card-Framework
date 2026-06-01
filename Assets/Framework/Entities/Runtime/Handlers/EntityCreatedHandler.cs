using Framework.Wire;

namespace Framework.Entities
{
    // Sole writer for EntityCreatedEvent on both sides. Server: EntityService allocates
    // an id and emits — this handler runs synchronously and does the Insert. Client: the
    // wire delivers the event — same handler, same write. Explicit-id Insert is idempotent
    // for safety, but the no-double-write invariant means it shouldn't trigger.
    //
    // The injected EntityTypeRegistry rehydrates the kind from the wire's int slot — the
    // handler reaches a scoped registry, never a static global.
    internal sealed class EntityCreatedHandler : WireEventHandler<EntityCreatedEvent>
    {
        readonly EntityStore _store;
        readonly EntityTypeRegistry _types;

        public EntityCreatedHandler(EntityStore store, EntityTypeRegistry types)
        {
            _store = store;
            _types = types;
        }

        public override void Apply(EntityCreatedEvent ev)
            => _store.Insert(ev.EntityId, _types.BySlot(ev.TypeSlot), ev.DefinitionId);
    }
}
