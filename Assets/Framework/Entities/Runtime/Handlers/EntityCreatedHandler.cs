using Framework.Wire;

namespace Framework.Entities
{
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
