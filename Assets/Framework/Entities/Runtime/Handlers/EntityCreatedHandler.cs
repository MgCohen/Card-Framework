using Framework.Wire;

namespace Framework.Entities
{
    // Mirror-side writer for EntityCreatedEvent. EntityStore.Insert is idempotent on dup
    // id, so this is safe to run server-side too (the service already inserted) — same
    // path both sides, no fork.
    internal sealed class EntityCreatedHandler : WireEventHandler<EntityCreatedEvent>
    {
        readonly EntityStore _store;
        public EntityCreatedHandler(EntityStore store) { _store = store; }
        public override void Apply(EntityCreatedEvent ev)
            => _store.Insert(ev.EntityId, EntityTypeRegistry.BySlot(ev.TypeSlot), ev.DefinitionId);
    }
}
