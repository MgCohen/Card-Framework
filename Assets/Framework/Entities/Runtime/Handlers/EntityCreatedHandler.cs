using System;
using System.Threading;
using System.Threading.Tasks;
using Framework.Wire;
using Scaffold.AppFlow;

namespace Framework.Entities
{
    // Mirror-side writer for EntityCreatedEvent. Inserts (idempotently) into EntityStore.
    // Type-system no-bounce: ctor only sees EntityStore + IHandlerRegistry, no IEventEmitter,
    // no EntityService. Cannot emit, only insert.
    internal sealed class EntityCreatedHandler : IWireEventHandler<EntityCreatedEvent>, IAsyncInitializable
    {
        readonly EntityStore _store;
        readonly IHandlerRegistry _handlers;

        public EntityCreatedHandler(EntityStore store, IHandlerRegistry handlers)
        {
            _store = store;
            _handlers = handlers;
        }

        public Task InitializeAsync(CancellationToken ct)
        {
            _handlers.Register<EntityCreatedEvent>(this);
            return Task.CompletedTask;
        }

        public void Apply(EntityCreatedEvent ev)
            => _store.Insert(ev.EntityId, EntityTypeRegistry.BySlot(ev.TypeSlot), ev.DefinitionId);

        public void Unapply(EntityCreatedEvent ev) => throw new NotImplementedException("Rollback lights up later");
    }
}
